using System.Text;
using Delver.Import.Jar;
using Delver.Import.RelaxedJson;

namespace Delver.Import.Reporting;

/// <summary>
/// Concise inventory of a donor archive or an extracted directory: entry
/// counts per category plus the data-file list with each document's
/// top-level keys. Output is deterministic for a given input.
/// </summary>
public static class InventoryReport
{
    private const int KeysShown = 8;

    /// <summary>Markdown report for a jar/zip.</summary>
    public static string BuildMarkdown(DelverArchive archive, string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return RenderMarkdown(Collect(archive, sourceLabel ?? "archive"));
    }

    /// <summary>Markdown report for a directory of extracted files.</summary>
    public static string BuildMarkdown(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return RenderMarkdown(Collect(directory));
    }

    /// <summary>TSV report (category counts, then data-file keys) for a jar/zip.</summary>
    public static string BuildTsv(DelverArchive archive, string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return RenderTsv(Collect(archive, sourceLabel ?? "archive"));
    }

    /// <summary>TSV report (category counts, then data-file keys) for a directory.</summary>
    public static string BuildTsv(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return RenderTsv(Collect(directory));
    }

    private sealed record InventoryItem(string Path, DelverEntryKind Kind, string? Content);

    private sealed record Inventory(string Source, IReadOnlyList<InventoryItem> Items);

    private static Inventory Collect(DelverArchive archive, string source)
    {
        List<InventoryItem> items = new();
        foreach (DelverEntry entry in archive.Entries)
        {
            string? content = IsDataDocument(entry.Path) ? archive.ReadEntryText(entry.Path) : null;
            items.Add(new InventoryItem(entry.Path, entry.Kind, content));
        }

        return new Inventory(source, items);
    }

    private static Inventory Collect(string directory)
    {
        List<InventoryItem> items = new();
        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            bool isData = IsDataDocument(relative);
            items.Add(new InventoryItem(
                relative,
                DelverEntryClassifier.Classify(relative),
                isData ? File.ReadAllText(file) : null));
        }

        return new Inventory(directory, items);
    }

    private static bool IsDataDocument(string path) =>
        path.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static string RenderMarkdown(Inventory inventory)
    {
        StringBuilder builder = new();
        builder.Append("# Delver reference inventory\n\n");
        builder.Append("- Source: `").Append(inventory.Source).Append("`\n");
        builder.Append("- Entries: ").Append(inventory.Items.Count).Append('\n');
        builder.Append("\n## Category counts\n\n| Category | Count |\n| --- | ---: |\n");
        foreach (var group in Counts(inventory))
        {
            builder.Append("| ").Append(group.Kind).Append(" | ").Append(group.Count).Append(" |\n");
        }

        builder.Append("\n## Data files\n\n| Path | Top-level keys |\n| --- | --- |\n");
        foreach (InventoryItem item in DataFiles(inventory))
        {
            builder.Append("| `").Append(item.Path).Append("` | ")
                .Append(TopLevelKeys(item.Content)).Append(" |\n");
        }

        return builder.ToString();
    }

    private static string RenderTsv(Inventory inventory)
    {
        StringBuilder builder = new();
        builder.Append("category\tcount\n");
        foreach (var group in Counts(inventory))
        {
            builder.Append(group.Kind).Append('\t').Append(group.Count).Append('\n');
        }

        builder.Append("\npath\ttop-level keys\n");
        foreach (InventoryItem item in DataFiles(inventory))
        {
            builder.Append(item.Path).Append('\t').Append(TopLevelKeys(item.Content)).Append('\n');
        }

        return builder.ToString();
    }

    private static IEnumerable<(DelverEntryKind Kind, int Count)> Counts(Inventory inventory) =>
        inventory.Items
            .GroupBy(item => item.Kind)
            .OrderBy(group => group.Key)
            .Select(group => (group.Key, group.Count()));

    private static IEnumerable<InventoryItem> DataFiles(Inventory inventory) =>
        inventory.Items.Where(item => item.Content is not null);

    private static string TopLevelKeys(string? content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        JsonValue document;
        try
        {
            document = RelaxedJsonReader.Parse(content);
        }
        catch (RelaxedJsonException)
        {
            return "(not JSON)";
        }

        if (document.Kind != JsonValueKind.Object)
        {
            return $"({document.Kind.ToString().ToLowerInvariant()})";
        }

        IReadOnlyList<JsonProperty> properties = document.Properties;
        IEnumerable<string> shown = properties.Take(KeysShown).Select(property => $"`{property.Name}`");
        string joined = string.Join(", ", shown);
        return properties.Count > KeysShown ? $"{joined}, … (+{properties.Count - KeysShown} more)" : joined;
    }
}
