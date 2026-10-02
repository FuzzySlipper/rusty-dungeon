namespace DelveRpg.Kit.World;

/// <summary>
/// One wall torch: the floor tile it hangs in and the wall it hangs on
/// (<see cref="WallDx"/>, <see cref="WallDy"/> point from the tile to the wall).
/// </summary>
public readonly record struct WallTorch(int TileX, int TileY, int WallDx, int WallDy)
{
    /// <summary>Where the flame sprite hangs, in tile units: almost against the wall.</summary>
    public float SpriteX => TileX + 0.5f + (WallDx * 0.42f);

    public float SpriteY => TileY + 0.5f + (WallDy * 0.42f);

    /// <summary>Where the light sits, a little out from the wall.</summary>
    public float LightX => TileX + 0.5f + (WallDx * 0.25f);

    public float LightY => TileY + 0.5f + (WallDy * 0.25f);
}

/// <summary>
/// Wall torches for a floor. The donor's room templates carry torch markers
/// ([donor] generator/GenInfo.java Markers.torch, RoomGenerator.java:299-302
/// exit torches); this port's generated rooms have none, so torches hang on
/// walls from the tile grid alone: floor tiles against a wall, taken in a
/// hashed order and kept <see cref="Spacing"/> tiles apart. The same grid
/// always gets the same torches. Torches are a rules fact because their light
/// decides how far monsters see the player (<see cref="LightLevel"/>); the
/// Host draws them.
/// </summary>
public static class TorchPlacement
{
    public const int Spacing = 6;
    public const int MaximumTorches = 48;

    private static readonly (int X, int Y)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    public static IReadOnlyList<WallTorch> Place(DungeonLevel level)
    {
        var candidates = new List<(uint Order, int X, int Y, int Dx, int Dy)>();
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                if (level.At(x, y).Kind != TileKind.Floor)
                {
                    continue;
                }

                foreach ((int dx, int dy) in Directions)
                {
                    if (level.InBounds(x + dx, y + dy) && level.At(x + dx, y + dy).Kind == TileKind.Wall)
                    {
                        candidates.Add((Hash(x, y, dx, dy), x, y, dx, dy));
                    }
                }
            }
        }

        candidates.Sort((left, right) => left.Order.CompareTo(right.Order));
        var torches = new List<WallTorch>();
        foreach ((uint _, int x, int y, int dx, int dy) in candidates)
        {
            if (torches.Count >= MaximumTorches)
            {
                break;
            }

            if (torches.Any(other => Math.Abs(other.TileX - x) < Spacing && Math.Abs(other.TileY - y) < Spacing))
            {
                continue;
            }

            torches.Add(new WallTorch(x, y, dx, dy));
        }

        return torches;
    }

    private static uint Hash(int x, int y, int dx, int dy)
    {
        uint h = (uint)((x * 73856093) ^ (y * 19349663) ^ (((dx + 2) * 4) + dy + 2) * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        return h ^ (h >> 15);
    }
}

/// <summary>
/// How lit a point of the floor is, for the donor's light-based stealth
/// ([donor] game/Level.java:1734-1815 getLightColorAt,
/// entities/Light.java:201-233 attenuateLightColor). Each torch adds
/// <c>min(1, 2 × (1 − distance / range))</c> of its brightest channel when it
/// can see the point; a wall between them is full shadow. The donor's level
/// ambient defaults to black, and so does this.
/// </summary>
public static class LightLevel
{
    /// <summary>The donor torch's light range ([donor] entities/Torch.java).</summary>
    public const float TorchRange = 3.2f;

    /// <summary>The brightest channel of the torch colour (1, 0.8, 0.2).</summary>
    private const float TorchBrightness = 1f;

    public static float At(DungeonLevel level, IReadOnlyList<WallTorch> torches, float x, float y)
    {
        float light = 0f;
        foreach (WallTorch torch in torches)
        {
            float dx = torch.LightX - x;
            float dy = torch.LightY - y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance > TorchRange
                || !LineOfSight.CanSee(level, torch.TileX, torch.TileY, (int)MathF.Floor(x), (int)MathF.Floor(y)))
            {
                continue;
            }

            light += Math.Min(1f, 2f * (1f - (distance / TorchRange))) * TorchBrightness;
        }

        return light;
    }
}
