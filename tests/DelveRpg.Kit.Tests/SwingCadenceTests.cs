using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Session;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>Swings take their donor time: the blow lands partway through, and the next waits on the swing.</summary>
public sealed class SwingCadenceTests
{
    private static RunSession DaggerHall() => Hall(
        ScriptedRandom.Always(999_999),
        [new SnapshotSlot("test.item.dagger", 1)],
        "test.item.dagger",
        [new SnapshotMonster("test.monster.rat", 2.5f, 1.5f, 6, 0f)]);

    [Fact]
    public void A_quick_swing_lands_ten_ticks_in_and_the_next_waits_three_quarters_of_it()
    {
        // Starting DEX is 4, so playback is speed × 0.25 = 0.25: the quick
        // dagger swing lasts 7.5 / 0.25 = 30 ticks and its blow lands at
        // 5 / 0.25 × 0.5 = 10.
        RunSession session = DaggerHall();
        HoldThenRelease(session, 10); // a quarter charge: the quick swing

        SwingState swing = Assert.IsType<SwingState>(session.Player.Swing);
        Assert.False(swing.Strong);
        Assert.Equal(30, swing.LengthTicks);
        Assert.Equal(10, swing.HitAtTicks);
        Assert.Equal(22, session.Player.Body.AttackCooldownRemaining); // 30 × 0.75 = 22.5, rounded to even
        int hp = session.Monsters[0].Body.Hp;

        Idle(session, 8);
        Assert.Equal(hp, session.Monsters[0].Body.Hp);
        Idle(session, 2);
        Assert.True(session.Monsters[0].Body.Hp < hp);
    }

    [Fact]
    public void Half_charge_or_more_plays_the_full_swing()
    {
        RunSession session = DaggerHall();
        HoldThenRelease(session, 20);

        SwingState swing = Assert.IsType<SwingState>(session.Player.Swing);
        Assert.True(swing.Strong);
        Assert.Equal(45, swing.LengthTicks); // 11.25 / 0.25
        Assert.Equal(13, swing.HitAtTicks);  // 6.5 / 0.25 × 0.5
    }

    [Fact]
    public void An_empty_hand_neither_charges_nor_swings()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            [new SnapshotMonster("test.monster.rat", 2.5f, 1.5f, 6, 0f)]);

        session.Tick(RunInput.Idle with { AttackHeld = true });
        Assert.Equal(0, session.Player.AttackCharge);
        HoldThenRelease(session, 20);
        Assert.Null(session.Player.Swing);
        Assert.Equal(6, session.Monsters[0].Body.Hp);
    }
}
