using Delver.Import.Jar;
using Delver.Import.Normalized;
using Delver.Import.RelaxedJson;
using Delver.Import.Reporting;

namespace Delver.Import.Tool;

/// <summary>Hand-rolled command dispatch for the offline import tool.</summary>
internal static class ImportCommands
{
    private const string Usage =
        """
        delverimport — offline Delver donor import tooling.

        Usage:
          delverimport extract --jar <delver.jar> [--engine <delver-engine dir>] --out <dir>
          delverimport report (--in <dir> | --jar <delver.jar>) [--tsv]
          delverimport normalize --in <dir> --out <dir>

        extract   copies reference-worthy donor entries (dat, bin, png, obj,
                  json, atlas skins, fnt) into <dir>/extracted/ and, with
                  --engine, jsonschema/current/** into <dir>/jsonschema/.
        report    prints the inventory report (markdown, or TSV with --tsv).
        normalize writes stable strict-JSON reference tables into
                  <dir>/normalized/ (monsters/items/sections + documents/).

        Exit codes: 0 ok, 2 usage, 1 failure.
        """;

    private static readonly HashSet<DelverEntryKind> ReferenceKinds = new()
    {
        DelverEntryKind.Data,
        DelverEntryKind.Level,
        DelverEntryKind.RoomTemplate,
        DelverEntryKind.Texture,
        DelverEntryKind.Mesh,
        DelverEntryKind.Font,
    };

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            throw new UsageException(Usage);
        }

        return args[0] switch
        {
            "extract" => Extract(args[1..]),
            "report" => Report(args[1..]),
            "normalize" => Normalize(args[1..]),
            _ => throw new UsageException($"Unknown command '{args[0]}'.\n\n{Usage}"),
        };
    }

    private static int Extract(string[] args)
    {
        Options options = Options.Parse(args, new[] { "--jar", "--engine", "--out" }, Array.Empty<string>());
        string jarPath = options.Required("--jar");
        string outDirectory = options.Required("--out");
        string? engineDirectory = options.Optional("--engine");

        if (!File.Exists(jarPath))
        {
            throw new FileNotFoundException($"Jar not found: {jarPath}");
        }

        string extractedDirectory = Path.Combine(outDirectory, "extracted");
        Directory.CreateDirectory(extractedDirectory);

        int extractedCount;
        using (DelverArchive archive = DelverArchive.Open(jarPath))
        {
            extractedCount = archive.ExtractTo(extractedDirectory, ReferenceKinds).Count;
        }

        Console.WriteLine($"Extracted {extractedCount} entries into {extractedDirectory}");

        if (engineDirectory is not null)
        {
            string schemaSource = Path.Combine(engineDirectory, "jsonschema", "current");
            if (!Directory.Exists(schemaSource))
            {
                throw new DirectoryNotFoundException($"Schema directory not found: {schemaSource}");
            }

            string schemaTarget = Path.Combine(outDirectory, "jsonschema");
            int schemaCount = CopyTree(schemaSource, schemaTarget);
            Console.WriteLine($"Copied {schemaCount} schema files into {schemaTarget}");
        }

        return 0;
    }

    private static int Report(string[] args)
    {
        Options options = Options.Parse(args, new[] { "--in", "--jar" }, new[] { "--tsv" });
        string? inDirectory = options.Optional("--in");
        string? jarPath = options.Optional("--jar");
        bool tsv = options.HasFlag("--tsv");

        if ((inDirectory is null) == (jarPath is null))
        {
            throw new UsageException("report needs exactly one of --in <dir> or --jar <delver.jar>.\n\n" + Usage);
        }

        if (inDirectory is not null)
        {
            if (!Directory.Exists(inDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: {inDirectory}");
            }

            Console.Write(tsv ? InventoryReport.BuildTsv(inDirectory) : InventoryReport.BuildMarkdown(inDirectory));
            return 0;
        }

        if (!File.Exists(jarPath))
        {
            throw new FileNotFoundException($"Jar not found: {jarPath}");
        }

        using DelverArchive archive = DelverArchive.Open(jarPath);
        Console.Write(tsv
            ? InventoryReport.BuildTsv(archive, Path.GetFileName(jarPath))
            : InventoryReport.BuildMarkdown(archive, Path.GetFileName(jarPath)));
        return 0;
    }

    private static int Normalize(string[] args)
    {
        Options options = Options.Parse(args, new[] { "--in", "--out" }, Array.Empty<string>());
        string inDirectory = options.Required("--in");
        string outDirectory = options.Required("--out");
        if (!Directory.Exists(inDirectory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {inDirectory}");
        }

        string normalizedDirectory = Path.Combine(outDirectory, "normalized");
        string documentsDirectory = Path.Combine(normalizedDirectory, "documents");
        Directory.CreateDirectory(documentsDirectory);

        List<NormalizedMonster> monsters = new();
        List<NormalizedItem> items = new();
        List<NormalizedSection> sections = new();
        List<string> skipped = new();
        int documentCount = 0;

        foreach (string file in Directory.EnumerateFiles(inDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Relative(inDirectory, path), StringComparer.Ordinal))
        {
            string relative = Relative(inDirectory, file);
            if (!relative.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) &&
                !relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                JsonValue document = RelaxedJsonReader.Parse(File.ReadAllBytes(file));
                documentCount++;
                string targetRelative = Path.ChangeExtension(relative, ".normalized.json");
                string target = Path.Combine(
                    documentsDirectory, targetRelative.Replace('/', Path.DirectorySeparatorChar));
                string? parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllBytes(target, ContentNormalizer.WriteDocumentBytes(document));

                // Table dispatch is by canonical donor file name (game.dat
                // references these by name); the readers verify the shape.
                switch (Path.GetFileName(relative))
                {
                    case "monsters.dat":
                    case "entities.dat":
                        monsters.AddRange(MonsterTableReader.Read(document));
                        break;
                    case "items.dat":
                        items.AddRange(ItemTableReader.Read(document));
                        break;
                    case "section.dat":
                        sections.Add(SectionTableReader.Read(document));
                        break;
                }
            }
            catch (Exception failure) when (failure is RelaxedJsonException or InvalidDataException)
            {
                skipped.Add($"{relative}: {failure.Message}");
            }
        }

        List<NormalizedSection> orderedSections = sections
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Name, StringComparer.Ordinal)
            .ToList();

        File.WriteAllBytes(
            Path.Combine(normalizedDirectory, "monsters.normalized.json"),
            ContentNormalizer.WriteDocumentBytes(NormalizedJson.Monsters(monsters)));
        File.WriteAllBytes(
            Path.Combine(normalizedDirectory, "items.normalized.json"),
            ContentNormalizer.WriteDocumentBytes(NormalizedJson.Items(items)));
        File.WriteAllBytes(
            Path.Combine(normalizedDirectory, "sections.normalized.json"),
            ContentNormalizer.WriteDocumentBytes(NormalizedJson.Sections(orderedSections)));

        if (skipped.Count > 0)
        {
            File.WriteAllText(
                Path.Combine(normalizedDirectory, "skipped.txt"),
                string.Join('\n', skipped) + "\n");
        }

        Console.WriteLine(
            $"Normalized {documentCount} documents into {normalizedDirectory} "
            + $"(monsters: {monsters.Count}, items: {items.Count}, sections: {orderedSections.Count}, "
            + $"skipped: {skipped.Count})");
        return 0;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static int CopyTree(string source, string target)
    {
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            string destination = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
            string? parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllBytes(destination, File.ReadAllBytes(file));
            count++;
        }

        return count;
    }

    private sealed class Options
    {
        private readonly Dictionary<string, string> _values;
        private readonly HashSet<string> _flags;

        private Options(Dictionary<string, string> values, HashSet<string> flags)
        {
            _values = values;
            _flags = flags;
        }

        public static Options Parse(string[] args, IReadOnlyList<string> valueOptions, IReadOnlyList<string> flags)
        {
            Dictionary<string, string> values = new();
            HashSet<string> present = new();
            for (int i = 0; i < args.Length; i++)
            {
                string option = args[i];
                if (flags.Contains(option))
                {
                    present.Add(option);
                    continue;
                }

                if (!valueOptions.Contains(option))
                {
                    throw new UsageException($"Unknown option '{option}'.\n\n{Usage}");
                }

                if (i + 1 >= args.Length)
                {
                    throw new UsageException($"Option '{option}' needs a value.\n\n{Usage}");
                }

                values[option] = args[i + 1];
                i++;
            }

            return new Options(values, present);
        }

        public string Required(string option) =>
            _values.TryGetValue(option, out string? value)
                ? value
                : throw new UsageException($"Missing required option {option}.\n\n{Usage}");

        public string? Optional(string option) => _values.GetValueOrDefault(option);

        public bool HasFlag(string flag) => _flags.Contains(flag);
    }
}
