using System.Numerics;
using DelveRpg.Kit.World;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// Turns one tile grid into a retained static mesh: floor and ceiling quads
/// at each open tile's own heights, wall boxes for solids that touch open
/// space spanning their neighbours' floors to ceilings, and risers where
/// neighbouring floors or ceilings step. Geometry is
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
        Func<string, int, int, UvRect> rectFor,
        bool themedArt = false)
    {
        // Theme art carries the theme itself; the palette tint is for the
        // default sheet and the placeholder colors.
        (float r, float g, float b) = themedArt
            ? (1f, 1f, 1f)
            : Themes.TryGetValue(theme, out (float R, float G, float B) palette)
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
            foreach ((int x, int y, Tile tile) in TilesOf(level, role))
            {
                UvRect rect = rectFor(role, x, y);
                Color color = role switch
                {
                    "wall" => WallColor(tile, r, g, b),
                    "door" => WallColor(tile, r, g, b),
                    "ceiling" => Clamped(r * 0.8f, g * 0.8f, b * 0.82f),
                    _ => FloorColor(tile, r, g, b),
                };

                var mesh = new MeshLists(positions, normals, uvs, colors, indices);
                switch (role)
                {
                    case "wall" when tile.Kind == TileKind.Wall:
                        (float bottom, float top) = NeighbourSpan(level, x, y);
                        AddBox(mesh, x, y, bottom, top, color, rect);
                        break;
                    case "wall":
                        AddRisers(level, mesh, x, y, color, rect);
                        break;
                    case "door":
                        // A closed door stands one tile tall on its floor; a
                        // lintel of wall fills the span above it.
                        float doorFloor = level.FloorHeight(x, y);
                        AddBox(mesh, x, y, doorFloor, doorFloor + 1f, color, rect);
                        break;
                    case "ceiling":
                        AddQuad(mesh, x, y, level.CeilingHeight(x, y), ceiling: true, color, rect);
                        break;
                    default:
                        AddQuad(mesh, x, y, VisualFloor(level, x, y), ceiling: false, color, rect);
                        break;
                }
            }

            if (role == "wall")
            {
                // Door lintels: wall from the door top to the highest ceiling beside it.
                foreach ((int x, int y, Tile tile) in TilesOf(level, "door"))
                {
                    float top = NeighbourSpan(level, x, y).Top;
                    float doorTop = level.FloorHeight(x, y) + 1f;
                    if (top > doorTop)
                    {
                        AddBox(new MeshLists(positions, normals, uvs, colors, indices), x, y, doorTop, top, WallColor(Tile.Wall, r, g, b), rectFor(role, x, y));
                    }
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
                    "wall" => tile.Kind == TileKind.Wall ? TouchesOpen(level, x, y) : !tile.BlocksMovement,
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

    /// <summary>The lists one mesh is built into.</summary>
    private readonly record struct MeshLists(
        List<Vector3> Positions,
        List<Vector3> Normals,
        List<Vector2> Uvs,
        List<Color> Colors,
        List<uint> Indices);

    /// <summary>Water shows its surface a little below the floor around it; wading stands lower still.</summary>
    private const float WaterSurfaceDrop = 0.1f;

    private static readonly (int X, int Y)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private static float VisualFloor(DungeonLevel level, int x, int y) =>
        level.FloorHeight(x, y) - (level.At(x, y).Kind == TileKind.Water ? WaterSurfaceDrop : 0f);

    /// <summary>The lowest floor and highest ceiling among a solid's open neighbours.</summary>
    private static (float Bottom, float Top) NeighbourSpan(DungeonLevel level, int x, int y)
    {
        float bottom = float.MaxValue;
        float top = float.MinValue;
        foreach ((int dx, int dy) in Sides)
        {
            if (level.InBounds(x + dx, y + dy) && !level.At(x + dx, y + dy).BlocksMovement)
            {
                bottom = Math.Min(bottom, VisualFloor(level, x + dx, y + dy));
                top = Math.Max(top, level.CeilingHeight(x + dx, y + dy));
            }
        }

        return bottom == float.MaxValue ? (0f, 1f) : (bottom, top);
    }

    /// <summary>
    /// The faces between an open tile and a lower floor or a higher ceiling
    /// beside it, each facing the neighbour that sees it.
    /// </summary>
    private static void AddRisers(DungeonLevel level, MeshLists mesh, int x, int y, Color color, UvRect rect)
    {
        float floor = VisualFloor(level, x, y);
        float ceiling = level.CeilingHeight(x, y);
        foreach ((int dx, int dy) in Sides)
        {
            if (!level.InBounds(x + dx, y + dy) || level.At(x + dx, y + dy).BlocksMovement)
            {
                continue;
            }

            float otherFloor = VisualFloor(level, x + dx, y + dy);
            if (otherFloor < floor - 0.001f)
            {
                AddSide(mesh, x, y, dx, dy, otherFloor, floor, color, rect);
            }

            float otherCeiling = level.CeilingHeight(x + dx, y + dy);
            if (otherCeiling > ceiling + 0.001f)
            {
                AddSide(mesh, x, y, dx, dy, ceiling, otherCeiling, color, rect);
            }
        }
    }

    private static void AddQuad(MeshLists mesh, int x, int y, float height, bool ceiling, Color color, UvRect rect)
    {
        // A floor quad faces up; a ceiling quad faces down, wound the other
        // way so back-face culling keeps the side seen from below.
        uint start = (uint)mesh.Positions.Count;
        mesh.Positions.Add(new Vector3(x, height, y));
        mesh.Positions.Add(new Vector3(x + 1, height, y));
        mesh.Positions.Add(new Vector3(x + 1, height, y + 1));
        mesh.Positions.Add(new Vector3(x, height, y + 1));
        for (int i = 0; i < 4; i++)
        {
            mesh.Normals.Add(ceiling ? -Vector3.UnitY : Vector3.UnitY);
            mesh.Colors.Add(color);
        }

        mesh.Uvs.Add(new Vector2(rect.U0, rect.V0));
        mesh.Uvs.Add(new Vector2(rect.U1, rect.V0));
        mesh.Uvs.Add(new Vector2(rect.U1, rect.V1));
        mesh.Uvs.Add(new Vector2(rect.U0, rect.V1));
        mesh.Indices.AddRange(ceiling
            ? new uint[] { start, start + 1, start + 2, start, start + 2, start + 3 }
            : new uint[] { start, start + 2, start + 1, start, start + 3, start + 2 });
    }

    private static void AddBox(MeshLists mesh, int x, int y, float bottom, float top, Color color, UvRect rect)
    {
        foreach ((int dx, int dy) in Sides)
        {
            AddSide(mesh, x, y, dx, dy, bottom, top, color, rect);
        }

        AddQuad(mesh, x, y, top, ceiling: false, color, rect);
    }

    /// <summary>
    /// One vertical side of tile (x, y), facing (dx, dy), from bottom to top,
    /// in tile-tall slices so the texture repeats rather than stretches.
    /// </summary>
    private static void AddSide(MeshLists mesh, int x, int y, int dx, int dy, float bottom, float top, Color color, UvRect rect)
    {
        // Corners of the side seen from outside, left then right.
        (Vector2 left, Vector2 right) = (dx, dy) switch
        {
            (0, -1) => (new Vector2(x, y), new Vector2(x + 1, y)),
            (0, 1) => (new Vector2(x + 1, y + 1), new Vector2(x, y + 1)),
            (1, 0) => (new Vector2(x + 1, y), new Vector2(x + 1, y + 1)),
            _ => (new Vector2(x, y + 1), new Vector2(x, y)),
        };
        var normal = new Vector3(dx, 0f, dy);
        for (float from = bottom; from < top - 0.001f; from += 1f)
        {
            float to = Math.Min(top, from + 1f);
            float v1 = rect.V0 + ((rect.V1 - rect.V0) * (to - from));
            uint start = (uint)mesh.Positions.Count;
            mesh.Positions.Add(new Vector3(left.X, from, left.Y));
            mesh.Positions.Add(new Vector3(right.X, from, right.Y));
            mesh.Positions.Add(new Vector3(right.X, to, right.Y));
            mesh.Positions.Add(new Vector3(left.X, to, left.Y));
            for (int i = 0; i < 4; i++)
            {
                mesh.Normals.Add(normal);
                mesh.Colors.Add(color);
            }

            mesh.Uvs.Add(new Vector2(rect.U0, rect.V0));
            mesh.Uvs.Add(new Vector2(rect.U1, rect.V0));
            mesh.Uvs.Add(new Vector2(rect.U1, v1));
            mesh.Uvs.Add(new Vector2(rect.U0, v1));
            mesh.Indices.AddRange(new uint[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
    }
}
