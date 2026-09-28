using DelveRpg.Kit.Rules;
using Xunit;

namespace DelveRpg.Kit.Tests;

/// <summary>Pins the donor-derived defaults so tuning drift is a visible change.</summary>
public sealed class TuningDefaultsTests
{
    private static readonly GameTuning Tuning = new();

    [Fact]
    public void Combat_defaults_match_the_donor()
    {
        Assert.Equal(0.15f, Tuning.DodgeChance);       // flat dodge before the damage roll
        Assert.Equal(40, Tuning.AttackChargeTicks);    // donor attackChargeTime
        Assert.Equal(0.25f, Tuning.FleeHpFraction);    // flee below 25% hp
    }

    [Fact]
    public void Escape_arc_defaults_match_the_donor_curve()
    {
        Assert.Equal(600, Tuning.EscapeSpawnCadenceStartTicks);
        Assert.Equal(60, Tuning.EscapeSpawnCadenceEndTicks);
        Assert.Equal(3, Tuning.EscapeSpawnGroupStart);
        Assert.Equal(15, Tuning.EscapeSpawnGroupEnd);
    }

    [Fact]
    public void Economy_defaults_match_the_donor()
    {
        Assert.Equal(40, Tuning.StartingGold);
        Assert.Equal(0.2f, Tuning.EnchantChance);
    }
}
