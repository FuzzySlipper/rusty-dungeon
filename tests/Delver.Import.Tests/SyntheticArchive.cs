using System.IO.Compression;
using System.Text;

namespace Delver.Import.Tests;

/// <summary>Builds in-memory jar/zip fixtures; tests never touch real game files.</summary>
internal static class SyntheticArchive
{
    public static MemoryStream CreateZip(params (string Path, string Content)[] entries)
    {
        MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, string content) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(path);
                using Stream output = entry.Open();
                output.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        stream.Position = 0;
        return stream;
    }

    public static void WithTempDirectory(Action<string> body)
    {
        string directory = Directory.CreateTempSubdirectory("delver-import-tests-").FullName;
        try
        {
            body(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
