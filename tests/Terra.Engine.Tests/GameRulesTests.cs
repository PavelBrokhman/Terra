namespace Terra.Engine.Tests;

public class GameRulesTests
{
    private static SpeciesTraits Traits(
        int energy = 0, int speed = 0, int eyes = 0, int matureSize = 30) => new()
    {
        MaximumEnergyPoints = energy,
        MaximumSpeedPoints = speed,
        EatingSpeedPoints = 0,
        AttackDamagePoints = 0,
        DefendDamagePoints = 0,
        EyesightPoints = eyes,
        CamouflagePoints = 0,
        MatureSize = matureSize,
    };

    // ── MaxEnergy ────────────────────────────────────────────────────────
    [Fact]
    public void MaxEnergy_ZeroPoints_UsesBase()
    {
        // (19040 + 0) × 10 = 190,400
        Assert.Equal(190_400, GameRules.MaxEnergy(Traits(energy: 0), radius: 10));
    }

    [Fact]
    public void MaxEnergy_FullPoints_UsesBasePlusMaximum()
    {
        // (19040 + 380800) × 10 = 3,998,400
        Assert.Equal(3_998_400, GameRules.MaxEnergy(Traits(energy: 100), radius: 10));
    }

    // ── EnergyState classification ───────────────────────────────────────
    [Theory]
    [InlineData(0,        EnergyState.Dead)]
    [InlineData(1,        EnergyState.Deterioration)]   // ≤ 1×bucket
    [InlineData(38_080,   EnergyState.Deterioration)]   // exactly 1 bucket (190400/5)
    [InlineData(38_081,   EnergyState.Hungry)]
    [InlineData(76_160,   EnergyState.Hungry)]          // 2×bucket
    [InlineData(76_161,   EnergyState.Normal)]
    [InlineData(152_320,  EnergyState.Normal)]          // 4×bucket
    [InlineData(152_321,  EnergyState.Full)]
    [InlineData(190_400,  EnergyState.Full)]
    public void ClassifyEnergyState_FiveBuckets(double energy, EnergyState expected)
    {
        // Radius=10, energy=0 trait → MaxEnergy=190,400, bucket=38,080
        var state = GameRules.ClassifyEnergyState(energy, Traits(energy: 0), radius: 10);
        Assert.Equal(expected, state);
    }

    // ── LifeSpan ─────────────────────────────────────────────────────────
    [Fact]
    public void LifeSpan_Plant_Uses150Multiplier()
    {
        var sp = new Species("P", SpeciesKind.Plant, Traits(matureSize: 40));  // radius=20
        Assert.Equal(150 * 20, GameRules.LifeSpan(sp));
    }

    [Fact]
    public void LifeSpan_Herbivore_Uses50Multiplier()
    {
        var sp = new Species("H", SpeciesKind.Herbivore, Traits(matureSize: 40));
        Assert.Equal(50 * 20, GameRules.LifeSpan(sp));
    }

    [Fact]
    public void LifeSpan_Carnivore_IsTwiceHerbivore()
    {
        var sp = new Species("C", SpeciesKind.Carnivore, Traits(matureSize: 40));
        Assert.Equal(100 * 20, GameRules.LifeSpan(sp)); // 50 × 2 × 20
    }

    // ── MaxSpeed ─────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0, 5)]       // base
    [InlineData(100, 100)]   // maximum
    [InlineData(50, 52)]     // 5 + 0.5*95 = 52.5 → truncated to 52
    public void MaxSpeed_LinearInterpolation(int points, int expected)
    {
        Assert.Equal(expected, GameRules.MaxSpeed(Traits(speed: points)));
    }

    // ── EyesightRadiusPixels ─────────────────────────────────────────────
    [Theory]
    [InlineData(0, 40)]    // 5 cells × 8 px = 40
    [InlineData(100, 120)] // 15 cells × 8 px = 120
    public void EyesightRadiusPixels_LinearInterpolation(int points, int expected)
    {
        Assert.Equal(expected, GameRules.EyesightRadiusPixels(Traits(eyes: points)));
    }

    // ── MetabolismCost ───────────────────────────────────────────────────
    [Fact]
    public void MetabolismCost_Plant_IsRadius()
    {
        Assert.Equal(15, GameRules.MetabolismCost(SpeciesKind.Plant, 15));
    }

    [Fact]
    public void MetabolismCost_Animal_IsThousandthOfRadius()
    {
        Assert.Equal(0.015, GameRules.MetabolismCost(SpeciesKind.Herbivore, 15), precision: 10);
        Assert.Equal(0.015, GameRules.MetabolismCost(SpeciesKind.Carnivore, 15), precision: 10);
    }

    [Fact]
    public void PhotosynthesisGain_OnlyPlants()
    {
        Assert.Equal(550, GameRules.PhotosynthesisGain(SpeciesKind.Plant));
        Assert.Equal(0, GameRules.PhotosynthesisGain(SpeciesKind.Herbivore));
        Assert.Equal(0, GameRules.PhotosynthesisGain(SpeciesKind.Carnivore));
    }

    // ── MovementEnergyCost ───────────────────────────────────────────────
    [Fact]
    public void MovementEnergyCost_Formula()
    {
        // distance=10, radius=5, speed=20 → 10*5*20*0.005 = 5
        Assert.Equal(5.0, GameRules.MovementEnergyCost(radius: 5, distance: 10, speed: 20));
    }
}
