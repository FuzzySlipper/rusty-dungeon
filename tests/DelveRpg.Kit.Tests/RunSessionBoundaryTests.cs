using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

/// <summary>
/// Save-boundary and interaction regressions: what capture/restore must keep,
/// and what the run must refuse without crashing.
/// </summary>
public sealed class RunSessionBoundaryTests
{
    private static RunPlan Plan => new(
    [
        new FloorSpec(0, 1, "One", "Test", false),
        new FloorSpec(1, 2, "Two", "Test", false),
        new FloorSpec(2, 3, "Three", "Test", false),
    ]);

    private static RunSession Session() =>
        new(new ScriptedCatalog(), new GameTuning(), Plan, 4242UL, MetaProgression.Fresh, new ScriptedRandom());

    private static RunSession Restored(RunSnapshot snapshot) =>
        new(new ScriptedCatalog(), new GameTuning(), Plan, snapshot, MetaProgression.Fresh, new ScriptedRandom());

    /// <summary>A 5×5 room: walls on the border, an interior of floors and features.</summary>
    private static SnapshotFloor Room(
        int doorX = 0,
        int doorY = 0,
        int waterX = 0,
        int waterY = 0,
        int stairsDownX = 0,
        int stairsDownY = 0)
    {
        var tiles = new byte[25];
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                bool border = x == 0 || y == 0 || x == 4 || y == 4;
                tiles[(y * 5) + x] = (byte)(border ? TileKind.Wall : TileKind.Floor);
            }
        }

        tiles[(1 * 5) + 1] = (byte)TileKind.StairsUp;
        if (doorX != 0) tiles[(doorY * 5) + doorX] = (byte)TileKind.DoorClosed;
        if (waterX != 0) tiles[(waterY * 5) + waterX] = (byte)TileKind.Water;
        if (stairsDownX != 0) tiles[(stairsDownY * 5) + stairsDownX] = (byte)TileKind.StairsDown;

        var explored = new byte[25];
        Array.Fill(explored, (byte)1);
        return new SnapshotFloor(5, 5, 1, "Test", tiles, explored, 1, 1, 3, 3);
    }

    private static RunSnapshot Snapshot(SnapshotFloor floor, int runIndex = 0) =>
        Session().Capture() with
        {
            Floor = floor,
            RunIndex = runIndex,
            PlayerX = 1.5f,
            PlayerY = 2.5f,
            Facing = MathF.PI / 2f, // facing east
            Monsters = [],
            GroundItems = [],
            Slots = Enumerable.Repeat(new SnapshotSlot(null, 0), 24).ToList(),
        };

    [Fact]
    public void An_unknown_monster_id_is_skipped_without_corrupting_the_rest()
    {
        RunSnapshot snapshot = Snapshot(Room()) with
        {
            Monsters =
            [
                new SnapshotMonster("unknown.monster", 2.5f, 2.5f, 3, 0f),
                new SnapshotMonster("test.monster.rat", 3.5f, 2.5f, 4, 1f),
            ],
        };

        RunSession restored = Restored(snapshot);

        MonsterState monster = Assert.Single(restored.Monsters);
        Assert.Equal(3.5f, monster.Body.X);
        Assert.Equal(4, monster.Body.Hp);
    }

    [Fact]
    public void An_unknown_ground_item_is_skipped()
    {
        RunSnapshot snapshot = Snapshot(Room()) with
        {
            GroundItems =
            [
                new SnapshotGroundItem("unknown.item", 1, 2, 2),
                new SnapshotGroundItem("test.item.gold", 5, 3, 3),
            ],
        };

        RunSession restored = Restored(snapshot);

        GroundItem item = Assert.Single(restored.GroundItems);
        Assert.Equal(3, item.X);
        Assert.Equal(5, item.Item.Count);
    }

    [Fact]
    public void Descending_past_the_last_floor_is_refused_not_fatal()
    {
        RunSnapshot snapshot = Snapshot(Room(stairsDownX: 2, stairsDownY: 2), runIndex: 2);
        RunSession restored = Restored(snapshot);

        restored.Tick(RunInput.Idle with { UsePressed = true });

        Assert.Equal(2, restored.RunIndex);
        Assert.Contains(restored.Messages, message => message.Text.Contains("nowhere deeper", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_run_index_outside_the_rebuilt_plan_is_refused_at_restore()
    {
        RunSnapshot snapshot = Snapshot(Room(), runIndex: 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => Restored(snapshot));
    }

    [Fact]
    public void A_pending_level_up_offer_survives_the_save_boundary()
    {
        RunSnapshot snapshot = Snapshot(Room()) with
        {
            Phase = RunPhase.LevelUp,
            LevelUpOffers = ["attack", "defense", "endurance"],
            LevelUpCursor = 1,
            Experience = 10,
            PlayerLevel = 1,
        };

        RunSession restored = Restored(snapshot);

        Assert.Equal(RunPhase.LevelUp, restored.Phase);
        Assert.Equal(["attack", "defense", "endurance"], restored.LevelUpOffers);
        Assert.Equal(1, restored.LevelUpCursor);

        restored.Tick(RunInput.Idle with { MenuConfirm = true });
        Assert.Equal(RunPhase.Playing, restored.Phase);
        Assert.Equal(3, restored.Player.Body.Stats.Defense); // defense 2 + 1
    }

    [Fact]
    public void Slot_positions_and_the_wielded_slot_survive_restore()
    {
        SnapshotSlot[] slots = Enumerable.Repeat(new SnapshotSlot(null, 0), 24).ToArray();
        slots[0] = new SnapshotSlot("test.item.potion", 3);
        slots[5] = new SnapshotSlot("test.item.sword", 1);
        slots[6] = new SnapshotSlot("test.item.potion", 2);
        RunSnapshot snapshot = Snapshot(Room()) with
        {
            Slots = slots,
            WieldedSlot = 5,
            WeaponItemId = "test.item.sword",
        };

        RunSession restored = Restored(snapshot);

        Assert.Equal(3, restored.Player.Inventory.Slot(0)?.Count);
        Assert.Equal("test.item.sword", restored.Player.Inventory.Slot(5)?.ArchetypeId);
        Assert.Equal(2, restored.Player.Inventory.Slot(6)?.Count);
        Assert.Equal(5, restored.Player.WieldedSlot);
    }

    [Fact]
    public void Hit_points_clamp_on_restore()
    {
        RunSnapshot snapshot = Snapshot(Room()) with { Hp = 9999, MaxHp = 10 };
        RunSession restored = Restored(snapshot);
        Assert.Equal(10, restored.Player.Body.Hp);
    }

    [Fact]
    public void Movement_is_relative_to_the_look_direction()
    {
        // Facing east (pi/2): forward walks +x — the donor's FPS walk, not a
        // world-axis grid step.
        RunSession east = Restored(Snapshot(Room()) with { Facing = MathF.PI / 2f });
        for (int i = 0; i < 30; i++)
        {
            east.Tick(RunInput.Idle with { MoveY = 1f });
        }

        Assert.True(east.Player.Body.X > 2.2f, $"forward did not walk east (x {east.Player.Body.X})");
        Assert.True(MathF.Abs(east.Player.Body.Y - 2.5f) < 0.05f, "forward drifted off the look axis");

        // Facing north (0): forward walks -y.
        RunSession north = Restored(Snapshot(Room()) with { Facing = 0f });
        for (int i = 0; i < 30; i++)
        {
            north.Tick(RunInput.Idle with { MoveY = 1f });
        }

        Assert.True(north.Player.Body.Y < 1.8f, $"forward did not walk north (y {north.Player.Body.Y})");
        Assert.True(MathF.Abs(north.Player.Body.X - 1.5f) < 0.05f, "forward drifted off the look axis");

        // Strafe right while looking north walks +x.
        RunSession strafe = Restored(Snapshot(Room()) with { Facing = 0f });
        for (int i = 0; i < 30; i++)
        {
            strafe.Tick(RunInput.Idle with { MoveX = 1f });
        }

        Assert.True(strafe.Player.Body.X > 2.2f, $"strafe did not walk east (x {strafe.Player.Body.X})");
        Assert.True(MathF.Abs(strafe.Player.Body.Y - 2.5f) < 0.05f, "strafe drifted off the look axis");
    }

    [Fact]
    public void A_full_pack_nags_once_per_item_not_once_per_tick()
    {
        SnapshotSlot[] slots = Enumerable.Repeat(new SnapshotSlot("test.item.sword", 1), 24).ToArray();
        RunSnapshot snapshot = Snapshot(Room()) with
        {
            Slots = slots,
            GroundItems = [new SnapshotGroundItem("test.item.sword", 1, 2, 2)],
        };

        RunSession restored = Restored(snapshot);
        for (int i = 0; i < 30; i++)
        {
            restored.Tick(RunInput.Idle with { MoveY = 1f });
        }

        int nags = restored.Messages.Count(message => message.Text.Contains("pack is full", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, nags);
    }

    [Fact]
    public void Using_a_door_opens_it()
    {
        RunSnapshot snapshot = Snapshot(Room(doorX: 2, doorY: 2));
        RunSession restored = Restored(snapshot);

        restored.Tick(RunInput.Idle with { UsePressed = true });

        Assert.Equal(TileKind.DoorOpen, restored.Level.At(2, 2).Kind);
        Assert.Contains(restored.Messages, message => message.Text.Contains("open the door", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Opening_a_door_marks_the_scene_stale()
    {
        // Presentation rebuilds only when LevelRevision moves; a door opening
        // that misses it leaves a phantom wall box standing in the doorway.
        RunSnapshot snapshot = Snapshot(Room(doorX: 2, doorY: 2));
        RunSession restored = Restored(snapshot);
        ulong before = restored.LevelRevision;

        restored.Tick(RunInput.Idle with { UsePressed = true });

        Assert.Equal(TileKind.DoorOpen, restored.Level.At(2, 2).Kind);
        Assert.True(restored.LevelRevision > before, "a door opening must move the scene revision");
    }

    [Fact]
    public void The_minimap_covers_every_documented_glyph()
    {
        RunSnapshot snapshot = Snapshot(Room(doorX: 2, doorY: 2, waterX: 3, waterY: 2, stairsDownX: 3, stairsDownY: 3));
        RunSession restored = Restored(snapshot);

        // One tile stays unexplored: the far corner behind the border wall.
        RunSnapshot unexplored = Snapshot(Room()) with
        {
            Floor = Room() with
            {
                Explored = Room().Explored.Select((value, index) => index == 0 ? (byte)0 : value).ToArray(),
            },
        };
        RunSession explored = Restored(unexplored);

        HudFacts facts = restored.BuildHudFacts();
        string cells = facts.Minimap.Cells;

        Assert.Contains(' ', explored.BuildHudFacts().Minimap.Cells);
        Assert.Contains('@', cells);
        Assert.Contains('#', cells); // explored wall
        Assert.Contains('.', cells); // explored floor
        Assert.Contains('+', cells); // door
        Assert.Contains('~', cells); // water
        Assert.Contains('<', cells); // stairs up
        Assert.Contains('>', cells); // stairs down
    }

    [Fact]
    public void An_older_save_shape_without_offers_restores_tolerantly()
    {
        // A pre-offer save decodes with null offers; restore must not crash.
        RunSnapshot snapshot = Snapshot(Room()) with
        {
            LevelUpOffers = null!,
            LevelUpCursor = 3,
        };

        RunSession restored = Restored(snapshot);

        Assert.Empty(restored.LevelUpOffers);
        Assert.Equal(0, restored.LevelUpCursor);
        Assert.Equal(RunPhase.Playing, restored.Phase);
    }

    [Fact]
    public void An_out_of_range_wielded_slot_resets()
    {
        RunSnapshot high = Snapshot(Room()) with { WieldedSlot = 99 };
        Assert.Equal(-1, Restored(high).Player.WieldedSlot);

        RunSnapshot low = Snapshot(Room()) with { WieldedSlot = -5 };
        Assert.Equal(-1, Restored(low).Player.WieldedSlot);
    }

    [Fact]
    public void The_save_boundary_strips_transient_combat_state()
    {
        // Recorded divergence (docs/gameplay-design.md §Saves): charge, cooldowns,
        // and effects do not survive a save boundary, like the donor's preSaveCleanup.
        RunSession session = Session();
        session.Player.AttackCharge = 7;
        session.Player.Body.AttackCooldownRemaining = 11;
        session.Player.Body.Effects.Apply(Kit.Effects.EffectKind.Poison, 100, 1);
        RunSnapshot snapshot = session.Capture();

        RunSession restored = Restored(snapshot);

        Assert.Equal(0, restored.Player.AttackCharge);
        Assert.Equal(0, restored.Player.Body.AttackCooldownRemaining);
        Assert.Empty(restored.Player.Body.Effects.Active);
    }

    [Fact]
    public void The_escape_arc_first_spawn_lands_on_the_documented_cadence()
    {
        // Default tuning: first pursuit spawn after 600 ticks of holding the orb.
        RunSnapshot snapshot = Snapshot(Room()) with { HoldingOrb = true, EscapePressureTicks = 0 };
        RunSession restored = Restored(snapshot);

        for (int i = 0; i < 599; i++)
        {
            restored.Tick(RunInput.Idle);
        }

        Assert.Empty(restored.Monsters);

        restored.Tick(RunInput.Idle);
        Assert.NotEmpty(restored.Monsters);
    }
}
