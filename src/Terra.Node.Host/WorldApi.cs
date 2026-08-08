using System.Text;
using Terra.Engine;

namespace Terra.Node.Host;

/// <summary>
/// The world's network surface: REST for control, SSE for the stream of what is
/// happening. Small on purpose — everything it does is a call into
/// <see cref="WorldHost"/>, which is where the rules actually live.
/// </summary>
public static class WorldApi
{
    public static void Map(WebApplication app, WorldHost host, WorldRunner runner, SseBroadcaster sse, NodeLog log)
    {
        // Published before anyone connects: a participant reads the terms and
        // decides for itself whether they suit (Plans/Phase3_Milestones.md, T7).
        app.MapGet("/world", () =>
        {
            var c = host.Conditions;
            return Results.Json(new ConditionsDto(
                c.WorldName, c.Width, c.Height, c.TickMode.ToString(),
                c.MinTick.TotalMilliseconds, c.MaxTick.TotalMilliseconds,
                c.FreeZones, c.QuotaOnJoin, c.HasRoom,
                host.World.Tick, host.World.LivingCount, host.Participants.Count));
        });

        app.MapGet("/world/participants", () =>
        {
            var now = DateTimeOffset.UtcNow;
            var list = host.Participants.Select(p => new ParticipantDto(
                p.Id.Value, p.Name, p.Zone.Index,
                p.Quota.Total, p.Quota.Used,
                host.LivingOf(p.Id).Count,
                p.Policy.ToString(),
                (now - p.LastSeen).TotalSeconds));
            return Results.Json(list);
        });

        app.MapPost("/world/participants", (JoinRequest request) =>
        {
            SpawnPolicy policy;
            List<SpeciesRequest> species;
            try
            {
                policy = WorldOptions.ParsePolicy(request.Policy);
                species = request.Species
                    .Select(s => new SpeciesRequest(
                        s.Name,
                        Enum.Parse<SpeciesKind>(s.Kind, ignoreCase: true)))
                    .ToList();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new RefusalResponse(ex.Message));
            }

            var result = host.Join(request.Name, species, policy, DateTimeOffset.UtcNow);
            if (!result.Accepted)
                return Results.Json(new RefusalResponse(result.Refusal!), statusCode: 409);

            var p = result.Participant!;
            log.Line("world", $"{p.Id} '{p.Name}' joined: zone {p.Zone.Index}, " +
                              $"quota {p.Quota.Total}, policy {p.Policy}, {result.Placed} placed");
            runner.PublishNodeEvent("joined", new
            {
                participant = p.Id.Value,
                name = p.Name,
                zone = p.Zone.Index,
                quota = p.Quota.Total,
                placed = result.Placed,
            });

            var zone = new ZoneDto(p.Zone.Index, p.Zone.X, p.Zone.Y, p.Zone.Width, p.Zone.Height);
            return Results.Json(new JoinResponse(p.Id.Value, zone, p.Quota.Total, result.Placed, result.Refusal));
        });

        app.MapPost("/world/participants/{id:int}/heartbeat", (int id, HeartbeatRequest request) =>
        {
            var pid = new ParticipantId(id);
            var participant = host.Participants.FirstOrDefault(p => p.Id == pid);
            if (participant is null)
                return Results.Json(new RefusalResponse("not a participant of this world"), statusCode: 404);

            host.Heartbeat(pid, TimeSpan.FromMilliseconds(request.LastRoundTripMs), DateTimeOffset.UtcNow);
            return Results.Json(new HeartbeatResponse(
                host.World.Tick,
                host.LivingOf(pid).Count,
                participant.Quota.Used,
                participant.Quota.Total,
                host.Pacer.NextDelay(host.SlowestResponse).TotalMilliseconds));
        });

        app.MapPost("/world/participants/{id:int}/policy", (int id, PolicyRequest request) =>
        {
            SpawnPolicy policy;
            try { policy = WorldOptions.ParsePolicy(request.Policy); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new RefusalResponse(ex.Message)); }

            if (!host.SetPolicy(new ParticipantId(id), policy))
                return Results.Json(new RefusalResponse("not a participant of this world"), statusCode: 404);

            log.Line("world", $"P{id} spawn policy → {policy}");
            return Results.Json(new PolicyRequest(policy.ToString()));
        });

        app.MapPost("/world/participants/{id:int}/organisms", (int id, TopUpRequest request) =>
        {
            if (request.Count <= 0)
                return Results.BadRequest(new RefusalResponse("count must be > 0"));

            var anchor = request.AnchorOrganismId is { } value ? new OrganismId(value) : (OrganismId?)null;
            var result = host.TopUp(
                new ParticipantId(id), request.SpeciesName, request.Count, anchor, DateTimeOffset.UtcNow);

            if (result.Placed > 0)
                log.Line("world", $"P{id} topped up {result.Placed}×{request.SpeciesName}");

            return Results.Json(new TopUpResponse(result.Placed, result.Refusal));
        });

        app.MapDelete("/world/participants/{id:int}", (int id) =>
        {
            var pid = new ParticipantId(id);
            if (!host.Leave(pid))
                return Results.Json(new RefusalResponse("not a participant of this world"), statusCode: 404);

            log.Line("world", $"{pid} left — quota and zone returned to the world");
            runner.PublishNodeEvent("left", new { participant = id });
            return Results.Ok();
        });

        // The state stream. Same shape as the browser viewer's feed: one message
        // per tick carrying that tick's events, plus node-level messages.
        app.MapGet("/world/events", async (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("X-Accel-Buffering", "no");

            var (subscriber, reader) = sse.Subscribe();
            try
            {
                await foreach (var line in reader.ReadAllAsync(ctx.RequestAborted))
                {
                    var payload = new StringBuilder().Append("data: ").Append(line).Append("\n\n");
                    while (reader.TryRead(out var more))
                        payload.Append("data: ").Append(more).Append("\n\n");
                    await ctx.Response.WriteAsync(payload.ToString(), ctx.RequestAborted);
                    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* watcher went away — nothing to fix */ }
            finally { sse.Unsubscribe(subscriber); }
        });
    }
}
