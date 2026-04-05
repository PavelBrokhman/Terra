namespace Terra.Engine;

/// <summary>
/// Configuration for a <see cref="Simulation"/> run. All randomness is derived
/// from <see cref="Seed"/>, so the same seed + same world + same behaviors
/// produce identical event streams.
/// </summary>
public sealed record SimulationConfig
{
    /// <summary>PRNG seed. 0 is allowed; change for different runs.</summary>
    public int Seed { get; init; } = 0;
}
