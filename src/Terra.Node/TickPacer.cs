namespace Terra.Node;

/// <summary>How a world decides the length of its next tick.</summary>
public enum TickMode
{
    /// <summary>Always the same step, whatever the participants do.</summary>
    Fixed,

    /// <summary>Stretches as observed response grows, so slow participants are waited for.</summary>
    Adaptive,
}

/// <summary>
/// Decides how long the next tick takes. The world's owner sets this — the engine
/// never substitutes a quiet default, and a badly chosen setting is allowed to
/// stall the world. See Plans/Phase3_Milestones.md, T7.
/// </summary>
public interface ITickPacer
{
    TickMode Mode { get; }

    /// <param name="observedResponse">
    /// Slowest participant's round-trip since the last tick. In M1 that is the
    /// measure of "response"; later this is where an external behaviour service's
    /// latency would be fed in instead.
    /// </param>
    TimeSpan NextDelay(TimeSpan observedResponse);
}

/// <summary>Constant tick length, ignoring how anyone responds.</summary>
public sealed class FixedTickPacer(TimeSpan step) : ITickPacer
{
    private readonly TimeSpan _step = step > TimeSpan.Zero
        ? step
        : throw new ArgumentOutOfRangeException(nameof(step), step, "must be > 0");

    public TickMode Mode => TickMode.Fixed;

    public TimeSpan NextDelay(TimeSpan observedResponse) => _step;
}

/// <summary>
/// Tick length follows the observed response: the slower participants answer, the
/// longer the tick, bounded by <c>min</c> and <c>max</c>. Bounded on purpose —
/// unbounded growth would let one hung participant stop the world for good, and
/// how far to stretch is a decision the owner should make explicitly.
/// </summary>
public sealed class AdaptiveTickPacer(TimeSpan min, TimeSpan max, double slack = 1.5) : ITickPacer
{
    private readonly TimeSpan _min = min > TimeSpan.Zero
        ? min
        : throw new ArgumentOutOfRangeException(nameof(min), min, "must be > 0");

    private readonly TimeSpan _max = max >= min
        ? max
        : throw new ArgumentOutOfRangeException(nameof(max), max, "must be ≥ min");

    private readonly double _slack = slack >= 1.0
        ? slack
        : throw new ArgumentOutOfRangeException(nameof(slack), slack, "must be ≥ 1");

    public TickMode Mode => TickMode.Adaptive;

    public TimeSpan NextDelay(TimeSpan observedResponse)
    {
        if (observedResponse < TimeSpan.Zero) observedResponse = TimeSpan.Zero;
        var target = observedResponse * _slack;
        if (target < _min) return _min;
        return target > _max ? _max : target;
    }
}
