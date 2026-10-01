using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class RunSessionTests
{
    private static GameTuning Tuning => new() { EscapeSpawnCadenceStartTicks = 5, EscapeSpawnCadenceEndTicks = 2 };

    private static RunPlan Plan => new(
    [
        new FloorSpec(0, 1, "One", "Test", false),
        new FloorSpec(1, 2, "Two", "Test", false),
        new FloorSpec(2, 3, "Three", "Test", false),
    ]);

    private static RunSession Session(Kit.Random.IRandomSource? draws = null) =>
        new(new ScriptedCatalog(), Tuning, Plan, 1234UL, MetaProgression.Fresh, draws ?? new ScriptedRandom());

    private static RunSession Restored(RunSnapshot snapshot, Kit.Random.IRandomSource? draws = null) =>
        new(new ScriptedCatalog(), Tuning, Plan, snapshot, MetaProgression.Fresh, draws ?? new ScriptedRandom());

    [Fact]
    public void A_fresh_character_starts_with_full_hit_points()
    {
        RunSession session = Session();
        // Donor-derived starting durability: maxHp from the starting stats at
        // level 1, and the bar starts full. Starting at 1 hp is a death spiral.
        Assert.Equal(
            Kit.Progression.LevelProgression.MaxHitPoints(session.Player.Body.Stats, 1),
            session.Player.Body.MaxHp);
        Assert.Equal(session.Player.Body.MaxHp, session.Player.Body.Hp);
        Assert.True(session.Player.Body.MaxHp >= 10);
    }

    [Fact]
    public void Held_movement_walks_the_player_across_the_floor()
    {
        RunSession session = Session();
        float startX = session.Player.Body.X;
        float startY = session.Player.Body.Y;

        for (int i = 0; i < 60; i++)
        {
            session.Tick(RunInput.Idle with { MoveY = 1f });
        }

        float moved = MathF.Abs(session.Player.Body.X - startX) + MathF.Abs(session.Player.Body.Y - startY);
        Assert.True(moved > 0.2f, $"player did not walk (moved {moved})");
        Assert.True(session.Level.IsWalkable(session.Player.Body.TileX, session.Player.Body.TileY));
    }

    [Fact]
    public void The_starting_kit_is_carried_and_the_first_weapon_is_wielded()
    {
        RunSession session = Session();
        session.GiveStartingKit(["test.item.potion", "test.item.sword"]);

        Assert.Equal("test.item.sword", session.Player.Equipment.WeaponItemId);
        Assert.Equal(1, session.Player.WieldedSlot);
        Assert.Equal("test.item.potion", session.Player.Inventory.Slot(0)?.ArchetypeId);
    }

    [Fact]
    public void Walking_into_a_monster_stops_at_the_separation_distance()
    {
        RunSnapshot start = Session().Capture();
        RunSession probe = Restored(start);
        float px = probe.Player.Body.X;
        float py = probe.Player.Body.Y;

        // Pick a heading with open floor ahead, and put a monster two tiles out.
        float facing = 0f;
        bool found = false;
        for (int quarter = 0; quarter < 4 && !found; quarter++)
        {
            facing = quarter * (MathF.PI / 2f);
            found = true;
            for (float step = 0.5f; step <= 2.5f; step += 0.5f)
            {
                int tx = (int)MathF.Floor(px + (MathF.Sin(facing) * step));
                int ty = (int)MathF.Floor(py - (MathF.Cos(facing) * step));
                found &= probe.Level.IsWalkable(tx, ty);
            }
        }

        Assert.True(found, "entrance has no straight run of open floor");
        float mx = px + (MathF.Sin(facing) * 2f);
        float my = py - (MathF.Cos(facing) * 2f);
        RunSession session = Restored(start with
        {
            Facing = facing,
            Monsters = [new SnapshotMonster("test.monster.rat", mx, my, 6, 0f)],
            GroundItems = [],
        });

        float closest = float.MaxValue;
        for (int tick = 0; tick < 180; tick++)
        {
            session.Tick(RunInput.Idle with { MoveY = 1f });
            Kit.Actors.ActorState rat = session.Monsters[0].Body;
            float dx = rat.X - session.Player.Body.X;
            float dy = rat.Y - session.Player.Body.Y;
            closest = MathF.Min(closest, MathF.Sqrt((dx * dx) + (dy * dy)));
        }

        Assert.InRange(closest, Tuning.ActorSeparationTiles - 0.05f, Tuning.ActorSeparationTiles + 0.1f);
    }

    [Fact]
    public void Using_the_stairs_down_moves_the_run_one_floor_deeper()
    {
        RunSession session = Session();
        (int stairsX, int stairsY) = FindTile(session, Kit.World.TileKind.StairsDown);
        StandBefore(session, stairsX, stairsY);

        session.Tick(RunInput.Idle with { UsePressed = true });

        Assert.Equal(1, session.RunIndex);
        Assert.Equal(2, session.Level.DifficultyLevel);
    }

    [Fact]
    public void Leaving_without_the_objective_is_refused()
    {
        RunSession session = Session();
        (int startX, int startY) = FindTile(session, Kit.World.TileKind.StairsUp);
        StandBefore(session, startX, startY);

        session.Tick(RunInput.Idle with { UsePressed = true });

        Assert.Equal(0, session.RunIndex);
        Assert.Equal(RunPhase.Playing, session.Phase);
        Assert.Contains(session.Messages, message => message.Text.Contains("cannot leave", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Walking_over_an_item_picks_it_up()
    {
        RunSession session = Session();
        RunSnapshot snapshot = session.Capture() with
        {
            Gold = 0,
            GroundItems = [new SnapshotGroundItem("test.item.gold", 1, (int)session.Player.Body.X, (int)session.Player.Body.Y)],
        };

        RunSession restored = Restored(snapshot);
        restored.Tick(RunInput.Idle with { MoveX = 1f });

        Assert.Empty(restored.GroundItems);
        Assert.Equal(25, restored.Player.Gold);
    }

    [Fact]
    public void A_monster_next_to_the_player_attacks()
    {
        RunSession session = Session();
        RunSnapshot snapshot = session.Capture() with
        {
            Monsters = [new SnapshotMonster("test.monster.rat", session.Player.Body.X + 1f, session.Player.Body.Y, 6, 0f)],
        };

        // Seeded draws: no dodge, then the attack roll — a hit is deterministic.
        RunSession restored = Restored(snapshot, new ScriptedRandom(999_999, 1));
        restored.Tick(RunInput.Idle);

        Assert.True(restored.Player.Body.Hp < restored.Player.Body.MaxHp, "the hit did not land");
        Assert.Contains(restored.Messages, message => message.Text.Contains("hits you", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Earning_enough_experience_offers_a_level_up_choice()
    {
        RunSession session = Session();
        // Cumulative thresholds: level 2 needs 8 total xp, level 3 needs 32.
        RunSnapshot snapshot = session.Capture() with { Experience = 10, PlayerLevel = 1 };

        RunSession restored = Restored(snapshot);
        restored.Tick(RunInput.Idle);

        Assert.Equal(RunPhase.LevelUp, restored.Phase);
        Assert.Equal(3, restored.LevelUpOffers.Count);
        // ScriptedRandom's zero fallback makes the offer shuffle deterministic:
        // offers are [defense, dexterity, speed], so the cursor lands on 1.

        restored.Tick(RunInput.Idle with { MenuDown = true, MenuConfirm = true });
        Assert.Equal(RunPhase.Playing, restored.Phase);
        Assert.Equal(2, restored.Player.Level);
        Assert.Equal(restored.Player.Body.MaxHp, restored.Player.Body.Hp);
        Assert.Equal(5, restored.Player.Body.Stats.Dexterity); // offer[1] "dexterity" was raised from 4
    }

    [Fact]
    public void Holding_the_objective_brings_pursuit()
    {
        RunSession session = Session();
        RunSnapshot snapshot = session.Capture() with { HoldingOrb = true };
        RunSession restored = Restored(snapshot);
        int before = restored.Monsters.Count;

        for (int i = 0; i < 12; i++)
        {
            restored.Tick(RunInput.Idle);
        }

        Assert.True(restored.Monsters.Count > before);
    }

    [Fact]
    public void Capture_and_restore_preserves_the_run()
    {
        RunSession session = Session();
        for (int i = 0; i < 30; i++)
        {
            session.Tick(RunInput.Idle with { MoveY = 1f, LookYawDegrees = 2f });
        }

        session.Player.Gold = 77;
        RunSnapshot snapshot = session.Capture();
        RunSession restored = Restored(snapshot);

        Assert.Equal(session.RunIndex, restored.RunIndex);
        Assert.Equal(session.Player.Body.X, restored.Player.Body.X);
        Assert.Equal(session.Player.Body.Y, restored.Player.Body.Y);
        Assert.Equal(77, restored.Player.Gold);
        Assert.Equal(session.Level.Width, restored.Level.Width);
        for (int y = 0; y < session.Level.Height; y++)
        {
            for (int x = 0; x < session.Level.Width; x++)
            {
                Assert.Equal(session.Level.At(x, y), restored.Level.At(x, y));
            }
        }
    }

    private static (int X, int Y) FindTile(RunSession session, Kit.World.TileKind kind)
    {
        for (int y = 0; y < session.Level.Height; y++)
        {
            for (int x = 0; x < session.Level.Width; x++)
            {
                if (session.Level.At(x, y).Kind == kind)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException($"Floor has no {kind} tile.");
    }

    /// <summary>Stand just west of a tile and face it, so "use" reaches it.</summary>
    private static void StandBefore(RunSession session, int tileX, int tileY)
    {
        session.Player.Body.X = tileX - 0.5f;
        session.Player.Body.Y = tileY + 0.5f;
        session.Player.Body.Facing = MathF.PI / 2f;
    }
}
