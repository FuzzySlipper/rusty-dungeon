using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Session;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>Knockback, stun, flinch, the death stagger, corpses and the hurt flash.</summary>
public sealed class HitFeedbackTests
{
    private static readonly SnapshotSlot[] Sword = [new SnapshotSlot("test.item.sword", 1)];

    [Fact]
    public void A_full_swing_shoves_the_monster_back_and_it_flinches()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            Sword,
            "test.item.sword",
            [new SnapshotMonster("test.monster.rat", 2.5f, 1.5f, 6, 0f)]);

        HoldThenRelease(session, 30);

        MonsterState rat = Assert.Single(session.Monsters);
        Assert.Equal(2, rat.Body.Hp); // sword 4, nothing random on top
        // Knockback min(0.4 × 1.1, 0.6) = 0.44 along the facing times the 0.5 reach.
        // The same tick already slid it once and took a fifth off: 0.22 × 0.8.
        Assert.Equal(0.176f, rat.VelocityX, 3);
        Assert.True(rat.StunTicksRemaining > 0);
        Assert.True(rat.HurtTicksRemaining > 0);

        Idle(session, 10);
        Assert.True(rat.Body.X > 2.9f, $"the rat slid to {rat.Body.X}");
        Assert.True(MathF.Abs(rat.VelocityX) < 0.05f);
    }

    [Fact]
    public void A_slain_monster_staggers_for_the_death_delay_then_falls_as_a_corpse()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            Sword,
            "test.item.sword",
            [new SnapshotMonster("test.monster.rat", 2.5f, 1.5f, 3, 0f)]);
        int experience = session.Player.Experience;

        HoldThenRelease(session, 30);

        MonsterState dying = Assert.Single(session.Monsters);
        Assert.True(dying.IsDying);
        Assert.Empty(session.Corpses);
        Assert.Equal(experience, session.Player.Experience);

        Idle(session, RunSession.DeathDelayTicks);

        Assert.Empty(session.Monsters);
        Corpse corpse = Assert.Single(session.Corpses);
        Assert.Equal("monster.rat", corpse.SpriteId);
        Assert.True(session.Player.Experience > experience);
    }

    [Fact]
    public void A_landed_blow_shoves_the_player_and_flashes_the_screen()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            [new SnapshotMonster("test.monster.rat", 4.5f, 1.5f, 6, 0f)],
            playerX: 3.7f);
        int hp = session.Player.Body.Hp;

        session.Tick(RunInput.Idle);

        Assert.True(session.Player.Body.Hp < hp);
        Assert.Equal(RunSession.HurtFlashTicks, session.Player.HurtFlashRemaining);
        Idle(session, 3);
        Assert.True(session.Player.Body.X < 3.7f, $"player at {session.Player.Body.X}");
        Idle(session, RunSession.HurtFlashTicks);
        Assert.Equal(0, session.Player.HurtFlashRemaining); // the rat's next blow is 40 ticks off
    }

    [Fact]
    public void Burning_damage_flashes_the_screen_too()
    {
        RunSession session = Hall(ScriptedRandom.Always(999_999), [], null);
        session.Player.Body.Effects.Apply(EffectKind.Burning, 600, 1, intervalTicks: 2);

        Idle(session, 2);

        Assert.True(session.Player.HurtFlashRemaining > 0);
    }
}
