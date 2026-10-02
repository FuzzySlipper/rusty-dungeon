using System.Numerics;
using DelveRpg.Kit.World;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// Turns one tile grid into a retained static mesh: floor and ceiling quads
/// for open tiles and wall boxes for solids that touch open space. Geometry is
/// emitted in role blocks (floor, wall, water, door, ceiling) so each block can bind its own
/// material; UVs map every face into the role's atlas cell when local art is
/// present. Vertex colors carry the theme tint either way.
/// </summary>
public static class LevelMesh
{
    /// <summary>Material slot per tile role; slot order is the geometry order.</summary>
    public static readonly string[] Roles = ["floor", "wall", "water", "door", "ceiling"];

    public static int SlotFor(string role) => Array.IndexOf(Roles, role);

    /// <summary>
    /// One mesh with one <see cref="MeshGroup"/> per non-empty role, in slot
    /// order. <c>Groups</c> carries the role name alongside the group so the
    /// renderer can bind each block's material.
    /// </summary>
    public sealed record LevelGeometry(
        Vector3[] Positions,
        Vector3[] Normals,
        Vector2[] Uvs,
        Color[] Colors,
        uint[] Indices,
        IReadOnlyList<(string Role, MeshGroup Group)> Groups);
    // Theme tints multiply the tile textures (and stand alone as the
    // placeholder colors when no art is staged), so they sit near white and
    // lean the palette rather than shading it.
    private static readonly Dictionary<string, (float R, float G, float B)> Themes = new(StringComparer.Ordinal)
    {
        ["Sewer"] = (0.72f, 0.80f, 0.68f),
        ["Temple"] = (0.88f, 0.80f, 0.62f),
        ["Cave"] = (0.80f, 0.72f, 0.64f),
        ["Undead"] = (0.72f, 0.68f, 0.88f),
        ["Cold"] = (0.72f, 0.82f, 0.96f),
    };

    public static LevelGeometry Build(
        DungeonLevel level,
        string theme,
        Func<string, UvRect> rectFor)
    {
        (float r, float g, float b) = Themes.TryGetValue(theme, out (float R, float G, float B) palette)
            ? palette
            : (0.34f, 0.32f, 0.30f);

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<uint>();

        // Role blocks keep each material's geometry contiguous: slot order is
        // Roles[] and each block ends as one MeshGroup.
        var groups = new List<(string Role, MeshGroup Group)>();
        foreach (string role in Roles)
        {
            uint blockStart = (uint)indices.Count;
            UvRect rect = rectFor(role);
            foreach ((int x, int y, Tile tile) in TilesOf(level, role))
            {
                Color color = role switch
                {
                    "wall" => WallColor(tile, r, g, b),
                    "door" => WallColor(tile, r, g, b),
                    "ceiling" => Clamped(r * 0.8f, g * 0.8f, b * 0.82f),
                    _ => FloorColor(tile, r, g, b),
                };

                if (role is "wall" or "door")
                {
                    AddBox(positions, normals, uvs, colors, indices, x, y, 0f, 1f, color, rect);
                }
                else
                {
                    AddQuad(positions, normals, uvs, colors, indices, x, y, role == "ceiling", color, rect);
                }
            }

            uint blockCount = (uint)indices.Count - blockStart;
            if (blockCount > 0)
            {
                groups.Add((role, new MeshGroup((uint)SlotFor(role), blockStart, blockCount)));
            }
        }

        return new LevelGeometry(
            positions.ToArray(),
            normals.ToArray(),
            uvs.ToArray(),
            colors.ToArray(),
            indices.ToArray(),
            groups);
    }

