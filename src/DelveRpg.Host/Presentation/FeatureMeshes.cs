using System.Numerics;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// Authored geometry for floor features that the donor draws as meshes we
/// do not import ([donor] meshes/spikes.obj): a 3 × 3 bed of steel
/// pyramids in a tile, rising out of the floor.
/// </summary>
public static class FeatureMeshes
{
    public const float SpikeHeight = 0.35f;

    public static MeshResourceCreateRequest Spikes(Material material)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<uint>();
        var steel = new Color(0.62f, 0.64f, 0.68f, 1f);
        const float half = 0.07f;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                Vector3 centre = new(-0.27f + (column * 0.27f), 0f, -0.27f + (row * 0.27f));
                Vector3 tip = centre + new Vector3(0f, SpikeHeight, 0f);
                Vector3[] corners =
                [
                    centre + new Vector3(-half, 0f, -half),
                    centre + new Vector3(half, 0f, -half),
                    centre + new Vector3(half, 0f, half),
                    centre + new Vector3(-half, 0f, half),
                ];
                for (int side = 0; side < 4; side++)
                {
                    Vector3 a = corners[side];
                    Vector3 b = corners[(side + 1) % 4];
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(tip - a, b - a));
                    uint start = (uint)positions.Count;
                    foreach (Vector3 vertex in new[] { a, tip, b })
                    {
                        positions.Add(vertex);
                        normals.Add(normal);
                        uvs.Add(Vector2.Zero);
                        colors.Add(steel);
                    }

                    indices.AddRange([start, start + 1, start + 2]);
                }
            }
        }

        return new MeshResourceCreateRequest(
            positions.ToArray(),
            normals.ToArray(),
            uvs.ToArray(),
            colors.ToArray(),
            indices.ToArray(),
            new[] { new MeshGroup(0, 0, (uint)indices.Count) },
            new[] { new MeshMaterialBinding(0, material) });
    }
}
