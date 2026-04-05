namespace Terra.Engine;

/// <summary>
/// Five energy buckets (MaxEnergy / 5 each) that gate eating and reproduction.
/// See OrganismState.cs:764-822 in the legacy source.
/// </summary>
public enum EnergyState
{
    /// <summary>energy == 0</summary>
    Dead,

    /// <summary>(0, 1×bucket]</summary>
    Deterioration,

    /// <summary>(1×bucket, 2×bucket]</summary>
    Hungry,

    /// <summary>(2×bucket, 4×bucket] — can eat and reproduce</summary>
    Normal,

    /// <summary>(4×bucket, max] — cannot eat, can still reproduce</summary>
    Full,
}
