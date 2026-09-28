using Delver.Import.Jar;
using Delver.Import.Reporting;
using Xunit;

namespace Delver.Import.Tests;

public class InventoryReportTests
{
    private static readonly (string Path, string Content)[] Fixture =
    {
        ("data/game.dat", "{\"tutorialLevel\": \"levels/tutorial.bin\", \"entityDataFiles\": [\"data/entities.dat\"]}"),
        ("data/monsters.dat", "{\"monsters\": {\"CAVE\": []}}"),
        ("com/interrupt/Foo.class", "class"),
    };

    [Fact]
    public void BuildMarkdown_HasExpectedShape()
    {
        using MemoryStream zip = SyntheticArchive.CreateZip(Fixture);
        using DelverArchive archive = new(zip, leaveOpen: true);

        string report = InventoryReport.BuildMarkdown(archive, "synthetic.jar");

        Assert.StartsWith("# Delver reference inventory", report);
        Assert.Contains("- Source: `synthetic.jar`", report);
        Assert.Contains("- Entries: 3", report);
        Assert.Contains("## Category counts", report);
        Assert.Contains("| Data | 2 |", report);
        Assert.Contains("| Class | 1 |", report);
        Assert.Contains("## Data files", report);
        Assert.Contains("| `data/game.dat` |", report);
        Assert.Contains("`tutorialLevel`", report);
        Assert.Contains("`entityDataFiles`", report);
        Assert.Contains("| `data/monsters.dat` |", report);
    }

    [Fact]
    public void BuildTsv_ListsCountsAndDataFiles()
    {
        using MemoryStream zip = SyntheticArchive.CreateZip(Fixture);
        using DelverArchive archive = new(zip, leaveOpen: true);

        string report = InventoryReport.BuildTsv(archive);

        Assert.Contains("category\tcount", report);
        Assert.Contains("Data\t2", report);
        Assert.Contains("path\ttop-level keys", report);
        Assert.Contains("data/game.dat\t`tutorialLevel`", report);
    }

    [Fact]
    public void BuildMarkdown_ReportsExtractedDirectories()
    {
        SyntheticArchive.WithTempDirectory(directory =>
        {
            Directory.CreateDirectory(Path.Combine(directory, "data"));
            // Donor: data/quests.dat is prose, not JSON at all.
            File.WriteAllText(Path.Combine(directory, "data", "quests.dat"), "not json at all");
            File.WriteAllText(Path.Combine(directory, "data", "broken.dat"), "{\"a\": ,}");
            File.WriteAllText(Path.Combine(directory, "data", "items.dat"), "{unique: []}");

            string report = InventoryReport.BuildMarkdown(directory);

            Assert.Contains("- Entries: 3", report);
            Assert.Contains("| `data/quests.dat` | (string) |", report);
            Assert.Contains("| `data/broken.dat` | (not JSON) |", report);
            Assert.Contains("| `data/items.dat` |", report);
        });
    }
}
