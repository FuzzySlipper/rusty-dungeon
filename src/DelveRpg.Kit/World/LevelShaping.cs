using DelveRpg.Kit.Random;

namespace DelveRpg.Kit.World;

/// <summary>
/// An authored room piece stamped into a generated room: rows of marker
/// characters, the donor's prefab chunks and room-builder markers in small
/// ([donor] generator/GenInfo.java Markers, generator/RoomGenerator.java).
/// <list type="bullet">
/// <item><c>.</c> floor, <c>#</c> pillar, <c>^</c> raised dais, <c>~</c> water;</item>
/// <item><c>*</c> a find on a raised dais, out of reach but for a jump;</item>
/// <item><c>T</c> a torch hung on the pillar beside it;</item>
/// <item><c>M</c> a monster, <c>L</c> a find, <c>o</c> a pot.</item>
/// </list>
/// </summary>
public sealed record RoomTemplate(string Id, IReadOnlyList<string> Rows)
{
    public int Width => Rows.Count == 0 ? 0 : Rows.Max(row => row.Length);

    public int Height => Rows.Count;

    public char At(int x, int y) => y < Rows.Count && x < Rows[y].Length ? Rows[y][x] : '.';
}

/// <summary>What stamping templates added to a floor: spawn points, pots and torches.</summary>
public sealed record StampedMarkers(
    IReadOnlyList<(int X, int Y)> Monsters,
    IReadOnlyList<(int X, int Y)> Loot,
    IReadOnlyList<(int X, int Y)> Pots,
    IReadOnlyList<WallTorch> Torches);

/// <summary>
/// Heights, room pieces and torches for a carved floor.
/// <list type="bullet">
/// <item>Heights: each room sits a step from the one before it, and its
/// ceiling rises 1 to 2.4 above its floor, the donor's
/// <c>floor + 1 + rand(8) × 0.2</c> ([donor] generator/rooms/Room.java:82).
/// Corridors take a smooth ramp of steps between the rooms they join, as the
/// donor's hallways step from one end height to the other
/// (halls/Hallway.java:106-159). A floor whose steps would be too tall to
/// walk anywhere is flattened instead.</item>
/// <item>Room pieces: a room may take an authored template, centred.</item>
/// <item>Torches: markers on room walls, every few tiles along the long
/// walls, as the donor's room builder hangs them (RoomGenerator.java:302, 506).
/// Corridors stay dark.</item>
/// </list>
/// </summary>
public static class LevelShaping
{
    /// <summary>A dais rises 0.4375 ([donor] RoomGenerator.java:1120): above a step, so it takes a jump.</summary>
    public const float DaisRise = 0.4375f;

    /// <summary>The steepest step a shaped floor keeps between walkable neighbours; above it the floor is flattened.</summary>
    public const float MaxWalkStep = 0.3f;

    private const int TorchSpacing = 5;

