using System.IO.Compression;
using System.Text;

namespace Delver.Import.Jar;

/// <summary>
/// Read-only view of a donor jar/zip (delver.jar, DelvEdit.jar) built on
/// <see cref="ZipArchive"/>. Entries are enumerated in ordinal path order so
/// every downstream listing and extraction is deterministic. Only offline
/// reading and extraction: no engine types, no Java interop.
/// </summary>
public sealed class DelverArchive : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Stream? _ownedStream;
    private readonly List<(DelverEntry Entry, ZipArchiveEntry Source)> _entries;
    private readonly List<DelverEntry> _entryList;

    /// <summary>Opens a jar/zip file.</summary>
    public static DelverArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return new DelverArchive(stream, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Reads an archive from an open stream.</summary>
    public DelverArchive(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _ownedStream = leaveOpen ? null : stream;
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);

        List<(DelverEntry, ZipArchiveEntry)> entries = new();
        foreach (ZipArchiveEntry source in _zip.Entries)
        {
            string path = source.FullName.Replace('\\', '/').TrimStart('/');
            bool isDirectory = path.Length == 0 || path.EndsWith('/');
            entries.Add((
                new DelverEntry(path.TrimEnd('/'), isDirectory ? DelverEntryKind.Other : DelverEntryClassifier.Classify(path), source.Length),
                source));
        }

        _entries = entries
            .OrderBy(pair => pair.Item1.Path, StringComparer.Ordinal)
            .ThenBy(pair => pair.Item1.Kind)
            .ToList();
        _entryList = _entries.Select(pair => pair.Item1).ToList();
    }

    /// <summary>All entries (including directory placeholders), ordinal by path.</summary>
    public IReadOnlyList<DelverEntry> Entries => _entryList;

    /// <summary>Uncompressed entry bytes.</summary>
    public byte[] ReadEntryBytes(string path)
    {
        ZipArchiveEntry source = FindEntry(path);
        using Stream input = source.Open();
        using MemoryStream buffer = new();
        input.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Entry bytes decoded as UTF-8 (invalid sequences replaced).</summary>
    public string ReadEntryText(string path) => Encoding.UTF8.GetString(ReadEntryBytes(path));

    /// <summary>
    /// Extracts entries of the selected kinds to a destination directory,
    /// preserving inner paths and overwriting existing files. Returns the
    /// extracted relative paths in the same deterministic order. Entries whose
    /// inner path escapes the destination (absolute or <c>..</c>) are rejected.
    /// </summary>
    public IReadOnlyList<string> ExtractTo(string destinationDirectory, IReadOnlySet<DelverEntryKind> kinds)
    {
        ArgumentException.ThrowIfNullOrEmpty(destinationDirectory);
        ArgumentNullException.ThrowIfNull(kinds);

        List<string> extracted = new();
        foreach ((DelverEntry entry, ZipArchiveEntry source) in _entries)
        {
            if (!kinds.Contains(entry.Kind) || entry.Path.Length == 0)
            {
                continue;
            }

            string relative = SafeRelativePath(entry.Path);
            string target = Path.Combine(destinationDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            using (Stream input = source.Open())
            using (FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }

            extracted.Add(relative);
        }

        return extracted;
    }

    public void Dispose()
    {
        _zip.Dispose();
        _ownedStream?.Dispose();
    }

    private ZipArchiveEntry FindEntry(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string normalized = path.Replace('\\', '/').TrimStart('/');
        foreach ((DelverEntry entry, ZipArchiveEntry source) in _entries)
        {
            if (entry.Path == normalized)
            {
                return source;
            }
        }

        throw new FileNotFoundException($"Entry '{path}' is not in the archive.");
    }

    private static string SafeRelativePath(string path)
    {
        string[] segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or "..") || Path.IsPathRooted(path))
        {
            throw new InvalidDataException($"Archive entry path '{path}' is not a safe relative path.");
        }

        return string.Join('/', segments);
    }
}
