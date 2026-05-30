using Terra.Web;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// Run parameters: defaults, overridable via the "Simulation" config section
// (appsettings.json / env vars / --Simulation:Seed=... on the command line).
var simOptions = new SimulationOptions();
builder.Configuration.GetSection("Simulation").Bind(simOptions);

builder.Services.AddSingleton(simOptions);
builder.Services.AddSingleton<Broadcaster>();
builder.Services.AddHostedService<SimulationHost>();

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
            await ctx.Response.WriteAsync($"data: {line}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* client disconnected — fine */ }
    finally { bc.Unsubscribe(id); }
});

app.Run();