    public static void ShapeHeights(IRandomSource random, DungeonLevel level, IReadOnlyList<RoomBounds> rooms)
    {
        var roomFloors = new float[rooms.Count];
        for (int i = 1; i < rooms.Count; i++)
        {
            roomFloors[i] = Math.Clamp(roomFloors[i - 1] + ((random.Next(0, 3) - 1) * 0.2f), -0.6f, 0.6f);
        }

        var fixedTiles = new bool[level.Width * level.Height];
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomBounds room = rooms[i];
            float ceiling = roomFloors[i] + 1f + (random.Next(0, 8) * 0.2f);
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    level.SetHeights(x, y, roomFloors[i], ceiling);
                    fixedTiles[(y * level.Width) + x] = true;
                }
            }
        }

        // Corridors relax towards the average of their neighbours, which
        // spreads each height difference evenly along the way between rooms.
        var heights = new float[level.Width * level.Height];
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                heights[(y * level.Width) + x] = level.FloorHeight(x, y);
            }
        }

        for (int pass = 0; pass < 200; pass++)
        {
            for (int y = 1; y < level.Height - 1; y++)
            {
                for (int x = 1; x < level.Width - 1; x++)
                {
                    int index = (y * level.Width) + x;
                    if (fixedTiles[index] || !level.IsNavigable(x, y))
                    {
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;
                    foreach ((int dx, int dy) in Steps)
                    {
                        if (level.IsNavigable(x + dx, y + dy))
                        {
                            sum += heights[((y + dy) * level.Width) + x + dx];
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        heights[index] = sum / count;
                    }
                }
            }
        }

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int index = (y * level.Width) + x;
                if (!fixedTiles[index] && level.IsNavigable(x, y))
                {
                    float floor = MathF.Round(heights[index] * 16f) / 16f;
                    level.SetHeights(x, y, floor, floor + 1f);
                }
            }
        }

        if (SteepestStep(level) > MaxWalkStep)
        {
            Flatten(level);
        }
    }

    /// <summary>
    /// Stamp templates into rooms other than the entrance, each with
    /// <paramref name="chance"/>, centred, where one fits. A stamp that would
    /// cut the floor apart is taken back.
    /// </summary>
    public static StampedMarkers StampTemplates(
        IRandomSource random,
        DungeonLevel level,
        IReadOnlyList<RoomBounds> rooms,
        RoomBounds entrance,
        IReadOnlyList<RoomTemplate> templates,
        float chance)
    {
        var monsters = new List<(int, int)>();
        var loot = new List<(int, int)>();
        var pots = new List<(int, int)>();
        var torches = new List<WallTorch>();
        if (templates.Count == 0)
        {
            return new StampedMarkers(monsters, loot, pots, torches);
        }

        foreach (RoomBounds room in rooms)
        {
            if (room == entrance || !random.Chance(chance))
            {
                continue;
            }

            List<RoomTemplate> fitting = templates.Where(t => t.Width <= room.Width && t.Height <= room.Height).ToList();
            if (fitting.Count == 0)
            {
                continue;
            }

            RoomTemplate template = fitting[random.Next(0, fitting.Count)];
            int left = room.X + ((room.Width - template.Width) / 2);
            int top = room.Y + ((room.Height - template.Height) / 2);
            var before = new List<(int X, int Y, Tile Tile, float Floor, float Ceiling)>();
            var stampMonsters = new List<(int, int)>();
            var stampLoot = new List<(int, int)>();
            var stampPots = new List<(int, int)>();
            var stampTorches = new List<(int X, int Y)>();
            for (int ty = 0; ty < template.Height; ty++)
            {
                for (int tx = 0; tx < template.Width; tx++)
                {
                    int x = left + tx;
                    int y = top + ty;
                    float floor = level.FloorHeight(x, y);
                    float ceiling = level.CeilingHeight(x, y);
                    before.Add((x, y, level.At(x, y), floor, ceiling));
                    switch (template.At(tx, ty))
                    {
                        case '#':
                            level.Set(x, y, Tile.Wall);
                            break;
                        case '^':
                            level.SetHeights(x, y, floor + DaisRise, Math.Max(ceiling, floor + DaisRise + 1f));
                            break;
                        case '*':
                            level.SetHeights(x, y, floor + DaisRise, Math.Max(ceiling, floor + DaisRise + 1f));
                            stampLoot.Add((x, y));
                            break;
                        case '~':
                            level.Set(x, y, Tile.Water);
                            break;
                        case 'T':
                            stampTorches.Add((x, y));
                            break;
                        case 'M':
                            stampMonsters.Add((x, y));
                            break;
                        case 'L':
                            stampLoot.Add((x, y));
                            break;
                        case 'o':
                            stampPots.Add((x, y));
                            break;
                    }
                }
            }

            if (CountReachable(level) < CountWalkable(level))
            {
                foreach ((int x, int y, Tile tile, float floor, float ceiling) in before)
                {
                    level.Set(x, y, tile);
                    level.SetHeights(x, y, floor, ceiling);
                }

                continue;
            }

            monsters.AddRange(stampMonsters);
            loot.AddRange(stampLoot);
            pots.AddRange(stampPots);
            foreach ((int x, int y) in stampTorches)
            {
                foreach ((int dx, int dy) in Steps)
                {
                    if (level.At(x + dx, y + dy).Kind == TileKind.Wall)
                    {
                        torches.Add(new WallTorch(x, y, dx, dy));
                        break;
                    }
                }
            }
        }

        return new StampedMarkers(monsters, loot, pots, torches);
    }

    /// <summary>
    /// Torch markers along each room's walls: a floor tile against a solid
    /// wall every few tiles of the long sides, starting a little in.
    /// </summary>
    public static List<WallTorch> RoomTorches(IRandomSource random, DungeonLevel level, IReadOnlyList<RoomBounds> rooms)
    {
        var torches = new List<WallTorch>();
        foreach (RoomBounds room in rooms)
        {
            bool alongX = room.Width >= room.Height;
            int length = alongX ? room.Width : room.Height;
            int start = 1 + random.Next(0, 2);
            for (int along = start; along < length - 1; along += TorchSpacing)
            {
                foreach (int side in new[] { 0, 1 })
                {
                    (int x, int y, int dx, int dy) = alongX
                        ? (room.X + along, side == 0 ? room.Y : room.Y + room.Height - 1, 0, side == 0 ? -1 : 1)
                        : (side == 0 ? room.X : room.X + room.Width - 1, room.Y + along, side == 0 ? -1 : 1, 0);
                    if (level.At(x, y).Kind == TileKind.Floor && level.At(x + dx, y + dy).Kind == TileKind.Wall)
                    {
                        torches.Add(new WallTorch(x, y, dx, dy));
                    }
                }
            }
        }

        return torches;
    }

    /// <summary>The tallest climb between walkable neighbours, daises and water aside.</summary>
    public static float SteepestStep(DungeonLevel level)
    {
        float steepest = 0f;
        for (int y = 1; y < level.Height - 1; y++)
        {
            for (int x = 1; x < level.Width - 1; x++)
            {
                if (!level.IsNavigable(x, y))
                {
                    continue;
                }

                foreach ((int dx, int dy) in new[] { (1, 0), (0, 1) })
                {
                    if (level.IsNavigable(x + dx, y + dy))
                    {
                        steepest = Math.Max(steepest, MathF.Abs(level.FloorHeight(x, y) - level.FloorHeight(x + dx, y + dy)));
                    }
                }
            }
        }

        return steepest;
    }

    private static void Flatten(DungeonLevel level)
    {
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                level.SetHeights(x, y, 0f, Math.Max(1f, level.CeilingHeight(x, y) - level.FloorHeight(x, y)));
            }
        }
    }

    private static int CountWalkable(DungeonLevel level)
    {
        int count = 0;
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                count += level.IsNavigable(x, y) ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>Navigable tiles reachable from the first one found.</summary>
    private static int CountReachable(DungeonLevel level)
    {
        (int X, int Y)? first = null;
        for (int y = 0; y < level.Height && first is null; y++)
        {
            for (int x = 0; x < level.Width && first is null; x++)
            {
                if (level.IsNavigable(x, y))
                {
                    first = (x, y);
                }
            }
        }

        if (first is not { } origin)
        {
            return 0;
        }

        var seen = new HashSet<(int, int)> { origin };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            foreach ((int dx, int dy) in Steps)
            {
                if (level.IsNavigable(x + dx, y + dy) && seen.Add((x + dx, y + dy)))
                {
                    queue.Enqueue((x + dx, y + dy));
                }
            }
        }

        return seen.Count;
    }

    private static readonly (int X, int Y)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];
}
