using System.Globalization;
using Terra.Engine.Events;

namespace Terra.Presentation.Text;

/// <summary>
/// Human-readable one-line format for each event. Designed for end-users
/// watching the simulation as a timeline, plus developers reading logs.
///
/// Line shape: <c>[t=NNNN] CATEGORY: details</c>
/// </summary>
public sealed class HumanTextFormatter : IEventFormatter
{
    public string? Format(SimulationEvent evt)
    {
        var t = $"[t={evt.Tick.ToString("D4", CultureInfo.InvariantCulture)}]";
        return evt switch
        {
            SimulationStarted s =>
                $"{t} === Simulation started: {s.WorldWidth}x{s.WorldHeight}, " +
                $"{s.InitialOrganismCount} organisms, seed={s.Seed}",

            SimulationEnded s =>
                $"{t} === Simulation ended: {s.Reason}, {s.FinalOrganismCount} organisms remain",

            TickCompleted c =>
                $"{t} Tick: P={c.PlantCount} H={c.HerbivoreCount} C={c.CarnivoreCount} " +
                $"(total={c.TotalOrganisms})",

            OrganismBorn b =>
                $"{t} Born: {b.Kind} {b.Id} \"{b.SpeciesName}\" at {b.Position} r={b.Radius} gen={b.Generation}",

            OrganismMoved m =>
                $"{t} Moved: {m.Id} {m.From} → {m.To}",

            OrganismDied d =>
                $"{t} Died: {d.Id} \"{d.SpeciesName}\" at {d.Position} age={d.TickAge} [{d.Reason}]",

            _ => $"{t} {evt.GetType().Name}",
        };
    }
}
