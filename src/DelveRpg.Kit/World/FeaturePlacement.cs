using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.World;

/// <summary>A locked side room: its door, its floor, and where its key lies.</summary>
public sealed record Vault((int X, int Y) Door, IReadOnlyList<(int X, int Y)> Inside, (int X, int Y) Key);

/// <summary>
/// Traps, triggers, pots and a locked vault for a freshly carved floor.
/// <list type="bullet">
/// <item>Traps: each open floor tile rolls the donor's intended 1.2% and
/// stays 6 tiles (Chebyshev) from the start and the stairs
/// ([donor] gamemode/delver/DelverGameMode.java:505-588 generateTraps, whose
/// own copy never places one: its eligibility array is never set). Half are
/// spikes, half pressure plates over a random fire, poison or teleport trap
/// (the Dungeon section's ["PressureTrap", "ProximitySpikes"], [data]
/// generator/Dungeon/section.dat).</item>
/// <item>Wall bolts: a room may hide a tripwire that a wall in line with it
/// answers with a magic bolt (the room builders' Magic Missile Trap).</item>
/// <item>Pots stand against room walls (the room builders' pot wall prefab).</item>
/// <item>The vault is this product's: the donor's generated floors never
/// lock a door, but its doors can lock and its keys exist. A door whose
/// closing cuts off only a side room — never the stairs — becomes locked,
/// the room gets extra loot, and the key lies somewhere reachable without it.</item>
/// </list>
/// </summary>
public static class FeaturePlacement
{
    private const int SafeRadius = 6;
    private const int WallBoltResetTicks = 200;
    private const int VaultMinimumTiles = 6;

