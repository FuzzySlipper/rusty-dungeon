using System.Numerics;
using DelveRpg.Kit.World;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// Turns one tile grid into a retained static mesh: floor quads for walkable
/// tiles and wall boxes for solids that touch open space. Vertex colors stand
/// in for the donor's textured tile atlas until this repo's own art slice
/// lands.
/// </summary>
public static class LevelMesh
{
    private static readonly Dictionary<string, (float R, float G, float B)> Themes = new(StringComparer.Ordinal)
    {
        ["Sewer"] = (0.30f, 0.36f, 0.28f),
        ["Temple"] = (0.42f, 0.38f, 0.28f),
        ["Cave"] = (0.36f, 0.32f, 0.28f),
        ["Undead"] = (0.30f, 0.28f, 0.36f),
        ["Cold"] = (0.32f, 0.38f, 0.46f),
    };

    public static (Vector3[] Positions, Vector3[] Normals, Vector2[] Uvs, Color[] Colors, uint[] Indices) Build(
        DungeonLevel level,
        string theme)
    {
        (float r, float g, float b) = Themes.TryGetValue(theme, out (float R, float G, float B) palette)
            ? palette
            : (0.34f, 0.32f, 0.30f);

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<uint>();

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                Tile tile = level.At(x, y);
                if (tile.BlocksMovement)
                {
                    if (TouchesOpen(level, x, y))
                    {
                        AddBox(positions, normals, uvs, colors, indices, x, y, 0f, 1f, WallColor(tile, r, g, b));
                    }

                    continue;
                }

                AddQuad(positions, normals, uvs, colors, indices, x, y, FloorColor(tile, r, g, b));
            }
        }

        return (positions.ToArray(), normals.ToArray(), uvs.ToArray(), colors.ToArray(), indices.ToArray());
    }

    private static bool TouchesOpen(DungeonLevel level, int x, int y) =>
        (level.InBounds(x + 1, y) && !level.At(x + 1, y).BlocksMovement)
        || (level.InBounds(x - 1, y) && !level.At(x - 1, y).BlocksMovement)
        || (level.InBounds(x, y + 1) && !level.At(x, y + 1).BlocksMovement)
        || (level.InBounds(x, y - 1) && !level.At(x, y - 1).BlocksMovement);

    private static Color FloorColor(Tile tile, float r, float g, float b) => tile.Kind switch
    {
        TileKind.StairsDown => new Color(0.55f, 0.18f, 0.16f, 1f),
        TileKind.StairsUp => new Color(0.24f, 0.52f, 0.26f, 1f),
        TileKind.Water => new Color(r * 0.5f, g * 0.6f, b * 1.2f, 1f),
        _ => new Color(r, g, b, 1f),
    };

    private static Color WallColor(Tile tile, float r, float g, float b) => tile.Kind switch
    {
        TileKind.DoorClosed => new Color(r * 1.4f, g * 1.1f, b * 0.6f, 1f),
        _ => new Color(r * 0.62f, g * 0.62f, b * 0.66f, 1f),
    };

    private static void AddQuad(
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<Color> colors,
        List<uint> indices,
        int x,
        int y,
        Color color)
    {
        uint start = (uint)positions.Count;
        positions.Add(new Vector3(x, 0f, y));
        positions.Add(new Vector3(x + 1, 0f, y));
        positions.Add(new Vector3(x + 1, 0f, y + 1));
        positions.Add(new Vector3(x, 0f, y + 1));
        for (int i = 0; i < 4; i++)
        {
            normals.Add(Vector3.UnitY);
            colors.Add(color);
        }

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));
        indices.AddRange(new uint[] { start, start + 2, start + 1, start, start + 3, start + 2 });
    }

    private static void AddBox(
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<Color> colors,
        List<uint> indices,
        int x,
        int y,
        float bottom,
        float top,
        Color color)
    {
        Vector3 a = new(x, bottom, y);
        Vector3 b = new(x + 1, bottom, y);
        Vector3 c = new(x + 1, bottom, y + 1);
        Vector3 d = new(x, bottom, y + 1);
        Vector3 e = new(x, top, y);
        Vector3 f = new(x + 1, top, y);
        Vector3 g = new(x + 1, top, y + 1);
        Vector3 h = new(x, top, y + 1);

        AddFace(positions, normals, uvs, colors, indices, e, f, g, h, Vector3.UnitY, color);
        AddFace(positions, normals, uvs, colors, indices, a, b, f, e, -Vector3.UnitZ, color);
        AddFace(positions, normals, uvs, colors, indices, c, d, h, g, Vector3.UnitZ, color);
        AddFace(positions, normals, uvs, colors, indices, b, c, g, f, Vector3.UnitX, color);
        AddFace(positions, normals, uvs, colors, indices, d, a, e, h, -Vector3.UnitX, color);
    }

    private static void AddFace(
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<Color> colors,
        List<uint> indices,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector3 normal,
        Color color)
    {
        uint start = (uint)positions.Count;
        positions.Add(v0);
        positions.Add(v1);
        positions.Add(v2);
        positions.Add(v3);
        for (int i = 0; i < 4; i++)
        {
            normals.Add(normal);
            colors.Add(color);
        }

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));
        indices.AddRange(new uint[] { start, start + 2, start + 1, start, start + 3, start + 2 });
    }
}
