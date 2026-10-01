using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// The sprite art manifest. Members with a default are settable: the source-generated reader keeps
/// an initializer only on a settable property and leaves an omitted init-only
/// member null or zero.
/// </summary>
public sealed class DelveSpriteManifest
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    /// <summary>Content path prefix of the gitignored import art staging area.</summary>
    [JsonPropertyName("artContentPrefix")]
    public string ArtContentPrefix { get; set; } = string.Empty;

    [JsonPropertyName("atlases")]
    public Dictionary<string, DelveArtAtlas> Atlases { get; set; } = new();

    [JsonPropertyName("sprites")]
    public Dictionary<string, DelveSpriteDefinition> Sprites { get; set; } = new();

    [JsonPropertyName("lighting")]
    public DelveSpriteLighting Lighting { get; set; } = new();
}

/// <summary>
/// How sprites respond to scene lights. <c>unlit</c> shows the raw cell;
/// <c>derived-gradient</c> bumps the cell's own red channel in the shader;
/// <c>authored-normal</c> reads each sheet's derived normal sheet;
/// <c>synthetic</c> is the Engine's dome over the sprite UVs.
/// </summary>
public sealed class DelveSpriteLighting
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "unlit";

    [JsonPropertyName("strength")]
    public float Strength { get; set; } = 1f;

    public SpriteLightingMode EngineMode => Mode switch
    {
        "derived-gradient" => SpriteLightingMode.DerivedGradient,
        "authored-normal" => SpriteLightingMode.AuthoredNormal,
        "synthetic" => SpriteLightingMode.Synthetic,
        "unlit" => SpriteLightingMode.Unlit,
        _ => throw new InvalidOperationException($"Sprite lighting mode '{Mode}' is not one of unlit, derived-gradient, authored-normal, synthetic."),
    };
}

public sealed class DelveSpriteDefinition
{
    [JsonPropertyName("atlas")]
    public string Atlas { get; set; } = string.Empty;

    /// <summary>Resting cell: column + row * columns, like the donor's atlas index.</summary>
    [JsonPropertyName("frame")]
    public int Frame { get; init; }

    /// <summary>World height and width of the cell, in tiles.</summary>
    [JsonPropertyName("size")]
    public float Size { get; set; } = 1f;

    /// <summary>Height the sprite's base floats above the floor, in tiles.</summary>
    [JsonPropertyName("lift")]
    public float Lift { get; init; }

    /// <summary>
    /// Extra roll, in degrees, that stands a held weapon's cell upright in the
    /// hand (sheet art is drawn at different diagonals).
    /// </summary>
    [JsonPropertyName("heldRoll")]
    public float HeldRoll { get; init; }

    [JsonPropertyName("walk")]
    public DelveSpriteAnimation? Walk { get; init; }

    [JsonPropertyName("attack")]
    public DelveSpriteAnimation? Attack { get; init; }
}

/// <summary>
/// A donor sprite animation: cells <c>start..end</c> played over
/// <c>speed</c> ticks for the whole sequence (donor SpriteAnimation).
/// </summary>
public sealed class DelveSpriteAnimation
{
    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }

    [JsonPropertyName("speed")]
    public int Speed { get; set; } = 30;

    /// <summary>The cell shown <paramref name="ticks"/> into the sequence; loops.</summary>
    public int FrameAt(long ticks)
    {
        int count = Math.Max(1, End + 1 - Start);
        int speed = Math.Max(1, Speed);
        long position = ((ticks % speed) + speed) % speed;
        return Start + (int)(position * count / speed);
    }
}

/// <summary>A sprite id resolved against a loaded sheet and its lighting material.</summary>
public sealed record DelveSprite(SpriteAtlas Atlas, DelveSpriteDefinition Definition, SpriteMaterialDescriptor Material);

/// <summary>
/// Donor sprite sheets for actors, ground items and the held weapon. The
/// manifest is authored content; the pixels are operator-extracted donor art
/// staged under the gitignored content import area and never committed. Each
/// sheet becomes one Engine sprite atlas whose frame ids are the donor cell
/// indices. Missing manifest, sheet, or cell degrades to no sprite, and the
/// renderer keeps its placeholder plate for that id.
/// </summary>
public sealed class DelveSpriteAssets : IDisposable
{
    public const string ManifestPath = "delve/art/sprites.json";

    /// <summary>
    /// Pixel-art cutout (binary alpha, depth-written) under the manifest's
    /// lighting. An authored-normal sheet without a staged normal sheet is
    /// drawn unlit rather than with a missing texture.
    /// </summary>
    public static SpriteMaterialDescriptor CutoutMaterial(DelveSpriteLighting lighting, RenderResource? normals)
    {
        SpriteLightingMode mode = lighting.EngineMode;
        if (mode == SpriteLightingMode.AuthoredNormal && normals is null)
        {
            mode = SpriteLightingMode.Unlit;
        }

        return new SpriteMaterialDescriptor(
            mode,
            normals is null ? default : (RenderResourceReference)normals,
            default,
            lighting.Strength,
            0f,
            SpriteAlphaMode.Mask,
            0.5f,
            SpriteShadowPolicy.None);
    }

    private readonly Dictionary<string, DelveSprite> _sprites = new(StringComparer.Ordinal);
    private readonly List<SpriteAtlas> _atlases = new();
    private readonly List<RenderResource> _textures = new();
    private bool _disposed;

    private DelveSpriteAssets()
    {
    }

    /// <summary>The loaded sprite for a content sprite id, or null.</summary>
    public DelveSprite? SpriteFor(string spriteId) =>
        _sprites.TryGetValue(spriteId, out DelveSprite? sprite) ? sprite : null;