    public static (FloorFeatures Features, Vault? Vault) Place(
        IRandomSource random,
        DungeonLevel level,
        IReadOnlyList<RoomBounds> rooms,
        RoomBounds entrance,
        (int X, int Y) start,
        (int X, int Y) stairs,
        GameTuning tuning)
    {
        var used = new HashSet<(int, int)> { start, stairs };
        Vault? vault = random.Chance(tuning.VaultChance) ? PlaceVault(random, level, start, stairs, entrance) : null;
        if (vault is not null)
        {
            used.Add(vault.Key);
        }

        long nextId = 1;
        var spikes = new List<SpikeTrap>();
        var triggers = new List<TouchTrigger>();
        var effects = new List<TrapEffect>();
        var pots = new List<Pot>();

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                if (level.At(x, y).Kind != TileKind.Floor
                    || used.Contains((x, y))
                    || Chebyshev((x, y), start) <= SafeRadius
                    || Chebyshev((x, y), stairs) <= SafeRadius
                    || !random.Chance(tuning.TrapChance))
                {
                    continue;
                }

                used.Add((x, y));
                if (random.Next(0, 2) == 0)
                {
                    spikes.Add(new SpikeTrap(nextId++, x, y));
                    continue;
                }

                string id = $"plate:{x},{y}";
                TrapEffectKind kind = (TrapEffectKind)random.Next(0, 3);
                triggers.Add(new TouchTrigger(nextId++, x, y, id, isPlate: true, resetTicks: 0));
                effects.Add(new TrapEffect(id, kind, x + 0.5f, y + 0.5f));
            }
        }

        foreach (RoomBounds room in rooms)
        {
            if (room == entrance)
            {
                continue;
            }

            if (random.Chance(tuning.WallBoltRoomChance) && PlaceWallBolt(random, level, room, used) is var (wire, emitter))
            {
                string id = $"bolt:{wire.X},{wire.Y}";
                triggers.Add(new TouchTrigger(nextId++, wire.X, wire.Y, id, isPlate: false, WallBoltResetTicks));
                effects.Add(new TrapEffect(id, TrapEffectKind.WallBolt, emitter.X, emitter.Y));
            }

            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    if (level.At(x, y).Kind != TileKind.Floor || used.Contains((x, y)) || !AgainstWall(level, x, y)
                        || !random.Chance(tuning.PotChance))
                    {
                        continue;
                    }

                    used.Add((x, y));
                    pots.Add(new Pot(nextId++, (PotKind)random.Next(0, 3), x + 0.5f, y + 0.5f));
                }
            }
        }

        return (new FloorFeatures(spikes, triggers, effects, pots), vault);
    }

    /// <summary>
    /// Lock one door whose closing leaves the stairs reachable and cuts off
    /// a side room of at least six tiles; the key goes at least six steps
    /// from the start, outside the entrance room, on the open side.
    /// </summary>
    private static Vault? PlaceVault(IRandomSource random, DungeonLevel level, (int X, int Y) start, (int X, int Y) stairs, RoomBounds entrance)
    {
        var candidates = new List<((int X, int Y) Door, List<(int X, int Y)> Inside, int[] Reach)>();
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                if (level.At(x, y).Kind != TileKind.DoorClosed)
                {
                    continue;
                }

                int[] reach = Distances(level, start, blocked: (x, y));
                if (reach[(stairs.Y * level.Width) + stairs.X] < 0)
                {
                    continue;
                }

                var inside = new List<(int X, int Y)>();
                for (int cy = 0; cy < level.Height; cy++)
                {
                    for (int cx = 0; cx < level.Width; cx++)
                    {
                        if (level.At(cx, cy).Kind == TileKind.Floor && reach[(cy * level.Width) + cx] < 0)
                        {
                            inside.Add((cx, cy));
                        }
                    }
                }

                if (inside.Count >= VaultMinimumTiles)
                {
                    candidates.Add(((x, y), inside, reach));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var (door, vaultTiles, distances) = candidates[random.Next(0, candidates.Count)];
        var keySpots = new List<(int X, int Y)>();
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int distance = distances[(y * level.Width) + x];
                if (level.At(x, y).Kind == TileKind.Floor && distance >= SafeRadius && !entrance.Contains(x, y) && (x, y) != stairs)
                {
                    keySpots.Add((x, y));
                }
            }
        }

        if (keySpots.Count == 0)
        {
            return null;
        }

        level.Set(door.X, door.Y, Tile.DoorLocked);
        return new Vault(door, vaultTiles, keySpots[random.Next(0, keySpots.Count)]);
    }

    /// <summary>A tripwire in the room with a wall two to six open tiles away in a straight line.</summary>
    private static ((int X, int Y) Wire, (float X, float Y) Emitter)? PlaceWallBolt(
        IRandomSource random, DungeonLevel level, RoomBounds room, HashSet<(int, int)> used)
    {
        int x = room.X + random.Next(0, Math.Max(1, room.Width));
        int y = room.Y + random.Next(0, Math.Max(1, room.Height));
        if (level.At(x, y).Kind != TileKind.Floor || used.Contains((x, y)))
        {
            return null;
        }

        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            for (int step = 1; step <= 7; step++)
            {
                int wx = x + (dx * step);
                int wy = y + (dy * step);
                if (!level.InBounds(wx, wy))
                {
                    break;
                }

                if (level.At(wx, wy).Kind == TileKind.Wall)
                {
                    if (step < 3)
                    {
                        break;
                    }

                    used.Add((x, y));
                    return ((x, y), (wx + 0.5f - (dx * 0.55f), wy + 0.5f - (dy * 0.55f)));
                }

                if (!level.IsWalkable(wx, wy))
                {
                    break;
                }
            }
        }

        return null;
    }

    private static bool AgainstWall(DungeonLevel level, int x, int y) =>
        level.At(x + 1, y).Kind == TileKind.Wall || level.At(x - 1, y).Kind == TileKind.Wall
        || level.At(x, y + 1).Kind == TileKind.Wall || level.At(x, y - 1).Kind == TileKind.Wall;

    private static int Chebyshev((int X, int Y) a, (int X, int Y) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Steps from the start over navigable tiles, one tile treated as blocked; −1 unreached.</summary>
    private static int[] Distances(DungeonLevel level, (int X, int Y) start, (int X, int Y) blocked)
    {
        var distances = new int[level.Width * level.Height];
        Array.Fill(distances, -1);
        var queue = new Queue<(int X, int Y)>();
        distances[(start.Y * level.Width) + start.X] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            int here = distances[(y * level.Width) + x];
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!level.IsNavigable(nx, ny) || (nx, ny) == blocked || distances[(ny * level.Width) + nx] >= 0)
                {
                    continue;
                }

                distances[(ny * level.Width) + nx] = here + 1;
                queue.Enqueue((nx, ny));
            }
        }

        return distances;
    }
}
