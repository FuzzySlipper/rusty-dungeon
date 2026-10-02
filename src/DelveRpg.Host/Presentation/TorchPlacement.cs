using System.Numerics;
using DelveRpg.Kit.World;

namespace DelveRpg.Host.Presentation;

/// <summary>One wall torch: where its sprite hangs and where its light sits.</summary>
public readonly record struct WallTorch(Vector3 SpritePosition, Vector3 LightPosition);

/// <summary>
/// Wall torches for a floor. The donor's room templates carry torch markers
/// ([donor] generator/GenInfo.java Markers.torch, RoomGenerator.java:299-302
/// exit torches); this port's generated rooms have none, so presentation hangs
/// torches on walls from the tile grid alone: floor tiles against a wall, taken
/// in a hashed order and kept <see cref="Spacing"/> tiles apart. Torches are
/// decor with no rules attached, and the same grid always gets the same torches.
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
        var taken = new List<(int X, int Y)>();
        foreach ((uint _, int x, int y, int dx, int dy) in candidates)
        {
            if (torches.Count >= MaximumTorches)
            {
                break;
            }

            if (taken.Any(other => Math.Abs(other.X - x) < Spacing && Math.Abs(other.Y - y) < Spacing))
            {
                continue;
            }

            taken.Add((x, y));
            var center = new Vector2(x + 0.5f, y + 0.5f);
            var toWall = new Vector2(dx, dy);
            Vector2 sprite = center + (toWall * 0.42f);
            Vector2 light = center + (toWall * 0.25f);
            torches.Add(new WallTorch(new Vector3(sprite.X, 0.3f, sprite.Y), new Vector3(light.X, 0.65f, light.Y)));
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
