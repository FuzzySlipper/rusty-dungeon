using System.Xml.Linq;
using Xunit;

namespace DelveRpg.Architecture.Tests;

/// <summary>
/// The automated half of the boundary rules in AGENTS.md: the dependency
/// graph, the Kit's ruleset-vocabulary law, the one product entry, and the
/// intent contract between the product manifest and the DOM companion.
/// </summary>
public sealed class ArchitectureLawTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Project_references_follow_the_delverpg_dependency_graph()
    {
        AssertProjectReferences("DelveRpg.Kit", []);
        AssertProjectReferences("DelveRpg.Rulesets.Delver", ["DelveRpg.Kit"]);
        AssertProjectReferences("DelveRpg.Host", ["DelveRpg.Kit", "DelveRpg.Rulesets.Delver"]);
        AssertProjectReferences("Delver.Import", []);
        AssertProjectReferences("Delver.Import.Tool", ["Delver.Import"]);

        AssertPackageReference("DelveRpg.Host", "Rusty.Engine");
    }

    [Fact]
    public void The_kit_is_engine_free_and_ruleset_vocabulary_free()
    {
        AssertNoPackageReference("DelveRpg.Kit");
        AssertNoPackageReference("DelveRpg.Rulesets.Delver");

        // NOTE: the family name "DelveRpg" itself contains "delver" as a
        // substring, so the vocabulary checks below use discriminators the
        // family name cannot produce (dot-qualified donor names, content-id
        // prefixes, theme and monster names). A bare "Delver" stays a human
        // review check.
        string kit = ReadSources(Path.Combine(RepositoryRoot, "src", "DelveRpg.Kit"));
        foreach (string forbidden in new[] { "Delver.", "delve.", "Sewer", "Temple", "Kobold", "Slime", "com.interrupt" })
        {
            Assert.DoesNotContain(forbidden, kit, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_offline_importer_is_not_a_runtime_dependency()
    {
        AssertNoPackageReference("Delver.Import");
        AssertNoPackageReference("Delver.Import.Tool");

        foreach (string project in new[] { "DelveRpg.Kit", "DelveRpg.Rulesets.Delver", "DelveRpg.Host" })
        {
            Assert.DoesNotContain("Delver.Import", File.ReadAllText(ProjectFile(project)), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Exactly_one_project_declares_a_product()
    {
        string[] declaring = Directory.GetFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("RustyEngineProductEntryType", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();
        Assert.Equal(new[] { "DelveRpg.Host.csproj" }, declaring);
    }

    [Fact]
    public void The_dom_companion_claims_only_declared_intents()
    {
        string manifest = File.ReadAllText(ProjectFile("DelveRpg.Host"));
        string ui = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "ui", "main.ts"));

        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement element in XDocument.Parse(manifest).Descendants("RustyEngineProductInputIntent"))
        {
            declared.Add((string?)element.Attribute("Include") ?? string.Empty);
        }

        Assert.NotEmpty(declared);
        foreach (string claimed in new[] { "menu.confirm", "menu.cancel", "menu.up", "menu.down" })
        {
            Assert.Contains(claimed, declared);
            Assert.Contains(claimed, ui, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_projection_contract_is_declared_once_each_side()
    {
        string manifest = File.ReadAllText(ProjectFile("DelveRpg.Host"));
        string ui = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "ui", "main.ts"));
        Assert.Contains("delve.ui.snapshot.v1", manifest, StringComparison.Ordinal);
        Assert.Contains("delve.ui.snapshot.v1", ui, StringComparison.Ordinal);
    }

    private static void AssertProjectReferences(string project, string[] expected)
    {
        XDocument document = XDocument.Load(ProjectFile(project));
        string[] actual = document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
    }

    private static void AssertPackageReference(string project, string package)
    {
        XDocument document = XDocument.Load(ProjectFile(project));
        Assert.Contains(document.Descendants("PackageReference"),
            element => ((string?)element.Attribute("Include")) == package);
    }

    private static void AssertNoPackageReference(string project)
    {
        XDocument document = XDocument.Load(ProjectFile(project));
        Assert.Empty(document.Descendants("PackageReference"));
    }

    private static string ProjectFile(string project) =>
        Path.Combine(RepositoryRoot, "src", project, project + ".csproj");

    private static string ReadSources(string directory) =>
        string.Join('\n', Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("obj", StringComparison.Ordinal) && !path.Contains("bin", StringComparison.Ordinal))
            .Select(File.ReadAllText));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