    private static IEnumerable<(int X, int Y, Tile Tile)> TilesOf(DungeonLevel level, string role)
    {
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                Tile tile = level.At(x, y);
                bool match = role switch
                {
                    "wall" => tile.Kind == TileKind.Wall && TouchesOpen(level, x, y),
                    "door" => tile.Kind is TileKind.DoorClosed or TileKind.DoorLocked && TouchesOpen(level, x, y),
                    "floor" => !tile.BlocksMovement && tile.Kind != TileKind.Water,
                    "water" => tile.Kind == TileKind.Water,
                    "ceiling" => !tile.BlocksMovement,
                    _ => false,
                };

                if (match)
                {
                    yield return (x, y, tile);
                }
            }
        }
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
        TileKind.Water => Clamped(r * 0.5f, g * 0.6f, b * 1.2f),
        _ => new Color(r, g, b, 1f),
    };

    private static Color WallColor(Tile tile, float r, float g, float b) => tile.Kind switch
    {
        TileKind.DoorClosed => Clamped(r * 1.4f, g * 1.1f, b * 0.6f),
        TileKind.DoorLocked => Clamped(r * 0.9f, g * 0.75f, b * 0.6f),
        _ => Clamped(r * 0.62f, g * 0.62f, b * 0.66f),
    };

    /// <summary>The engine rejects vertex colors outside [0,1].</summary>
    private static Color Clamped(float r, float g, float b) =>
        new(Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(b, 0f, 1f), 1f);

    private static void AddQuad(
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<Color> colors,
        List<uint> indices,
        int x,
        int y,
        bool ceiling,
        Color color,
        UvRect rect)
    {
        // A floor quad sits at 0 facing up; a ceiling quad sits at the wall
        // top facing down, wound the other way so back-face culling keeps the
        // side seen from below.
        float height = ceiling ? 1f : 0f;
        uint start = (uint)positions.Count;
        positions.Add(new Vector3(x, height, y));
        positions.Add(new Vector3(x + 1, height, y));
        positions.Add(new Vector3(x + 1, height, y + 1));
        positions.Add(new Vector3(x, height, y + 1));
        for (int i = 0; i < 4; i++)
        {
            normals.Add(ceiling ? -Vector3.UnitY : Vector3.UnitY);
            colors.Add(color);
        }

        uvs.Add(new Vector2(rect.U0, rect.V0));
        uvs.Add(new Vector2(rect.U1, rect.V0));
        uvs.Add(new Vector2(rect.U1, rect.V1));
        uvs.Add(new Vector2(rect.U0, rect.V1));
        indices.AddRange(ceiling
            ? new uint[] { start, start + 1, start + 2, start, start + 2, start + 3 }
            : new uint[] { start, start + 2, start + 1, start, start + 3, start + 2 });
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
        Color color,
        UvRect rect)
    {
        Vector3 a = new(x, bottom, y);
        Vector3 b = new(x + 1, bottom, y);
        Vector3 c = new(x + 1, bottom, y + 1);
        Vector3 d = new(x, bottom, y + 1);
        Vector3 e = new(x, top, y);
        Vector3 f = new(x + 1, top, y);
        Vector3 g = new(x + 1, top, y + 1);
        Vector3 h = new(x, top, y + 1);

        AddFace(positions, normals, uvs, colors, indices, e, f, g, h, Vector3.UnitY, color, rect);
        AddFace(positions, normals, uvs, colors, indices, a, b, f, e, -Vector3.UnitZ, color, rect);
        AddFace(positions, normals, uvs, colors, indices, c, d, h, g, Vector3.UnitZ, color, rect);
        AddFace(positions, normals, uvs, colors, indices, b, c, g, f, Vector3.UnitX, color, rect);
        AddFace(positions, normals, uvs, colors, indices, d, a, e, h, -Vector3.UnitX, color, rect);
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
        Color color,
        UvRect rect)
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

        uvs.Add(new Vector2(rect.U0, rect.V0));
        uvs.Add(new Vector2(rect.U1, rect.V0));
        uvs.Add(new Vector2(rect.U1, rect.V1));
        uvs.Add(new Vector2(rect.U0, rect.V1));
        indices.AddRange(new uint[] { start, start + 2, start + 1, start, start + 3, start + 2 });
    }
}
