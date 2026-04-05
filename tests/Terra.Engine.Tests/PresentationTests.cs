using System.Text.Json;
using Terra.Engine.Events;
using Terra.Presentation.Text;

namespace Terra.Engine.Tests;

public class PresentationTests
{
    // ── HumanTextFormatter ───────────────────────────────────────────────
    [Fact]
    public void HumanFormatter_SimulationStarted()
    {
        var f = new HumanTextFormatter();
        var line = f.Format(new SimulationStarted(200, 100, 30, 42));
        Assert.Equal("[t=0000] === Simulation started: 200x100, 30 organisms, seed=42", line);
    }

    [Fact]
    public void HumanFormatter_TickCompleted_IncludesTotals()
    {
        var f = new HumanTextFormatter();
        var line = f.Format(new TickCompleted(17, 5, 3, 1));
        Assert.Equal("[t=0017] Tick: P=5 H=3 C=1 (total=9)", line);
    }

    [Fact]
    public void HumanFormatter_OrganismMoved_ShowsArrow()
    {
        var f = new HumanTextFormatter();
        var line = f.Format(new OrganismMoved(42, new OrganismId(7), new Position(10, 10), new Position(15, 12)));
        Assert.Equal("[t=0042] Moved: #7 (10,10) → (15,12)", line);
    }

    [Fact]
    public void HumanFormatter_OrganismAte_IncludesDetails()
    {
        var f = new HumanTextFormatter();
        var line = f.Format(new OrganismAte(
            15, new OrganismId(3), new OrganismId(7), ChunksEaten: 50,
            EnergyGained: 48.5, TargetChunksRemaining: 200));
        Assert.Equal(
            "[t=0015] Ate: #3 → #7 chunks=50 (+48 energy, target remaining=200)", line);
    }

    [Fact]
    public void HumanFormatter_OrganismDied_IncludesReason()
    {
        var f = new HumanTextFormatter();
        var line = f.Format(new OrganismDied(
            99, new OrganismId(3), "Wolf", new Position(50, 50), 450, DeathReason.OldAge));
        Assert.Equal("[t=0099] Died: #3 \"Wolf\" at (50,50) age=450 [OldAge]", line);
    }

    [Fact]
    public void HumanFormatter_PadsTickTo4Digits()
    {
        var f = new HumanTextFormatter();
        Assert.StartsWith("[t=0005]", f.Format(new TickCompleted(5, 0, 0, 0)));
        Assert.StartsWith("[t=1234]", f.Format(new TickCompleted(1234, 0, 0, 0)));
    }

    // ── JsonLinesFormatter ───────────────────────────────────────────────
    [Fact]
    public void JsonFormatter_IncludesTypeDiscriminator()
    {
        var f = new JsonLinesFormatter();
        var line = f.Format(new TickCompleted(7, 2, 3, 1))!;
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("TickCompleted", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(7, doc.RootElement.GetProperty("tick").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("plantCount").GetInt32());
    }

    [Fact]
    public void JsonFormatter_EnumsAsStrings()
    {
        var f = new JsonLinesFormatter();
        var line = f.Format(new OrganismDied(
            5, new OrganismId(1), "X", new Position(0, 0), 10, DeathReason.Killed))!;
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("Killed", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public void JsonFormatter_ProducesSingleLineNoNewlines()
    {
        var f = new JsonLinesFormatter();
        var line = f.Format(new OrganismBorn(
            0, new OrganismId(1), "P", SpeciesKind.Plant, new Position(5, 5), 3, 0))!;
        Assert.DoesNotContain("\n", line);
        Assert.DoesNotContain("\r", line);
    }

    [Fact]
    public void JsonFormatter_RoundtripsPolymorphically()
    {
        var f = new JsonLinesFormatter();
        var original = new OrganismMoved(10, new OrganismId(2), new Position(1, 2), new Position(3, 4));
        var line = f.Format(original)!;

        // Deserialize as base type: discriminator should select OrganismMoved.
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        var parsed = JsonSerializer.Deserialize<SimulationEvent>(line, options);
        var moved = Assert.IsType<OrganismMoved>(parsed);
        Assert.Equal(10, moved.Tick);
        Assert.Equal(new OrganismId(2), moved.Id);
        Assert.Equal(new Position(1, 2), moved.From);
        Assert.Equal(new Position(3, 4), moved.To);
    }

    // ── EventPresenter ───────────────────────────────────────────────────
    [Fact]
    public void EventPresenter_WritesOneLinePerEvent()
    {
        var bus = new EventBus();
        using var sw = new StringWriter();
        using var presenter = new EventPresenter(bus, new HumanTextFormatter(), sw);

        bus.Publish(new TickCompleted(1, 0, 0, 0));
        bus.Publish(new TickCompleted(2, 0, 0, 0));

        var lines = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void EventPresenter_Dispose_StopsReceiving()
    {
        var bus = new EventBus();
        var sw = new StringWriter();
        var presenter = new EventPresenter(bus, new HumanTextFormatter(), sw);
        bus.Publish(new TickCompleted(1, 0, 0, 0));
        presenter.Dispose();
        bus.Publish(new TickCompleted(2, 0, 0, 0));

        var lineCount = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(1, lineCount);
    }

    [Fact]
    public void EventPresenter_SkipsNullFormattedLines()
    {
        // A formatter that suppresses every TickCompleted.
        var filtering = new SuppressTickFormatter();
        var bus = new EventBus();
        using var sw = new StringWriter();
        using var presenter = new EventPresenter(bus, filtering, sw);

        bus.Publish(new TickCompleted(1, 0, 0, 0));
        bus.Publish(new OrganismBorn(1, new OrganismId(1), "P", SpeciesKind.Plant, new Position(0, 0), 5, 0));

        var lines = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    private sealed class SuppressTickFormatter : IEventFormatter
    {
        private readonly HumanTextFormatter _inner = new();
        public string? Format(SimulationEvent evt) =>
            evt is TickCompleted ? null : _inner.Format(evt);
    }
}
