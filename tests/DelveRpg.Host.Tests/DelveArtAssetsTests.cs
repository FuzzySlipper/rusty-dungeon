using System.Numerics;
using DelveRpg.Host.Presentation;
using Rusty.Engine;
using Xunit;

namespace DelveRpg.Host.Tests;

public sealed class DelveArtAssetsTests
{
    private static string WithEmptyArtRoot(Action<string> body)
    {
        string root = Path.Combine(Path.GetTempPath(), $"delve-art-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            body(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return root;
    }

    [Fact]
    public void No_manifest_degrades_to_placeholder_colors()
    {
        // The engine is never touched on the fallback paths: this product
        // must run identically with no donor art present.
        DelveArtAssets assets = DelveArtAssets.Load(null!, _ => null, _ => false);

        Assert.Null(assets.MaterialFor("floor"));
        Assert.Null(assets.MaterialFor("wall"));
        Assert.Equal(new UvRect(0f, 0f, 1f, 1f), assets.RectFor("floor"));
    }

    [Fact]
    public void A_manifest_with_no_art_files_stays_untextured()
    {
        WithEmptyArtRoot(root =>
        {
            string previous = Environment.GetEnvironmentVariable("DELVE_ART_ROOT") ?? string.Empty;
            Environment.SetEnvironmentVariable("DELVE_ART_ROOT", root);
            try
            {
                DelveArtAssets assets = DelveArtAssets.Load(null!, path => path switch
                {
                    DelveArtAssets.ManifestPath => """
                        {
                          "schema": "delve.art.tiles.v1",
                          "artContentPrefix": "delve/imports/art",
                          "atlases": { "textures.png": { "columns": 4, "rows": 16 } },
                          "tiles": {
                            "floor": { "atlas": "textures.png", "column": 0, "row": 9, "tint": [1, 1, 1, 1] }
                          }
                        }
                        """,
                    _ => null,
                }, _ => false);

                Assert.Null(assets.MaterialFor("floor"));
                Assert.Equal(new UvRect(0f, 0f, 1f, 1f), assets.RectFor("floor"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("DELVE_ART_ROOT", string.IsNullOrEmpty(previous) ? null : previous);
            }
        });
    }

    [Fact]
    public void The_manifest_ignores_cells_outside_their_atlas()
    {
        WithEmptyArtRoot(root =>
        {
            string previous = Environment.GetEnvironmentVariable("DELVE_ART_ROOT") ?? string.Empty;
            Environment.SetEnvironmentVariable("DELVE_ART_ROOT", root);
            try
            {
                DelveArtAssets assets = DelveArtAssets.Load(null!, _ => """
                    {
                      "schema": "delve.art.tiles.v1",
                      "artContentPrefix": "delve/imports/art",
                      "atlases": { "textures.png": { "columns": 4, "rows": 16 } },
                      "tiles": {
                        "wall": { "atlas": "textures.png", "column": 9, "row": 9, "tint": [1, 1, 1, 1] }
                      }
                    }
                    """, _ => true);

                Assert.Null(assets.MaterialFor("wall"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("DELVE_ART_ROOT", string.IsNullOrEmpty(previous) ? null : previous);
            }
        });
    }
}

public sealed class LevelMeshTests
{
    private static readonly UvRect Cell = new(0.25f, 0.25f, 0.5f, 0.5f);

    private static DelveRpg.Kit.World.DungeonLevel SmallLevel()
    {
        var level = new DelveRpg.Kit.World.DungeonLevel(5, 5, 1, "Test");
        for (int y = 1; y < 4; y++)
        {
            for (int x = 1; x < 4; x++)
            {
                level.Set(x, y, DelveRpg.Kit.World.Tile.Floor);
            }
        }

        level.Set(2, 2, DelveRpg.Kit.World.Tile.Water);
        level.Set(3, 2, DelveRpg.Kit.World.Tile.DoorClosed);
        return level;
    }

    [Fact]
    public void Role_blocks_cover_every_index_exactly_once()
    {
        LevelMesh.LevelGeometry geometry = LevelMesh.Build(SmallLevel(), "Test", _ => Cell);

        uint covered = 0;
        int expectedStart = 0;
        foreach ((string role, MeshGroup group) in geometry.Groups)
        {
            Assert.Equal((uint)LevelMesh.SlotFor(role), group.MaterialSlot);
            Assert.Equal((uint)expectedStart, group.Start);
            covered += group.Count;
            expectedStart += (int)group.Count;
        }

        Assert.Equal((uint)geometry.Indices.Length, covered);
        Assert.Contains(geometry.Groups, entry => entry.Role == "floor");
        Assert.Contains(geometry.Groups, entry => entry.Role == "wall");
        Assert.Contains(geometry.Groups, entry => entry.Role == "water");
        Assert.Contains(geometry.Groups, entry => entry.Role == "door");
        Assert.Contains(geometry.Groups, entry => entry.Role == "ceiling");
    }

    [Fact]
    public void Ceiling_quads_sit_at_wall_height_and_face_down()
    {
        LevelMesh.LevelGeometry geometry = LevelMesh.Build(SmallLevel(), "Test", _ => Cell);
        MeshGroup ceiling = geometry.Groups.Single(entry => entry.Role == "ceiling").Group;

        for (uint i = ceiling.Start; i < ceiling.Start + ceiling.Count; i += 3)
        {
            Vector3 a = geometry.Positions[geometry.Indices[i]];
            Vector3 b = geometry.Positions[geometry.Indices[i + 1]];
            Vector3 c = geometry.Positions[geometry.Indices[i + 2]];
            Assert.Equal(1f, a.Y);
            Assert.Equal(-Vector3.UnitY, geometry.Normals[geometry.Indices[i]]);

            // Counter-clockwise seen from below: the winding's normal points down.
            Assert.True(Vector3.Cross(b - a, c - a).Y < 0f);
        }
    }

    [Fact]
    public void Face_uvs_land_inside_the_roles_atlas_cell()
    {
        LevelMesh.LevelGeometry geometry = LevelMesh.Build(SmallLevel(), "Test", _ => Cell);

        Assert.NotEmpty(geometry.Uvs);
        foreach (Vector2 uv in geometry.Uvs)
        {
            Assert.InRange(uv.X, Cell.U0, Cell.U1);
            Assert.InRange(uv.Y, Cell.V0, Cell.V1);
        }
    }
}
