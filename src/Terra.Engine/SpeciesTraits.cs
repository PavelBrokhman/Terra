namespace Terra.Engine;

/// <summary>
/// Point allocation across species traits. Each value is 0–100; the sum of all
/// point-based traits must not exceed <see cref="EngineConstants.MaxCharacteristicPoints"/> (100).
/// MatureSize is NOT part of the point budget — it is a fixed size attribute
/// in the range [MinMatureSize, MaxMatureSize] = [25, 48] pixels diameter.
/// See EngineSettings.cs:29 and MatureSizeAttribute.cs.
/// </summary>
public sealed record SpeciesTraits
{
    public int MaximumEnergyPoints { get; init; }
    public int MaximumSpeedPoints { get; init; }
    public int EatingSpeedPoints { get; init; }
    public int AttackDamagePoints { get; init; }
    public int DefendDamagePoints { get; init; }
    public int EyesightPoints { get; init; }
    public int CamouflagePoints { get; init; }

    /// <summary>Diameter in pixels, 25–48. Not part of the point budget.</summary>
    public int MatureSize { get; init; }

    /// <summary>Total points allocated across point-based traits (excluding MatureSize).</summary>
    public int TotalPoints =>
        MaximumEnergyPoints + MaximumSpeedPoints + EatingSpeedPoints +
        AttackDamagePoints + DefendDamagePoints + EyesightPoints + CamouflagePoints;

    /// <summary>Validates that all points are in [0,100], their sum is ≤ 100, and MatureSize is in range.</summary>
    public void Validate()
    {
        static void InRange(string name, int value, int lo, int hi)
        {
            if (value < lo || value > hi)
                throw new ArgumentOutOfRangeException(name, value, $"must be in [{lo},{hi}]");
        }

        InRange(nameof(MaximumEnergyPoints), MaximumEnergyPoints, 0, 100);
        InRange(nameof(MaximumSpeedPoints), MaximumSpeedPoints, 0, 100);
        InRange(nameof(EatingSpeedPoints), EatingSpeedPoints, 0, 100);
        InRange(nameof(AttackDamagePoints), AttackDamagePoints, 0, 100);
        InRange(nameof(DefendDamagePoints), DefendDamagePoints, 0, 100);
        InRange(nameof(EyesightPoints), EyesightPoints, 0, 100);
        InRange(nameof(CamouflagePoints), CamouflagePoints, 0, 100);
        InRange(nameof(MatureSize), MatureSize, EngineConstants.MinMatureSize, EngineConstants.MaxMatureSize);

        if (TotalPoints > EngineConstants.MaxCharacteristicPoints)
            throw new InvalidOperationException(
                $"Total trait points ({TotalPoints}) exceed budget of {EngineConstants.MaxCharacteristicPoints}.");
    }
}
