using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>Locked doors, spikes, plates, tripwires and pots in play.</summary>
public sealed class FloorFeatureTests
{
    private static void Walk(RunSession session, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            session.Tick(RunInput.Idle with { MoveY = 1f }); // facing east: forward
        }
    }

    [Fact]
    public void A_locked_door_refuses_without_a_key_and_takes_one_to_open()
    {
        RunSession locked = Hall(ScriptedRandom.Always(0), [], null, lockedDoorX: 2);
        locked.Tick(RunInput.Idle with { UsePressed = true });
        Assert.Equal(TileKind.DoorLocked, locked.Level.At(2, 1).Kind);
        Assert.Contains(locked.Messages, message => message.Text.Contains("locked"));

        RunSession keyed = Hall(ScriptedRandom.Always(0), [], null, lockedDoorX: 2, keys: 1);
        keyed.Tick(RunInput.Idle with { UsePressed = true });
        Assert.Equal(TileKind.DoorOpen, keyed.Level.At(2, 1).Kind);
        Assert.Equal(0, keyed.Player.Keys);
    }

    [Fact]
    public void Spikes_spring_when_walked_onto_and_strike_once_at_full_extension()
    {
        RunSession session = Hall(ScriptedRandom.Always(0), [], null, features: [new SnapshotFeature("spikes", 2.5f, 1.5f)]);
        int hp = session.Player.Body.Hp;

        Walk(session, 18); // onto tile 2 and stop there
        for (int i = 0; i < 40; i++)
        {
            session.Tick(RunInput.Idle);
        }

        Assert.Equal(hp - SpikeTrap.Damage, session.Player.Body.Hp);
        Assert.Contains(session.Messages, message => message.Text.Contains("Spikes"));
    }

    [Fact]
    public void A_pressure_plate_sets_off_the_fire_trap_its_id_names()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            features:
            [
                new SnapshotFeature("trigger", 2.5f, 1.5f, "plate:2,1", IsPlate: true),
                new SnapshotFeature("effect", 2.5f, 1.5f, "plate:2,1", (int)TrapEffectKind.FireBurst),
            ]);
        int hp = session.Player.Body.Hp;

        Walk(session, 20);

        Assert.Equal(hp - 6, session.Player.Body.Hp); // 6 + half of dungeon level 1
        Assert.NotEmpty(session.Bursts);
    }

    [Fact]
    public void A_hidden_tripwire_makes_the_wall_shoot_a_bolt_and_waits_out_its_reset()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [],
            null,
            features:
            [
                new SnapshotFeature("trigger", 2.5f, 1.5f, "bolt:2,1", ResetTicks: 200),
                new SnapshotFeature("effect", 12.95f, 1.5f, "bolt:2,1", (int)TrapEffectKind.WallBolt),
            ]);

        Walk(session, 20);

        Assert.Contains(session.Messages, message => message.Text.Contains("from the wall"));
        Assert.Equal(1, session.Messages.Count(message => message.Text.Contains("from the wall")));
    }

    [Fact]
    public void A_pot_blocks_the_way_and_breaks_under_a_swing()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999), // no surprise
            [new SnapshotSlot("test.item.sword", 1)],
            "test.item.sword",
            features: [new SnapshotFeature("pot", 2.5f, 1.5f, Variant: (int)PotKind.Fragile, Hp: 1)]);

        Walk(session, 30);
        Assert.True(session.Player.Body.X < 2.5f - 0.4f, $"walked into the pot at {session.Player.Body.X}");

        HoldThenRelease(session, 10);
        Assert.Empty(session.Pots);
        Assert.Contains(session.Messages, message => message.Text.Contains("shatters"));
    }

    [Fact]
    public void Floor_features_survive_the_save_boundary()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [],
            null,
            features:
            [
                new SnapshotFeature("spikes", 5.5f, 1.5f),
                new SnapshotFeature("trigger", 7.5f, 1.5f, "plate:7,1", IsPlate: true),
                new SnapshotFeature("effect", 7.5f, 1.5f, "plate:7,1", (int)TrapEffectKind.PoisonBurst),
                new SnapshotFeature("pot", 9.5f, 1.5f, Variant: (int)PotKind.Sturdy, Hp: 2),
            ]);

        RunSnapshot snapshot = session.Capture();

        Assert.Equal(4, snapshot.Features!.Count);
        Assert.Contains(snapshot.Features, feature => feature is { Kind: "pot", Hp: 2 });
    }
}
