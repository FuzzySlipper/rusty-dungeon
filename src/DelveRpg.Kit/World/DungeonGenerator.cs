using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.World;

/// <summary>Size and density knobs for one floor's generation pass.</summary>
public sealed record GenerationConfig(
    int Width,
    int Height,
    int RoomCountMin,
    int RoomCountMax,
    int RoomSizeMin,
    int RoomSizeMax,
    int MonsterCount,
    int ItemCount,
    int MaxGenerationAttempts = 8);

public sealed record MonsterSpawn(string ArchetypeId, int X, int Y);

public sealed record ItemSpawn(string ArchetypeId, int X, int Y);

/// <summary>One room rectangle on the grid.</summary>
public readonly record struct RoomBounds(int X, int Y, int Width, int Height)
{
    /// <summary>Whether a tile sits in the room or on its immediate seams.</summary>
    public bool Contains(int x, int y) =>
        x >= X - 1 && x <= X + Width && y >= Y - 1 && y <= Y + Height;
}

/// <summary>
/// One finished floor: the grid, where the run starts and ends, and the
/// entrance room bounds (the spawn-free safe ground at the start).
/// </summary>
public sealed record GeneratedLevel(
    DungeonLevel Level,
    int StartX,
    int StartY,
    int StairsX,
    int StairsY,
    IReadOnlyList<MonsterSpawn> Monsters,
    IReadOnlyList<ItemSpawn> Items,
    IReadOnlyList<ItemSpawn> BonusLoot,
    RoomBounds EntranceRoom)
{
    /// <summary>Traps, triggers and pots (<see cref="FeaturePlacement"/>).</summary>
    public FloorFeatures Features { get; init; } = FloorFeatures.None;

    /// <summary>Where the vault's key lies, when the floor has a locked vault.</summary>
    public (int X, int Y)? KeySpot { get; init; }
}