    public static DelveSpriteAssets Load(IEngineContext engine, Func<string, string?> readText, Func<string, bool> contentExists)
    {
        var assets = new DelveSpriteAssets();
        string? manifestText = readText(ManifestPath);
        if (manifestText is null)
        {
            return assets;
        }

        DelveSpriteManifest manifest = JsonSerializer.Deserialize(
            manifestText, DelveSpriteJsonContext.Default.DelveSpriteManifest)!;

        var atlases = new Dictionary<string, SpriteAtlas>(StringComparer.Ordinal);
        var materials = new Dictionary<string, SpriteMaterialDescriptor>(StringComparer.Ordinal);
        bool wantsNormals = manifest.Lighting.EngineMode == SpriteLightingMode.AuthoredNormal;
        foreach ((string name, DelveArtAtlas sheet) in manifest.Atlases)
        {
            if (sheet.Columns <= 0 || sheet.Rows <= 0)
            {
                continue;
            }

            string contentPath = PathIn(manifest, name);
            if (!contentExists(contentPath))
            {
                continue;
            }

            RenderResource? colour = OpenSheet(engine, contentPath);
            if (colour is null)
            {
                continue;
            }

            assets._textures.Add(colour);
            RenderResource? normals = null;
            if (wantsNormals && sheet.Normals is string normalsName)
            {
                string normalsPath = PathIn(manifest, normalsName);
                normals = contentExists(normalsPath) ? OpenSheet(engine, normalsPath) : null;
                if (normals is not null)
                {
                    assets._textures.Add(normals);
                }
            }

            SpriteAtlas atlas = engine.Graphics.CreateSpriteAtlas(
                new SpriteAtlasCreateRequest(colour, CellFrames(sheet)));
            assets._atlases.Add(atlas);
            atlases[name] = atlas;
            materials[name] = CutoutMaterial(manifest.Lighting, normals);
        }

        foreach ((string id, DelveSpriteDefinition definition) in manifest.Sprites)
        {
            if (atlases.TryGetValue(definition.Atlas, out SpriteAtlas? atlas)
                && manifest.Atlases.TryGetValue(definition.Atlas, out DelveArtAtlas? sheet)
                && InSheet(sheet, definition))
            {
                assets._sprites[id] = new DelveSprite(atlas, definition, materials[definition.Atlas]);
            }
        }

        return assets;
    }

    private static string PathIn(DelveSpriteManifest manifest, string name) =>
        string.IsNullOrEmpty(manifest.ArtContentPrefix) ? name : $"{manifest.ArtContentPrefix.TrimEnd('/')}/{name}";

    /// <summary>Open one staged sheet as a nearest-filtered texture, or null when it is unusable.</summary>
    private static RenderResource? OpenSheet(IEngineContext engine, string contentPath)
    {
        RenderResourceInfo info;
        try
        {
            info = engine.Graphics.OpenResource(
                new RenderResourceRequest(contentPath, TextureFilter.Nearest, TextureWrap.Clamp));
        }
        catch (EngineCallException exception)
        {
            throw new InvalidOperationException(
                $"Sprite sheet '{contentPath}' was refused by the Engine (re-stage it with scripts/extract-delver-reference.sh): {exception.Message}",
                exception);
        }

        return info.Kind == RenderResourceKind.Texture && info.ByteLength > 0 && info.Handle.Handle.Value != 0
            ? info.Handle
            : null;
    }

    /// <summary>
    /// One frame per cell, frame id = cell index. Sheet rows run top-down and
    /// V zero is the sheet's top row, as for the tile atlases. Each cell is
    /// inset by a tenth of a percent, like the donor's atlas regions, so
    /// nearest sampling never picks up the neighbouring cell.
    /// </summary>
    public static SpriteAtlasFrame[] CellFrames(DelveArtAtlas sheet)
    {
        var frames = new SpriteAtlasFrame[sheet.Columns * sheet.Rows];
        float cellU = 1f / sheet.Columns;
        float cellV = 1f / sheet.Rows;
        float insetU = cellU * 0.001f;
        float insetV = cellV * 0.001f;
        for (int index = 0; index < frames.Length; index++)
        {
            int column = index % sheet.Columns;
            int row = index / sheet.Columns;
            frames[index] = new SpriteAtlasFrame(
                (uint)index,
                new Vector2((column * cellU) + insetU, (row * cellV) + insetV),
                new Vector2(((column + 1) * cellU) - insetU, ((row + 1) * cellV) - insetV),
                false,
                Vector2.Zero);
        }

        return frames;
    }

    private static bool InSheet(DelveArtAtlas sheet, DelveSpriteDefinition definition)
    {
        int cells = sheet.Columns * sheet.Rows;
        bool Fits(int cell) => cell >= 0 && cell < cells;
        return Fits(definition.Frame)
            && (definition.Walk is null || (Fits(definition.Walk.Start) && Fits(definition.Walk.End)))
            && (definition.Attack is null || (Fits(definition.Attack.Start) && Fits(definition.Attack.End)));
    }

    /// <summary>
    /// Releases the atlases, then their sheets. Every sprite appearance made
    /// from an atlas must already be out of the published scene and disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sprites.Clear();
        foreach (SpriteAtlas atlas in _atlases)
        {
            atlas.Dispose();
        }

        _atlases.Clear();
        foreach (RenderResource texture in _textures)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }
}

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(DelveSpriteManifest))]
public sealed partial class DelveSpriteJsonContext : JsonSerializerContext;
