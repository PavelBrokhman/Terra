using System.Text;
using Terra.Web;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// Run parameters: defaults, overridable via the "Simulation" config section
// (appsettings.json / env vars / --Simulation:Seed=... on the command line).
var simOptions = new SimulationOptions();
builder.Configuration.GetSection("Simulation").Bind(simOptions);

builder.Services.AddSingleton(simOptions);
builder.Services.AddSingleton<Broadcaster>();
builder.Services.AddSingleton<SimulationRunner>();

var app = builder.Build();

app.UseDefaultFiles();   // serve wwwroot/index.html at "/"
app.UseStaticFiles();

// Server-Sent Events: stream every simulation event (JSON-Lines) to the browser.
app.MapGet("/events", async (HttpContext ctx, Broadcaster bc) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("X-Accel-Buffering", "no");

    var (id, reader) = bc.Subscribe();
    try
    {
        await foreach (var line in reader.ReadAllAsync(ctx.RequestAborted))
        {
            // Coalesce a tick's burst of events into a single write + flush.
            var sb = new StringBuilder().Append("data: ").Append(line).Append("\n\n");
            while (reader.TryRead(out var more))
                sb.Append("data: ").Append(more).Append("\n\n");
            await ctx.Response.WriteAsync(sb.ToString(), ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* client disconnected — fine */ }
    finally { bc.Unsubscribe(id); }
});

// Start (or restart) a run from the UI with a chosen tick budget.
app.MapPost("/run", (SimulationRunner runner, int? ticks) =>
{
    var maxTicks = ticks is > 0 ? ticks.Value : simOptions.MaxTicks;
    runner.Start(maxTicks);
    return Results.Ok(new { started = true, ticks = maxTicks });
});

app.Run();
