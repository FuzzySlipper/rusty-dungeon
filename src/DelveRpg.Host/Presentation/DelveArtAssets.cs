using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// One tile role's cell in a local texture atlas. Cell coordinates are the
/// manifest's (column, row) from the top-left of a fixed-grid sheet.
/// </summary>
public readonly record struct ArtTileCell(string Atlas, int Column, int Row, Color Tint);

public sealed class DelveArtManifest
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = string.Empty;

    /// <summary>Content path prefix of the gitignored import art staging area.</summary>
    [JsonPropertyName("artContentPrefix")]
    public string ArtContentPrefix { get; init; } = string.Empty;

    [JsonPropertyName("atlases")]
    public Dictionary<string, DelveArtAtlas> Atlases { get; init; } = new();

    [JsonPropertyName("tiles")]
    public Dictionary<string, DelveArtTile> Tiles { get; init; } = new();
}

public sealed class DelveArtAtlas
{
    [JsonPropertyName("columns")]
    public int Columns { get; init; }

    [JsonPropertyName("rows")]
    public int Rows { get; init; }
}

public sealed class DelveArtTile
{
    [JsonPropertyName("atlas")]
    public string Atlas { get; init; } = string.Empty;

    [JsonPropertyName("column")]
    public int Column { get; init; }

    [JsonPropertyName("row")]
    public int Row { get; init; }

    [JsonPropertyName("tint")]
    public float[]? Tint { get; init; }
}

/// <summary>
/// Imported art for the level mesh: the manifest is authored content, the
/// pixels are operator-extracted donor art staged under the gitignored
/// content import area and never committed (Engine textures open from product
/// content only). With no manifest or no staged art the level keeps its
/// authored placeholder colors and nothing here is created.
/// </summary>
public sealed class DelveArtAssets : IDisposable
{
    public const string ManifestPath = "delve/art/tiles.json";

    private static readonly Dictionary<string, UvRect> FallbackRects = new(StringComparer.Ordinal)
    {
        ["floor"] = new UvRect(0f, 0f, 1f, 1f),
        ["wall"] = new UvRect(0f, 0f, 1f, 1f),
        ["water"] = new UvRect(0f, 0f, 1f, 1f),
        ["door"] = new UvRect(0f, 0f, 1f, 1f),
        ["ceiling"] = new UvRect(0f, 0f, 1f, 1f),
    };

    private readonly Dictionary<string, UvRect> _rects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Material> _materials = new(StringComparer.Ordinal);
    private readonly List<RenderResource> _textures = new();
    private readonly List<Material> _ownedMaterials = new();
    private bool _disposed;

    private DelveArtAssets()
    {
    }

    /// <summary>Atlas cell rect for a role; the full unit square without art.</summary>
    public UvRect RectFor(string role) =>
        _rects.TryGetValue(role, out UvRect rect) ? rect : FallbackRects[role];

    /// <summary>Textured material for a role, or null when no art covers it.</summary>
    public Material? MaterialFor(string role) =>
        _materials.TryGetValue(role, out Material? material) ? material : null;

    /// <summary>
    /// Load the manifest through the content snapshot and open the art it
    /// names from the staged import area. Any absence degrades to no
    /// materials; the engine only ever sees content paths.
    /// </summary>
    public static DelveArtAssets Load(IEngineContext engine, Func<string, string?> readText, Func<string, bool> contentExists)
    {
        var assets = new DelveArtAssets();
        string? manifestText = readText(ManifestPath);
        if (manifestText is null)
        {
            return assets;
        }

        DelveArtManifest manifest = JsonSerializer.Deserialize(
            manifestText, DelveArtJsonContext.Default.DelveArtManifest)!;

        var open = new Dictionary<string, RenderResource>(StringComparer.Ordinal);
        foreach ((string role, DelveArtTile tile) in manifest.Tiles)
        {
            if (!manifest.Atlases.TryGetValue(tile.Atlas, out DelveArtAtlas? atlas)
                || atlas.Columns <= 0
                || atlas.Rows <= 0
                || tile.Column < 0
                || tile.Column >= atlas.Columns
                || tile.Row < 0
                || tile.Row >= atlas.Rows)
            {
                continue;
            }

            string contentPath = string.IsNullOrEmpty(manifest.ArtContentPrefix)
                ? tile.Atlas
                : $"{manifest.ArtContentPrefix.TrimEnd('/')}/{tile.Atlas}";
            if (!contentExists(contentPath))
            {
                continue;
            }

            if (!open.TryGetValue(tile.Atlas, out RenderResource? texture))
            {
                RenderResourceInfo info = engine.Graphics.OpenResource(
                    new RenderResourceRequest(contentPath, TextureFilter.Nearest, TextureWrap.Clamp));
                if (info.Kind != RenderResourceKind.Texture || info.ByteLength == 0 || info.Handle.Handle.Value == 0)
                {
                    continue;
                }


                texture = info.Handle;
                assets._textures.Add(texture);
                open[tile.Atlas] = texture;
            }

            // Sheet rows run top-down and this renderer's V axis matches the
            // sheet: row zero is V zero (verified against the staged art).
            float u0 = tile.Column / (float)atlas.Columns;
            float u1 = (tile.Column + 1) / (float)atlas.Columns;
            float v0 = tile.Row / (float)atlas.Rows;
            float v1 = (tile.Row + 1) / (float)atlas.Rows;
            assets._rects[role] = new UvRect(u0, v0, u1, v1);

            float[] tint = tile.Tint is { Length: 4 } ? tile.Tint : [1f, 1f, 1f, 1f];
            var material = engine.Graphics.CreateMaterial(new MaterialRequest(
                new Color(tint[0], tint[1], tint[2], tint[3]),
                texture,
                0.9f,
                new Color(1f, 1f, 1f, 1f),
                Vector3.Zero,
                0f,
                false));
            assets._ownedMaterials.Add(material);
            assets._materials[role] = material;
        }

        return assets;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (Material material in _ownedMaterials)
        {
            material.Dispose();
        }

        _ownedMaterials.Clear();
        _materials.Clear();
        foreach (RenderResource texture in _textures)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }
}

/// <summary>A sub-rectangle of a texture, in mesh UV space.</summary>
public readonly record struct UvRect(float U0, float V0, float U1, float V1);

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(DelveArtManifest))]
public sealed partial class DelveArtJsonContext : JsonSerializerContext;
