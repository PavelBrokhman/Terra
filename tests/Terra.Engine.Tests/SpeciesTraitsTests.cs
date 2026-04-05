namespace Terra.Engine.Tests;

public class SpeciesTraitsTests
{
    private static SpeciesTraits Valid() => new()
    {
        MaximumEnergyPoints = 20,
        MaximumSpeedPoints = 20,
        EatingSpeedPoints = 20,
        AttackDamagePoints = 10,
        DefendDamagePoints = 10,
        EyesightPoints = 10,
        CamouflagePoints = 10,
        MatureSize = 32,
    };

    [Fact]
    public void Validate_AllowsPointBudgetUpToExactly100()
    {
        var traits = Valid();
        Assert.Equal(100, traits.TotalPoints);
        traits.Validate(); // should not throw
    }

    [Fact]
    public void Validate_RejectsOverBudget()
    {
        var traits = Valid() with { MaximumEnergyPoints = 21 };
        Assert.Equal(101, traits.TotalPoints);
        Assert.Throws<InvalidOperationException>(traits.Validate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_RejectsOutOfRangeTraitValue(int bad)
    {
        var traits = Valid() with { MaximumEnergyPoints = bad, MaximumSpeedPoints = 0 };
        Assert.Throws<ArgumentOutOfRangeException>(traits.Validate);
    }

    [Theory]
    [InlineData(24)] // below MinMatureSize=25
    [InlineData(49)] // above MaxMatureSize=48
    public void Validate_RejectsMatureSizeOutOfRange(int bad)
    {
        var traits = Valid() with { MatureSize = bad };
        Assert.Throws<ArgumentOutOfRangeException>(traits.Validate);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(48)]
    public void Validate_AcceptsMatureSizeAtBoundaries(int ok)
    {
        var traits = Valid() with { MatureSize = ok };
        traits.Validate(); // should not throw
    }

    [Fact]
    public void Species_MatureRadius_IsHalfOfMatureSize()
    {
        var species = new Species("Oak", SpeciesKind.Plant, Valid() with { MatureSize = 40 });
        Assert.Equal(20, species.MatureRadius);
    }
}
