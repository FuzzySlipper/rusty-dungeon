using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>The player's visibility follows the torchlight where they stand; attacking gives them away.</summary>
public sealed class StealthTests
{
    [Fact]
    public void Visibility_is_the_light_underfoot_squared_and_an_attack_makes_it_full()
    {
        RunSession session = Hall(ScriptedRandom.Always(999_999), [new SnapshotSlot("test.item.sword", 1)], "test.item.sword");
        Assert.NotEmpty(session.Torches);

        session.Tick(RunInput.Idle);
        float light = LightLevel.At(session.Level, session.Torches, session.Player.Body.X, session.Player.Body.Y);
        Assert.Equal(Math.Min(1f, light * light), session.Player.Visibility, 4);
        Assert.Equal(3f + (15f * session.Player.Visibility), session.NoticeRadius, 4);

        session.Tick(RunInput.Idle with { AttackHeld = true });
        session.Tick(RunInput.Idle); // the release swings
        Assert.Equal(1f, session.Player.Visibility);
        Assert.Equal(18f, session.NoticeRadius);

        session.Tick(RunInput.Idle);
        Assert.Equal(Math.Min(1f, light * light), session.Player.Visibility, 4);
    }
}
