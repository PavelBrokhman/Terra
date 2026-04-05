namespace Terra.Engine;

/// <summary>
/// Pure functions computing gameplay values from species traits and organism
/// state. All formulas are faithful ports from the legacy Terrarium source;
/// see Plans/Phase1_GameRules.md for derivations.
/// </summary>
public static class GameRules
{
    /// <summary>MaxEnergy = (19,040 + (points/100) × 380,800) × radius</summary>
    public static double MaxEnergy(SpeciesTraits traits, int radius) =>
        (EngineConstants.MaxEnergyBasePerUnitRadius
         + traits.MaximumEnergyPoints / 100.0 * EngineConstants.MaxEnergyMaximumPerUnitRadius)
        * radius;

    /// <summary>Energy threshold bucket size = MaxEnergy / 5.</summary>
    public static double EnergyBucket(SpeciesTraits traits, int radius) =>
        MaxEnergy(traits, radius) / 5.0;

    /// <summary>
    /// Classify an organism's current energy into one of the five buckets
    /// (see OrganismState.cs:764-822 in legacy source).
    /// </summary>
    public static EnergyState ClassifyEnergyState(double energy, SpeciesTraits traits, int radius)
    {
        if (energy <= 0) return EnergyState.Dead;
        var bucket = EnergyBucket(traits, radius);
        if (energy <= bucket)         return EnergyState.Deterioration;
        if (energy <= 2 * bucket)     return EnergyState.Hungry;
        if (energy <= 4 * bucket)     return EnergyState.Normal;
        return EnergyState.Full;
    }

    /// <summary>
    /// Lifespan in ticks, computed from species kind and mature radius.
    /// Carnivores live 2× longer than herbivores (EngineSettings.cs:273).
    /// Plants use a separate multiplier (EngineSettings.cs:282).
    /// </summary>
    public static int LifeSpan(Species species) => species.Kind switch
    {
        SpeciesKind.Plant     => EngineConstants.PlantLifeSpanPerUnitMaxRadius * species.MatureRadius,
        SpeciesKind.Herbivore => EngineConstants.AnimalLifeSpanPerUnitMaxRadius * species.MatureRadius,
        SpeciesKind.Carnivore => EngineConstants.AnimalLifeSpanPerUnitMaxRadius
                                 * EngineConstants.CarnivoreLifeSpanMultiplier
                                 * species.MatureRadius,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };

    /// <summary>Speed = 5 + (points/100) × 95, range [5,100].</summary>
    public static int MaxSpeed(SpeciesTraits traits) =>
        EngineConstants.SpeedBase
        + (int)(traits.MaximumSpeedPoints / 100.0
                * (EngineConstants.SpeedMaximum - EngineConstants.SpeedBase));

    /// <summary>
    /// Eyesight radius in pixels. Formula: cells = 5 + (points/100) × 10,
    /// then multiplied by cell size (8 pixels). Range: 40–120 pixels.
    /// </summary>
    public static int EyesightRadiusPixels(SpeciesTraits traits)
    {
        var cells = EngineConstants.BaseEyesightRadiusCells
                    + traits.EyesightPoints / 100.0 * EngineConstants.MaximumEyesightRadiusCells;
        return (int)(cells * EngineConstants.GridCellWidth);
    }

    /// <summary>
    /// Per-tick metabolism cost. Plants: 1 × radius. Animals: 0.001 × radius.
    /// Plants ALSO gain up to <see cref="EngineConstants.MaxEnergyFromLightPerTick"/>
    /// from photosynthesis (see <see cref="PhotosynthesisGain"/>).
    /// </summary>
    public static double MetabolismCost(SpeciesKind kind, int radius) => kind switch
    {
        SpeciesKind.Plant => EngineConstants.BasePlantEnergyPerUnitRadius * radius,
        _                 => EngineConstants.BaseAnimalEnergyPerUnitRadius * radius,
    };

    /// <summary>
    /// Photosynthesis gain for plants. Phase 1 MVP: grant the maximum
    /// (550 energy/tick) capped at the organism's missing energy. Environmental
    /// factors (shade, crowding) from the original can be layered in later.
    /// </summary>
    public static double PhotosynthesisGain(SpeciesKind kind) => kind switch
    {
        SpeciesKind.Plant => EngineConstants.MaxEnergyFromLightPerTick,
        _                 => 0,
    };

    /// <summary>Movement energy cost: distance × radius × speed × 0.005.</summary>
    public static double MovementEnergyCost(int radius, double distance, int speed) =>
        distance * radius * speed * EngineConstants.RequiredEnergyPerUnitRadiusSpeedDistance;
}
