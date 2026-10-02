using DelveRpg.Kit.Session;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>The tick's cues: what happened, for the Host to voice.</summary>
public sealed class CueTests
{
    private static List<RunCue> TickCollecting(RunSession session, RunInput input, int ticks)
    {
        var cues = new List<RunCue>();
        for (int i = 0; i < ticks; i++)
        {
            session.Tick(input);
            cues.AddRange(session.Cues);
        }

        return cues;
    }

    [Fact]
    public void Walking_steps_every_25_ticks_and_standing_is_silent()
    {
        RunSession session = Hall(ScriptedRandom.Always(999_999), [], null);

        List<RunCue> standing = TickCollecting(session, RunInput.Idle, 60);
        List<RunCue> walking = TickCollecting(session, RunInput.Idle with { MoveY = 1f }, 100);

        Assert.DoesNotContain(standing, cue => cue.Kind == CueKind.Footstep);
        int steps = walking.Count(cue => cue.Kind == CueKind.Footstep);
        Assert.InRange(steps, 3, 4);
    }

    [Fact]
    public void Cues_last_one_tick()
    {
        RunSession session = Hall(ScriptedRandom.Always(999_999), [], null);
        TickCollecting(session, RunInput.Idle with { MoveY = 1f }, 30);
        session.Tick(RunInput.Idle);
        session.Tick(RunInput.Idle);

        Assert.Empty(session.Cues);
    }

    [Fact]
    public void A_swing_that_lands_cues_the_swing_and_the_monsters_hurt()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [new SnapshotSlot("test.item.sword", 1)],
            "test.item.sword",
            [new SnapshotMonster("test.monster.slime", 2.3f, 1.5f, 30, 0f)],
            playerX: 1.5f);

        List<RunCue> cues = TickCollecting(session, RunInput.Idle with { AttackHeld = true }, 40);
        cues.AddRange(TickCollecting(session, RunInput.Idle, 40));

        RunCue swing = Assert.Single(cues, cue => cue.Kind == CueKind.Swing);
        Assert.Equal("test.item.sword", swing.Subject);
        RunCue hurt = Assert.Single(cues, cue => cue.Kind == CueKind.MonsterHurt);
        Assert.Equal("test.monster.slime", hurt.Subject);
        Assert.Contains(cues, cue => cue.Kind == CueKind.MonsterAlert && cue.Subject == "test.monster.slime");
    }

    [Fact]
    public void A_dry_bow_fizzles_instead_of_shooting()
    {
        RunSession session = Hall(new ScriptedRandom(), [new SnapshotSlot("test.item.bow", 1)], "test.item.bow");

        List<RunCue> cues = TickCollecting(session, RunInput.Idle with { AttackHeld = true }, 36);
        cues.AddRange(TickCollecting(session, RunInput.Idle, 2));

        Assert.Contains(cues, cue => cue.Kind == CueKind.Fizzle && cue.Subject == "test.item.bow");
        Assert.DoesNotContain(cues, cue => cue.Kind == CueKind.Bow);
    }
}
