using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.World;

/// <summary>One kind of decoration a theme scatters (rules-facing: where it goes and whether it blocks).</summary>
public sealed record DecorKind(string SpriteId, int Weight, bool OnCeiling, bool Solid);

/// <summary>One placed decoration: its sprite, where it stands, and whether bodies go round it.</summary>
public sealed record Decor(string SpriteId, float X, float Y, bool OnCeiling, bool Solid);

/// <summary>
/// Scatters a theme's decorations over a floor: each open floor tile rolls
/// <c>decorChance</c>, and a hit drops a cluster of one to three pieces of one
/// kind, spread up to 0.4 tiles from the tile's centre — the donor's decor
/// markers and texture-gated sprite clusters (clusterCount, clusterSpread
/// 0.4; [donor] gamemode/delver/DelverGameMode.java:675-717, [data]
/// generator/&lt;Theme&gt;/info.dat decorations and genInfos). Choices come from
/// a hash of the tile, so the same grid always gets the same decorations
/// and they need no saving, like the wall torches.
/// </summary>
public static class DecorPlacement
{
    private const float Spread = 0.4f;

    public static IReadOnlyList<Decor> Place(DungeonLevel level, IReadOnlyList<DecorKind> kinds, float chance)
    {
        var placed = new List<Decor>();
        int totalWeight = kinds.Sum(kind => Math.Max(0, kind.Weight));
        if (totalWeight == 0 || chance <= 0f)
        {
            return placed;
        }

        for (int y = 1; y < level.Height - 1; y++)
        {
            for (int x = 1; x < level.Width - 1; x++)
            {
                if (level.At(x, y).Kind != TileKind.Floor || Unit(Hash(x, y, 1)) >= chance)
                {
                    continue;
                }

                int pick = (int)(Hash(x, y, 2) % (uint)totalWeight);
                DecorKind kind = kinds[0];
                foreach (DecorKind candidate in kinds)
                {
                    pick -= Math.Max(0, candidate.Weight);
                    if (pick < 0)
                    {
                        kind = candidate;
                        break;
                    }
                }

                // A solid piece stands alone at the centre so it never walls off a corridor's width.
                int count = kind.Solid ? 1 : 1 + (int)(Hash(x, y, 3) % 3);
                for (int i = 0; i < count; i++)
                {
                    float dx = kind.Solid ? 0f : (Unit(Hash(x, y, 10 + i)) - 0.5f) * 2f * Spread;
                    float dy = kind.Solid ? 0f : (Unit(Hash(x, y, 20 + i)) - 0.5f) * 2f * Spread;
                    placed.Add(new Decor(kind.SpriteId, x + 0.5f + dx, y + 0.5f + dy, kind.OnCeiling, kind.Solid));
                }
            }
        }

        return placed;
    }

    private static float Unit(uint hash) => (hash & 0xFFFFFF) / (float)0x1000000;

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791));
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }
}
