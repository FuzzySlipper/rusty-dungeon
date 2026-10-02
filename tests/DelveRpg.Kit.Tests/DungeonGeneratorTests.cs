using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class DungeonGeneratorTests
{
    private static readonly GameTuning Tuning = new();

    private static GenerationConfig Config => new(
        Width: 32,
        Height: 32,
        RoomCountMin: 5,
        RoomCountMax: 8,
        RoomSizeMin: 4,
        RoomSizeMax: 7,
        MonsterCount: 4,
        ItemCount: 3);

    private static FloorSpec Floor => new(0, 2, "Test Section", "TestTheme", false);

    [Fact]
    public void Same_seed_produces_the_same_floor()
    {
        GeneratedLevel left = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(42, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);
        GeneratedLevel right = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(42, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);

        Assert.Equal(left.Level.Width, right.Level.Width);
        for (int y = 0; y < left.Level.Height; y++)
        {
            for (int x = 0; x < left.Level.Width; x++)
            {
                Assert.Equal(left.Level.At(x, y), right.Level.At(x, y));
            }
        }

        Assert.Equal(left.StartX, right.StartX);
        Assert.Equal(left.StairsX, right.StairsX);
        Assert.Equal(left.Monsters.Count, right.Monsters.Count);
        Assert.Equal(left.Items.Count, right.Items.Count);
    }

    [Fact]
    public void Different_seeds_produce_different_floors()
    {
        GeneratedLevel left = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(1, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);
        GeneratedLevel right = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(2, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);

        bool anyDifference = false;
        for (int y = 0; y < left.Level.Height && !anyDifference; y++)
        {
            for (int x = 0; x < left.Level.Width; x++)
            {
                if (left.Level.At(x, y) != right.Level.At(x, y))
                {
                    anyDifference = true;
                    break;
                }
            }
        }

        Assert.True(anyDifference);
    }

    [Fact]
    public void Stairs_are_reachable_from_the_start()
    {
        GeneratedLevel generated = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(7, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);

        int reach = ReachableTiles(generated);
        Assert.True(reach > 0);
        Assert.Equal(TileKind.StairsUp, generated.Level.At(generated.StartX, generated.StartY).Kind);
        Assert.Equal(TileKind.StairsDown, generated.Level.At(generated.StairsX, generated.StairsY).Kind);
        Assert.True(BfsDistance(generated, generated.StartX, generated.StartY, generated.StairsX, generated.StairsY) > 0);
    }

    [Fact]
    public void Spawns_sit_on_walkable_tiles_away_from_the_start()
    {
        GeneratedLevel generated = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(9, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);

        Assert.Equal(Config.MonsterCount, generated.Monsters.Count);
        Assert.Equal(Config.ItemCount, generated.Items.Count);
        foreach (MonsterSpawn spawn in generated.Monsters)
        {
            Assert.True(generated.Level.IsWalkable(spawn.X, spawn.Y));
            Assert.True(BfsDistance(generated, generated.StartX, generated.StartY, spawn.X, spawn.Y) >= 6);
        }

        foreach (ItemSpawn spawn in generated.Items)
        {
            Assert.True(generated.Level.IsWalkable(spawn.X, spawn.Y));
        }
    }

    [Fact]
    public void The_entrance_room_hosts_no_hostile_spawns()
    {
        // The run must be able to look around before anything comes for it.
        for (int seed = 1; seed <= 12; seed++)
        {
            GeneratedLevel generated = DungeonGenerator.Generate(
                new SplitMixRandom(SplitMixRandom.FloorSeed((ulong)seed, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);

            Assert.True(generated.EntranceRoom.Contains(generated.StartX, generated.StartY));
            Assert.Equal(Config.MonsterCount, generated.Monsters.Count);
            foreach (MonsterSpawn spawn in generated.Monsters)
            {
                Assert.False(
                    generated.EntranceRoom.Contains(spawn.X, spawn.Y),
                    $"monster at {spawn.X},{spawn.Y} landed in the entrance room (seed {seed})");
                int distance = BfsDistance(generated, generated.StartX, generated.StartY, spawn.X, spawn.Y);
                Assert.True(distance >= 6, $"monster at {spawn.X},{spawn.Y} is only {distance} tiles from start (seed {seed})");
            }
        }
    }

    [Fact]
    public void Generation_fails_loudly_when_the_config_cannot_place_rooms()
    {
        GenerationConfig impossible = Config with { Width = 6, Height = 6, RoomCountMin = 10, RoomCountMax = 12, RoomSizeMin = 8, RoomSizeMax = 10 };
        Assert.Throws<ArgumentOutOfRangeException>(() => DungeonGenerator.Generate(
            new SplitMixRandom(1), impossible, Tuning, Floor, ["m1"], ["i1"]));
    }

    private static int ReachableTiles(GeneratedLevel generated)
    {
        int count = 0;
        for (int y = 0; y < generated.Level.Height; y++)
        {
            for (int x = 0; x < generated.Level.Width; x++)
            {
                if (generated.Level.IsWalkable(x, y) && BfsDistance(generated, generated.StartX, generated.StartY, x, y) > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Navigation distance: any actor can open a closed door, and a locked
    /// one opens with the floor's key, so doors count as reachable ground here.
    /// </summary>
    private static int BfsDistance(GeneratedLevel generated, int fromX, int fromY, int toX, int toY)
    {
        var seen = new HashSet<(int X, int Y)> { (fromX, fromY) };
        var queue = new Queue<(int X, int Y, int Distance)>();
        queue.Enqueue((fromX, fromY, 0));
        while (queue.Count > 0)
        {
            (int x, int y, int distance) = queue.Dequeue();
            if (x == toX && y == toY)
            {
                return distance;
            }

            foreach ((int nextX, int nextY) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                bool passable = generated.Level.IsNavigable(nextX, nextY)
                    || (generated.Level.InBounds(nextX, nextY) && generated.Level.At(nextX, nextY).Kind == TileKind.DoorLocked);
                if (passable && seen.Add((nextX, nextY)))
                {
                    queue.Enqueue((nextX, nextY, distance + 1));
                }
            }
        }

        return -1;
    }

    [Fact]
    public void A_vault_locks_a_side_room_whose_key_lies_on_the_open_side_and_the_stairs_stay_open()
    {
        int vaults = 0;
        for (int seed = 1; seed <= 24; seed++)
        {
            GeneratedLevel generated = DungeonGenerator.Generate(
                new SplitMixRandom(SplitMixRandom.FloorSeed((ulong)seed, 0)), Config, Tuning with { VaultChance = 1f }, Floor, ["m1"], ["i1"]);
            DungeonLevel level = generated.Level;
            int locked = 0;
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    locked += level.At(x, y).Kind == TileKind.DoorLocked ? 1 : 0;
                }
            }

            // Without the key: the stairs are reachable, and so is the key.
            Assert.True(OpenDistance(level, generated.StartX, generated.StartY, generated.StairsX, generated.StairsY) >= 0, $"seed {seed}");
            if (generated.KeySpot is not (int keyX, int keyY))
            {
                Assert.Equal(0, locked);
                continue;
            }

            vaults++;
            Assert.Equal(1, locked);
            Assert.True(OpenDistance(level, generated.StartX, generated.StartY, keyX, keyY) >= 6, $"seed {seed}: key too close or locked away");
        }

        Assert.True(vaults > 0, "no floor in 24 seeds had a door that could lock a side room");
    }

    [Fact]
    public void Traps_keep_clear_of_the_start_and_the_stairs_and_plates_name_an_answering_effect()
    {
        GeneratedLevel generated = DungeonGenerator.Generate(
            new SplitMixRandom(SplitMixRandom.FloorSeed(5UL, 0)), Config, Tuning with { TrapChance = 0.2f, PotChance = 0.3f }, Floor, ["m1"], ["i1"]);
        FloorFeatures features = generated.Features;

        Assert.NotEmpty(features.Spikes);
        Assert.NotEmpty(features.Pots);
        var trapTiles = features.Spikes.Select(spikes => (spikes.TileX, spikes.TileY))
            .Concat(features.Triggers.Where(trigger => trigger.IsPlate).Select(trigger => (trigger.TileX, trigger.TileY)));
        foreach ((int x, int y) in trapTiles)
        {
            Assert.True(Math.Max(Math.Abs(x - generated.StartX), Math.Abs(y - generated.StartY)) > 6);
            Assert.True(Math.Max(Math.Abs(x - generated.StairsX), Math.Abs(y - generated.StairsY)) > 6);
            Assert.Equal(TileKind.Floor, generated.Level.At(x, y).Kind);
        }

        foreach (TouchTrigger trigger in features.Triggers)
        {
            Assert.Contains(features.Effects, effect => effect.TriggerId == trigger.TargetId);
        }
    }

    /// <summary>Steps without passing a locked door; −1 when unreachable.</summary>
    private static int OpenDistance(DungeonLevel level, int fromX, int fromY, int toX, int toY)
    {
        var seen = new HashSet<(int X, int Y)> { (fromX, fromY) };
        var queue = new Queue<(int X, int Y, int Distance)>();
        queue.Enqueue((fromX, fromY, 0));
        while (queue.Count > 0)
        {
            (int x, int y, int distance) = queue.Dequeue();
            if (x == toX && y == toY)
            {
                return distance;
            }

            foreach ((int nextX, int nextY) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (level.IsNavigable(nextX, nextY) && seen.Add((nextX, nextY)))
                {
                    queue.Enqueue((nextX, nextY, distance + 1));
                }
            }
        }

        return -1;
    }

    [Fact]
    public void Doors_stand_only_in_doorways_between_two_walls_and_corridors_are_floor()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            GeneratedLevel generated = DungeonGenerator.Generate(
                new SplitMixRandom(SplitMixRandom.FloorSeed((ulong)seed, 0)), Config, Tuning, Floor, ["m1"], ["i1"]);
            DungeonLevel level = generated.Level;
            int doors = 0;
            int corridorFloor = 0;
            for (int y = 1; y < level.Height - 1; y++)
            {
                for (int x = 1; x < level.Width - 1; x++)
                {
                    TileKind kind = level.At(x, y).Kind;
                    if (kind == TileKind.Floor)
                    {
                        corridorFloor++;
                    }

                    if (kind is not (TileKind.DoorClosed or TileKind.DoorLocked))
                    {
                        continue;
                    }

                    doors++;
                    bool wallsAcross = (Wall(level, x - 1, y) && Wall(level, x + 1, y) && !Wall(level, x, y - 1) && !Wall(level, x, y + 1))
                        || (Wall(level, x, y - 1) && Wall(level, x, y + 1) && !Wall(level, x - 1, y) && !Wall(level, x + 1, y));
                    Assert.True(wallsAcross, $"door at {x},{y} is not in a doorway (seed {seed})");
                    foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        Assert.False(level.At(nx, ny).Kind is TileKind.DoorClosed or TileKind.DoorLocked, $"doors touch at {x},{y} (seed {seed})");
                    }
                }
            }

            Assert.True(doors < corridorFloor / 4, $"{doors} doors against {corridorFloor} floor tiles (seed {seed})");
        }
    }

    private static bool Wall(DungeonLevel level, int x, int y) => level.At(x, y).Kind == TileKind.Wall;
}