/// <summary>
/// Room-and-corridor floor generation. The donor grows prefab chunks through a
/// chunk graph; this port keeps the outcome it guarantees — rooms connected by
/// corridors, doorways at room seams, exit candidates far from the start, and a
/// navigability check that rejects disconnected floors — with a simpler plan.
/// </summary>
public static class DungeonGenerator
{
    public static GeneratedLevel Generate(
        IRandomSource random,
        GenerationConfig config,
        GameTuning tuning,
        FloorSpec floor,
        IReadOnlyList<string> eligibleMonsterIds,
        IReadOnlyList<string> eligibleItemIds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(config.MonsterCount);
        ArgumentOutOfRangeException.ThrowIfNegative(config.ItemCount);
        if (config.RoomSizeMin < 2 || config.RoomSizeMax + 2 > Math.Min(config.Width, config.Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                "Room sizes cannot fit inside the configured floor with a wall margin.");
        }

        for (int attempt = 0; attempt < config.MaxGenerationAttempts; attempt++)
        {
            GeneratedLevel? candidate = TryGenerate(random, config, tuning, floor, eligibleMonsterIds, eligibleItemIds);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Floor generation failed after {config.MaxGenerationAttempts} attempts for dungeon level {floor.DungeonLevel}.");
    }

    private static GeneratedLevel? TryGenerate(
        IRandomSource random,
        GenerationConfig config,
        GameTuning tuning,
        FloorSpec floor,
        IReadOnlyList<string> eligibleMonsterIds,
        IReadOnlyList<string> eligibleItemIds)
    {
        var level = new DungeonLevel(config.Width, config.Height, floor.DungeonLevel, floor.Theme);
        List<(int X, int Y, int Width, int Height)> rooms = PlaceRooms(random, config);
        if (rooms.Count < 2)
        {
            return null;
        }

        foreach ((int x, int y, int width, int height) in rooms)
        {
            CarveRoom(level, x, y, width, height);
        }

        for (int i = 1; i < rooms.Count; i++)
        {
            (int fromX, int fromY) = RoomCenter(rooms[i - 1]);
            (int toX, int toY) = RoomCenter(rooms[i]);
            CarveCorridor(level, random, fromX, fromY, toX, toY);
        }

        PlaceDoors(level, rooms);

        (int startX, int startY) = RoomCenter(rooms[0]);
        level.Set(startX, startY, Tile.StairsUp);

        int[] distances = DistancesFrom(level, startX, startY);
        List<(int X, int Y, int Distance)> exits = ExitCandidates(level, distances);
        if (exits.Count == 0)
        {
            return null;
        }

        (int stairsX, int stairsY, _) = exits[0];
        level.Set(stairsX, stairsY, Tile.StairsDown);

        var bonusLoot = new List<ItemSpawn>();
        foreach ((int x, int y, _) in exits.Skip(1).Take(2))
        {
            if (eligibleItemIds.Count > 0)
            {
                string itemId = eligibleItemIds[random.Next(0, eligibleItemIds.Count)];
                bonusLoot.Add(new ItemSpawn(itemId, x, y));
            }
        }

        (int entranceX, int entranceY, int entranceWidth, int entranceHeight) = rooms[0];
        List<(int X, int Y)> spawnSpots = OpenSpots(level, distances, minimumDistance: 6)
            .Where(spot => !InsideEntrance(entranceX, entranceY, entranceWidth, entranceHeight, spot.X, spot.Y))
            .ToList();
        var monsters = new List<MonsterSpawn>();
        var items = new List<ItemSpawn>();
        foreach ((int x, int y) in TakeShuffled(random, spawnSpots, config.MonsterCount))
        {
            if (eligibleMonsterIds.Count > 0)
            {
                monsters.Add(new MonsterSpawn(eligibleMonsterIds[random.Next(0, eligibleMonsterIds.Count)], x, y));
            }
        }

        foreach ((int x, int y) in TakeShuffled(random, spawnSpots, config.ItemCount + monsters.Count).Skip(monsters.Count))
        {
            if (eligibleItemIds.Count > 0)
            {
                items.Add(new ItemSpawn(eligibleItemIds[random.Next(0, eligibleItemIds.Count)], x, y));
            }
        }

        var entrance = new RoomBounds(entranceX, entranceY, entranceWidth, entranceHeight);
        (FloorFeatures features, Vault? vault) = FeaturePlacement.Place(
            random,
            level,
            rooms.Select(room => new RoomBounds(room.X, room.Y, room.Width, room.Height)).ToList(),
            entrance,
            (startX, startY),
            (stairsX, stairsY),
            tuning);
        if (vault is not null && eligibleItemIds.Count > 0)
        {
            // A vault holds two finds for the key it cost.
            foreach ((int x, int y) in TakeShuffled(random, vault.Inside.ToList(), 2))
            {
                bonusLoot.Add(new ItemSpawn(eligibleItemIds[random.Next(0, eligibleItemIds.Count)], x, y));
            }
        }

        return new GeneratedLevel(
            level,
            startX,
            startY,
            stairsX,
            stairsY,
            monsters,
            items,
            bonusLoot,
            entrance)
        {
            Features = features,
            KeySpot = vault?.Key,
        };
    }

    private static List<(int X, int Y, int Width, int Height)> PlaceRooms(IRandomSource random, GenerationConfig config)
    {
        var rooms = new List<(int X, int Y, int Width, int Height)>();
        int target = random.Next(config.RoomCountMin, config.RoomCountMax + 1);
        for (int attempt = 0; attempt < target * 4 && rooms.Count < target; attempt++)
        {
            int width = random.Next(config.RoomSizeMin, config.RoomSizeMax + 1);
            int height = random.Next(config.RoomSizeMin, config.RoomSizeMax + 1);
            int x = random.Next(1, config.Width - width - 1);
            int y = random.Next(1, config.Height - height - 1);
            var candidate = (X: x, Y: y, Width: width, Height: height);
            if (rooms.All(room => !Overlaps(room, candidate)))
            {
                rooms.Add(candidate);
            }
        }

        return rooms;
    }

    private static bool Overlaps((int X, int Y, int Width, int Height) a, (int X, int Y, int Width, int Height) b) =>
        a.X - 1 < b.X + b.Width && b.X - 1 < a.X + a.Width
        && a.Y - 1 < b.Y + b.Height && b.Y - 1 < a.Y + a.Height;

    private static (int X, int Y) RoomCenter((int X, int Y, int Width, int Height) room) =>
        (room.X + (room.Width / 2), room.Y + (room.Height / 2));

    private static void CarveRoom(DungeonLevel level, int x, int y, int width, int height)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                level.Set(column, row, Tile.Floor);
            }
        }
    }

    private static void CarveCorridor(DungeonLevel level, IRandomSource random, int fromX, int fromY, int toX, int toY)
    {
        bool horizontalFirst = random.Next(0, 2) == 0;
        if (horizontalFirst)
        {
            CarveHorizontal(level, fromX, toX, fromY);
            CarveVertical(level, fromY, toY, toX);
        }
        else
        {
            CarveVertical(level, fromY, toY, fromX);
            CarveHorizontal(level, fromX, toX, toY);
        }
    }

    private static void CarveHorizontal(DungeonLevel level, int fromX, int toX, int y)
    {
        int step = fromX <= toX ? 1 : -1;
        for (int x = fromX; x != toX + step; x += step)
        {
            CarveFloor(level, x, y);
        }
    }

    private static void CarveVertical(DungeonLevel level, int fromY, int toY, int x)
    {
        int step = fromY <= toY ? 1 : -1;
        for (int y = fromY; y != toY + step; y += step)
        {
            CarveFloor(level, x, y);
        }
    }

    private static void CarveFloor(DungeonLevel level, int x, int y) => level.Set(x, y, Tile.Floor);

    /// <summary>
    /// Doorways go where a corridor enters a room: a corridor tile outside
    /// every room, beside a room's floor, with walls on both sides across
    /// the way in — the donor's doorway prefabs at room seams. A corridor
    /// running along a room or crossing open floor gets no door, and no two
    /// doors touch.
    /// </summary>
    private static void PlaceDoors(DungeonLevel level, IReadOnlyList<(int X, int Y, int Width, int Height)> rooms)
    {
        bool InRoom(int x, int y) => rooms.Any(room =>
            x >= room.X && x < room.X + room.Width && y >= room.Y && y < room.Y + room.Height);
        bool Solid(int x, int y) => !level.InBounds(x, y) || level.At(x, y).Kind == TileKind.Wall;

        for (int y = 1; y < level.Height - 1; y++)
        {
            for (int x = 1; x < level.Width - 1; x++)
            {
                if (level.At(x, y).Kind != TileKind.Floor || InRoom(x, y))
                {
                    continue;
                }

                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!InRoom(x + dx, y + dy))
                    {
                        continue;
                    }

                    // The way in runs along (dx, dy); its sides are across it.
                    bool walledSides = Solid(x + dy, y + dx) && Solid(x - dy, y - dx);
                    bool doorBeside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }
                        .Any(n => level.At(x + n.Item1, y + n.Item2).Kind == TileKind.DoorClosed);
                    if (walledSides && !doorBeside)
                    {
                        level.Set(x, y, Tile.DoorClosed);
                        break;
                    }
                }
            }
        }
    }

    private static int[] DistancesFrom(DungeonLevel level, int startX, int startY)
    {
        var distances = new int[level.Width * level.Height];
        Array.Fill(distances, -1);
        var queue = new Queue<(int X, int Y)>();
        distances[(startY * level.Width) + startX] = 0;
        queue.Enqueue((startX, startY));
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            int distance = distances[(y * level.Width) + x];
            foreach ((int nextX, int nextY) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (!level.IsNavigable(nextX, nextY))
                {
                    continue;
                }

                int index = (nextY * level.Width) + nextX;
                if (distances[index] >= 0)
                {
                    continue;
                }

                distances[index] = distance + 1;
                queue.Enqueue((nextX, nextY));
            }
        }

        return distances;
    }

    private static List<(int X, int Y, int Distance)> ExitCandidates(DungeonLevel level, int[] distances)
    {
        var candidates = new List<(int X, int Y, int Distance)>();
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int distance = distances[(y * level.Width) + x];
                if (distance > 0 && level.At(x, y).Kind == TileKind.Floor)
                {
                    candidates.Add((x, y, distance));
                }
            }
        }

        candidates.Sort((left, right) => right.Distance.CompareTo(left.Distance));
        return candidates.Take(5).ToList();
    }

    /// <summary>
    /// The entrance room plus its doorway seams: the safe ground the run
    /// starts on. No hostile spawns land here.
    /// </summary>
    private static bool InsideEntrance(int roomX, int roomY, int roomWidth, int roomHeight, int x, int y) =>
        x >= roomX - 1 && x <= roomX + roomWidth && y >= roomY - 1 && y <= roomY + roomHeight;

    private static List<(int X, int Y)> OpenSpots(DungeonLevel level, int[] distances, int minimumDistance)
    {
        var spots = new List<(int X, int Y)>();
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int distance = distances[(y * level.Width) + x];
                if (distance >= minimumDistance && level.At(x, y).Kind == TileKind.Floor)
                {
                    spots.Add((x, y));
                }
            }
        }

        return spots;
    }

    private static IEnumerable<(int X, int Y)> TakeShuffled(IRandomSource random, List<(int X, int Y)> spots, int count)
    {
        for (int i = spots.Count - 1; i > 0 && count > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        return spots.Take(count);
    }
}
