using Delver.Import.Normalized;
using Xunit;

namespace Delver.Import.Tests;

public class SectionTableReaderTests
{
    private const string SpecShape =
        """
        {
            "difficultyLevel": 1,
            "sortOrder": 1,
            "name": "Test",
            "floors": 2,
            "levelTemplates": [
                {
                    "class": "com.interrupt.dungeoneer.game.Level",
                    "theme": "TEST",
                    "fogStart": 3.0,
                    "generated": true
                }
            ],
            "transitionLevel": {
                "class": "com.interrupt.dungeoneer.game.Level",
                "levelName": "The Well",
                "generated": false
            }
        }
        """;

    [Fact]
    public void Read_MapsSectionShape()
    {
        NormalizedSection section = SectionTableReader.Read(SpecShape);

        Assert.Equal(1, section.SortOrder);
        Assert.Equal(1, section.DifficultyLevel);
        Assert.Equal("Test", section.Name);
        Assert.Equal(2, section.Floors);
        NormalizedLevelTemplate template = Assert.Single(section.Templates);
        Assert.Equal("com.interrupt.dungeoneer.game.Level", template.Class);
        Assert.Equal("TEST", template.Theme);
        Assert.Equal(3.0, template.FogStart);
        Assert.True(template.Generated);
        Assert.NotNull(section.Transition);
        Assert.Equal("The Well", section.Transition?.LevelName);
        Assert.False(section.Transition?.Generated);
    }

    [Fact]
    public void Read_ToleratesRelaxedSyntaxAndNumericStrings()
    {
        // Donor: generator/Sewer/section.dat encodes "ambientSoundVolume": "0.15"
        // as a string; the numeric fields may arrive the same way.
        NormalizedSection section = SectionTableReader.Read(
            "{sortOrder: 2, name: Camp, floors: \"1\", levelTemplates: [], transitionLevel: null}");

        Assert.Equal(2, section.SortOrder);
        Assert.Equal("Camp", section.Name);
        Assert.Equal(1, section.Floors);
        Assert.Empty(section.Templates);
        Assert.Null(section.Transition);
    }

    [Fact]
    public void Read_RejectsNonObjectDocuments()
    {
        Assert.Throws<InvalidDataException>(() => SectionTableReader.Read("[]"));
    }
}
