namespace Delver.Import.Jar;

/// <summary>
/// Classifies donor archive paths by extension and location. Path layout is
/// from the shipped delver.jar: Kryo room templates live under
/// <c>generator/&lt;Theme&gt;/&lt;Rooms&gt;/</c> (4+ segments), level data under
/// <c>levels/</c>, content documents elsewhere.
/// </summary>
public static class DelverEntryClassifier
{
    public static DelverEntryKind Classify(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string normalized = path.Replace('\\', '/').TrimStart('/');
        string extension = Path.GetExtension(normalized).ToLowerInvariant();
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        switch (extension)
        {
            case ".class":
                return DelverEntryKind.Class;
            case ".dll":
            case ".so":
            case ".dylib":
            case ".jnilib":
                return DelverEntryKind.Native;
            case ".mp3":
                return DelverEntryKind.Audio;
            case ".obj":
                return DelverEntryKind.Mesh;
            case ".png":
                return DelverEntryKind.Texture;
            case ".vert":
            case ".frag":
            case ".glsl":
                return DelverEntryKind.Shader;
            case ".fnt":
                return DelverEntryKind.Font;
        }

        if (extension is ".bin" or ".dat")
        {
            if (segments.Length > 0 && segments[0] == "levels")
            {
                return DelverEntryKind.Level;
            }

            if (segments.Length > 0 && segments[0] == "generator")
            {
                // generator/<Theme>/info.dat and section.dat are content docs;
                // deeper files (generator/Cave/Halls/1.dat) are room templates.
                return segments.Length >= 4 ? DelverEntryKind.RoomTemplate : DelverEntryKind.Data;
            }

            return extension == ".dat" ? DelverEntryKind.Data : DelverEntryKind.Other;
        }

        if (extension is ".json" or ".atlas")
        {
            return DelverEntryKind.Data;
        }

        return DelverEntryKind.Other;
    }
}
