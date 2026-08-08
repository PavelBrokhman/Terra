using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Terra.Node.Host;

/// <summary>
/// This node taking part in somebody else's world. It reads the world's published
/// conditions first and decides whether to go in at all — onboarding is two-sided
/// (Plans/Phase3_Milestones.md, T7) — then joins, sets its spawn policy, keeps
/// itself known with heartbeats, and watches the state stream.
/// </summary>
/// <remarks>
/// Nothing here is a "client type": the same program is running a world in another
/// object at the same time. Joining is a capability, not a role (T3).
/// </remarks>
public sealed class WorldClient(
    string worldId,
    Uri endpoint,
    JoinOptions options,
    string participantName,
    NodeLog log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new() { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(10) };

    public int? ParticipantId { get; private set; }

    public async Task RunAsync(CancellationToken ct)
    {
        var conditions = await WaitForWorldAsync(ct);
        if (conditions is null) return;

        log.Line(worldId, $"conditions: '{conditions.WorldName}' {conditions.Width}x{conditions.Height}, " +
                          $"tick {conditions.TickMode} {conditions.MinTickMs:0}–{conditions.MaxTickMs:0}ms, " +
                          $"quota on join {conditions.QuotaOnJoin}, free zones {conditions.FreeZones}");

        if (!conditions.HasRoom)
        {
            log.Line(worldId, "world is full — not joining. (The terms are the world's to set.)");
            return;
        }

        var join = new JoinRequest(
            participantName,
            options.Species.Select(s => new SpeciesDto(s.Name, s.ParsedKind.ToString())).ToList(),
            options.ParsedPolicy.ToString());

        var response = await _http.PostAsJsonAsync("/world/participants", join, Json, ct);
        if (!response.IsSuccessStatusCode)
        {
            var refusal = await response.Content.ReadFromJsonAsync<RefusalResponse>(Json, ct);
            log.Line(worldId, $"refused: {refusal?.Refusal ?? response.StatusCode.ToString()}");
            return;
        }

        var joined = (await response.Content.ReadFromJsonAsync<JoinResponse>(Json, ct))!;
        ParticipantId = joined.ParticipantId;
        log.Line(worldId, $"joined as P{joined.ParticipantId}: zone {joined.Zone.Index} " +
                          $"[{joined.Zone.X},{joined.Zone.Y} {joined.Zone.Width}x{joined.Zone.Height}], " +
                          $"quota {joined.Quota}, {joined.Placed} organisms placed, " +
                          $"policy {options.ParsedPolicy}");
        if (joined.Note is not null) log.Line(worldId, $"note: {joined.Note}");

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var stream = Task.Run(() => WatchAsync(watching.Token), watching.Token);

        try
        {
            await HeartbeatAsync(joined.ParticipantId, ct);
        }
        finally
        {
            watching.Cancel();
            await LeaveAsync(joined.ParticipantId);
            try { await stream; } catch (OperationCanceledException) { /* expected */ }
        }
    }

    /// <summary>Wait for the world to answer at all. A world that is not up yet is
    /// not an error — the four instances start in whatever order they start.</summary>
    private async Task<ConditionsDto?> WaitForWorldAsync(CancellationToken ct)
    {
        var announced = false;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                return await _http.GetFromJsonAsync<ConditionsDto>("/world", Json, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (!announced)
                {
                    log.Line(worldId, $"waiting for {endpoint} to come up…");
                    announced = true;
                }
                try { await Task.Delay(TimeSpan.FromMilliseconds(500), ct); }
                catch (OperationCanceledException) { return null; }
            }
        }

        return null;
    }

    private async Task HeartbeatAsync(int participantId, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(200, options.HeartbeatMs));
        var roundTrip = TimeSpan.Zero;
        var started = DateTimeOffset.UtcNow;
        var toppedUp = false;
        var beats = 0;

        while (!ct.IsCancellationRequested)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"/world/participants/{participantId}/heartbeat",
                    new HeartbeatRequest(roundTrip.TotalMilliseconds), Json, ct);
                clock.Stop();
                roundTrip = clock.Elapsed;

                if (!response.IsSuccessStatusCode)
                {
                    // The world dropped us: died out, timed out, or was restarted.
                    log.Line(worldId, "no longer a participant — the world let us go");
                    return;
                }

                var beat = (await response.Content.ReadFromJsonAsync<HeartbeatResponse>(Json, ct))!;
                // Count our own beats rather than the world's tick: the two are not
                // in step, so a "every 20th tick" test mostly never fires.
                if (beats++ % 5 == 0)
                    log.Line(worldId, $"tick {beat.Tick}: {beat.Living} of ours alive, " +
                                      $"quota {beat.QuotaUsed}/{beat.QuotaTotal}, " +
                                      $"next tick ~{beat.NextTickMs:0}ms");

                if (!toppedUp && options.TopUpAfterSeconds > 0 && options.Species.Count > 0
                    && (DateTimeOffset.UtcNow - started).TotalSeconds >= options.TopUpAfterSeconds)
                {
                    toppedUp = true;
                    await TopUpAsync(participantId, options.Species[0].Name, options.TopUpCount, ct);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (HttpRequestException ex)
            {
                log.Line(worldId, $"world unreachable: {ex.Message}");
                return;
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>Add more of one species while something of ours is still alive.
    /// No coordinate is sent: the world anchors placement on a living organism.</summary>
    private async Task TopUpAsync(int participantId, string speciesName, int count, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(
            $"/world/participants/{participantId}/organisms",
            new TopUpRequest(speciesName, count, null), Json, ct);

        var result = await response.Content.ReadFromJsonAsync<TopUpResponse>(Json, ct);
        if (result is null) return;

        log.Line(worldId, result.Placed > 0
            ? $"topped up {result.Placed}×{speciesName}" + (result.Refusal is null ? "" : $" ({result.Refusal})")
            : $"top-up refused: {result.Refusal}");
    }

    private async Task WatchAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/world/events");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(body);

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                using var document = JsonDocument.Parse(line[6..]);
                var root = document.RootElement;
                if (root.GetProperty("kind").GetString() != "node") continue;

                log.Line(worldId, $"world says: {root.GetProperty("event").GetString()} " +
                                  $"{root.GetProperty("payload")}");
            }
        }
        catch (OperationCanceledException) { /* leaving */ }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            log.Line(worldId, $"state stream ended: {ex.Message}");
        }
    }

    private async Task LeaveAsync(int participantId)
    {
        try
        {
            // Deliberate departure, so the world takes the quota and zone back at
            // once instead of waiting out the timeout.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _http.DeleteAsync($"/world/participants/{participantId}", cts.Token);
            log.Line(worldId, "left the world");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.Line(worldId, "left without a clean goodbye — the world will time us out");
        }
    }
}
