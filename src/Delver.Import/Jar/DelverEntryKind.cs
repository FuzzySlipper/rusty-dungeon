namespace Delver.Import.Jar;

/// <summary>Reference-relevant categories of donor archive entries.</summary>
public enum DelverEntryKind
{
    /// <summary>Content documents: <c>.dat</c> (outside levels/generator rooms), <c>.json</c>, <c>.atlas</c>.</summary>
    Data,

    /// <summary>Kryo level data under <c>levels/</c>: <c>.bin</c> and <c>.dat</c>.</summary>
    Level,

    /// <summary>Kryo room templates under <c>generator/&lt;Theme&gt;/&lt;Rooms&gt;/</c>.</summary>
    RoomTemplate,

    /// <summary>Textures: <c>.png</c>.</summary>
    Texture,

    /// <summary>Audio: <c>.mp3</c>.</summary>
    Audio,

    /// <summary>Meshes: <c>.obj</c>.</summary>
    Mesh,

    /// <summary>Shaders: <c>.vert</c>, <c>.frag</c>, <c>.glsl</c>.</summary>
    Shader,

    /// <summary>Bitmap fonts: <c>.fnt</c>.</summary>
    Font,

    /// <summary>Compiled Java classes: <c>.class</c>.</summary>
    Class,

    /// <summary>Native libraries: <c>.dll</c>, <c>.so</c>, <c>.dylib</c>, <c>.jnilib</c>.</summary>
    Native,

    /// <summary>Anything else (manifests, maven metadata, sources, directories).</summary>
    Other,
}
