using Terra.Engine.Events;

namespace Terra.Presentation.Text;

/// <summary>
/// Converts a <see cref="SimulationEvent"/> to a single output line.
/// Return <c>null</c> to skip an event (e.g., filter suppressing noisy ticks).
/// </summary>
public interface IEventFormatter
{
    string? Format(SimulationEvent evt);
}
