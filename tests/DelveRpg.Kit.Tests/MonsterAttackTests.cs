using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Session;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>Monster blows wind up, land on the damage frame, and miss a player who stepped away.</summary>
public sealed class MonsterAttackTests
{
    [Fact]
    public void A_blow_lands_only_when_the_wind_up_reaches_its_damage_frame()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            [new SnapshotMonster("test.monster.slime", 4.5f, 1.5f, 6, 0f)],
            playerX: 3.7f);
        int hp = session.Player.Body.Hp;

        session.Tick(RunInput.Idle); // alerted
        session.Tick(RunInput.Idle); // in reach: the wind-up starts
        MonsterState slime = Assert.Single(session.Monsters);
        Assert.True(slime.AttackWindupRemaining > 0);
        float standing = slime.Body.X;

        Idle(session, 26);
        Assert.Equal(hp, session.Player.Body.Hp);
        Assert.Equal(standing, slime.Body.X); // it stands while it winds up

        Idle(session, 2);
        Assert.True(session.Player.Body.Hp < hp);
        Assert.Equal(0, slime.AttackWindupRemaining);
        // The next blow waits out attackTime plus the 0-9 jitter (high draw: 9).
        Assert.InRange(slime.Body.AttackCooldownRemaining, 40 + 9 - 28, 40 + 9 - 27);
    }

    [Fact]
    public void Stepping_back_out_of_reach_dodges_the_blow()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            [new SnapshotMonster("test.monster.slime", 4.5f, 1.5f, 6, 0f)],
            playerX: 3.7f);
        int hp = session.Player.Body.Hp;

        session.Tick(RunInput.Idle);
        session.Tick(RunInput.Idle);
        // Facing east, so backing off is MoveY = -1.
        for (int i = 0; i < 28; i++)
        {
            session.Tick(RunInput.Idle with { MoveY = -1f });
        }

        Assert.Equal(hp, session.Player.Body.Hp);
        Assert.Contains(session.Messages, message => message.Text.Contains("falls short"));
    }

    [Fact]
    public void A_flinch_spoils_the_blow_being_wound_up()
    {
        // Low draws pass the pain roll.
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [new SnapshotSlot("test.item.sword", 1)],
            "test.item.sword",
            [new SnapshotMonster("test.monster.slime", 4.5f, 1.5f, 6, 0f)],
            playerX: 3.7f);

        session.Tick(RunInput.Idle with { AttackHeld = true });
        session.Tick(RunInput.Idle with { AttackHeld = true });
        MonsterState slime = Assert.Single(session.Monsters);
        Assert.True(slime.AttackWindupRemaining > 0);

        session.Tick(RunInput.Idle); // release: the swing lands, the slime flinches

        Assert.Equal(0, slime.AttackWindupRemaining);
        Assert.True(slime.HurtTicksRemaining > 0);
    }

    [Fact]
    public void A_lunging_attacker_starts_from_further_off_and_closes_in()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            [new SnapshotMonster("test.monster.lunger", 5.4f, 1.5f, 6, 0f)],
            playerX: 3.7f);

        session.Tick(RunInput.Idle);
        session.Tick(RunInput.Idle);
        MonsterState lunger = Assert.Single(session.Monsters);
        Assert.True(lunger.AttackWindupRemaining > 0); // 1.7 off is inside 0.3 + 1.75

        Idle(session, 30);
        Assert.True(lunger.Body.X < 5.3f, $"the lunge left it at {lunger.Body.X}");
        Assert.True(session.Player.Body.X - lunger.Body.X <= 0f || lunger.Body.X - session.Player.Body.X >= 0.74f);
    }
}
