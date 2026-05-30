namespace Terra.Engine;

/// <summary>
/// Gameplay constants ported verbatim from Terrarium 2.0's EngineSettings.cs.
/// All values documented with file:line references to the legacy source
/// under legacy/Terrarium2.0/Client/OrganismBase/Classes/Engine/EngineSettings.cs
/// unless noted otherwise. See Plans/Phase1_GameRules.md for derivations.
/// </summary>
public static class EngineConstants
{
    // ── Point budget ─────────────────────────────────────────────────────
    public const int MaxCharacteristicPoints = 100;          // :29

    // ── MatureSize bounds (pixels, diameter) ─────────────────────────────
    public const int MinMatureSize = 25;                     // :225
    public const int MaxMatureSize = 48;                     // :216

    // ── Energy (per unit radius) ─────────────────────────────────────────
    public const int MaxEnergyBasePerUnitRadius = 19_040;    // :99
    public const int MaxEnergyMaximumPerUnitRadius = 380_800;// :85

    // ── Metabolism (energy per tick, per unit radius) ────────────────────
    public const double BaseAnimalEnergyPerUnitRadius = 0.001; // :302
    public const double BasePlantEnergyPerUnitRadius = 1.0;    // :383

    // ── Movement ─────────────────────────────────────────────────────────
    public const double RequiredEnergyPerUnitRadiusSpeedDistance = 0.005; // AnimalState.cs:341
    public const int SpeedBase = 5;                          // :115
    public const int SpeedMaximum = 100;                     // :107

    // ── Food / eating ────────────────────────────────────────────────────
    public const int EnergyPerAnimalFoodChunk = 1;           // :353
    public const int EnergyPerPlantFoodChunk = 1;            // :418
    public const int FoodChunksPerUnitRadius = 25;           // :345  (animal corpse)
    public const int PlantFoodChunksPerUnitRadius = 50;      // :392
    public const int BaseEatingSpeedPerUnitRadius = 1;       // :137
    public const int MaximumEatingSpeedPerUnitRadius = 100;  // :126

    // ── Combat ───────────────────────────────────────────────────────────
    public const int BaseInflictedDamagePerUnitRadius = 50;  // :160
    public const int MaximumInflictedDamagePerUnitRadius = 25; // :149  (additive to base)
    public const int BaseDefendedDamagePerUnitRadius = 50;   // :183
    public const int MaximumDefendedDamagePerUnitRadius = 25;// :172
    public const int DamageToKillPerUnitRadius = 190;        // :363
    public const int CarnivoreAttackDefendMultiplier = 2;    // :479

    // ── Eyesight / camouflage ────────────────────────────────────────────
    public const int BaseEyesightRadiusCells = 5;            // :207 (in grid cells)
    public const int MaximumEyesightRadiusCells = 10;        // :195 (additive to base, so max total = 15)
    public const int InvisibilityOddsMaximum = 90;           // :39  (percent)

    // ── Reproduction ─────────────────────────────────────────────────────
    public const int AnimalReproductionWaitPerUnitRadius = 8;  // :245
    public const int PlantReproductionWaitPerUnitRadius = 25;  // :255
    public const int TicksToIncubate = 10;                     // :488

    // ── Lifespan (ticks per unit mature radius) ──────────────────────────
    public const int AnimalLifeSpanPerUnitMaxRadius = 50;    // :264
    public const int PlantLifeSpanPerUnitMaxRadius = 150;    // :282
    public const int CarnivoreLifeSpanMultiplier = 2;        // :273

    // ── Decomposition ────────────────────────────────────────────────────
    public const int TimeToRot = 60;                         // :292

    // ── Spatial grid ─────────────────────────────────────────────────────
    public const int GridCellWidth = 8;                      // :452
    public const int GridCellHeight = 8;                     // :454

    // ── Plant photosynthesis ─────────────────────────────────────────────
    public const int MaxEnergyFromLightPerTick = 550;        // :400

    // ── Misc ─────────────────────────────────────────────────────────────
    public const int MaxSeedSpreadDistance = 1000;           // :235

    // ── Offspring placement (Phase 2 default; not a verbatim legacy value) ─
    public const int PlantSeedSpreadRadius = 64;   // plant seeds drift near parent
    public const int AnimalBirthSpreadRadius = 24; // animals born close to parent
}
