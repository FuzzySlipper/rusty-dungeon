using Delver.Import.RelaxedJson;

namespace Delver.Import.Normalized;

/// <summary>One level template (or transition level) of a donor section.</summary>
public sealed record NormalizedLevelTemplate(
    string Class,
    string? Theme,
    string? LevelName,
    double? FogStart,
    double? FogEnd,
    double? Darkness,
    bool? Generated,
    string? RoomGeneratorType,
    string? Music,
    string? AmbientSound);

/// <summary>One donor generator section (generator/&lt;Theme&gt;/section.dat).</summary>
public sealed record NormalizedSection(
    int SortOrder,
    int DifficultyLevel,
    string Name,
    int Floors,
    IReadOnlyList<NormalizedLevelTemplate> Templates,
    NormalizedLevelTemplate? Transition);

/// <summary>
/// Reads the donor section definition shape
/// (generator/Dungeon/section.dat):
/// <c>{"difficultyLevel":1,"sortOrder":1,"name":"Dungeon","floors":2,
/// "levelTemplates":[{…}],"transitionLevel":{…}}</c>.
/// </summary>
public static class SectionTableReader
{
    public static NormalizedSection Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Read(RelaxedJsonReader.Parse(json));
    }

    public static NormalizedSection Read(JsonValue document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Kind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A section definition must be a JSON object.");
        }

        document.TryGetProperty("sortOrder", out JsonValue sortOrder);
        document.TryGetProperty("difficultyLevel", out JsonValue difficultyLevel);
        document.TryGetProperty("name", out JsonValue name);
        document.TryGetProperty("floors", out JsonValue floors);
        document.TryGetProperty("levelTemplates", out JsonValue levelTemplates);
        document.TryGetProperty("transitionLevel", out JsonValue transitionLevel);

        List<NormalizedLevelTemplate> templates = new();
        if (levelTemplates.Kind == JsonValueKind.Array)
        {
            foreach (JsonValue template in levelTemplates.Items)
            {
                if (template.Kind == JsonValueKind.Object)
                {
                    templates.Add(ReadTemplate(template));
                }
            }
        }

        return new NormalizedSection(
            JsonRead.IntOr(sortOrder, 0),
            JsonRead.IntOr(difficultyLevel, 0),
            JsonRead.StringOr(name, string.Empty),
            JsonRead.IntOr(floors, 0),
            templates,
            transitionLevel.Kind == JsonValueKind.Object ? ReadTemplate(transitionLevel) : null);
    }

    private static NormalizedLevelTemplate ReadTemplate(JsonValue template)
    {
        template.TryGetProperty("class", out JsonValue className);
        template.TryGetProperty("theme", out JsonValue theme);
        template.TryGetProperty("levelName", out JsonValue levelName);
        template.TryGetProperty("fogStart", out JsonValue fogStart);
        template.TryGetProperty("fogEnd", out JsonValue fogEnd);
        template.TryGetProperty("darkness", out JsonValue darkness);
        template.TryGetProperty("generated", out JsonValue generated);
        template.TryGetProperty("roomGeneratorType", out JsonValue roomGeneratorType);
        template.TryGetProperty("music", out JsonValue music);
        template.TryGetProperty("ambientSound", out JsonValue ambientSound);

        return new NormalizedLevelTemplate(
            JsonRead.StringOr(className, string.Empty),
            JsonRead.String(theme),
            JsonRead.String(levelName),
            JsonRead.Number(fogStart),
            JsonRead.Number(fogEnd),
            JsonRead.Number(darkness),
            generated.Kind == JsonValueKind.Bool ? generated.BoolValue : null,
            JsonRead.String(roomGeneratorType),
            JsonRead.String(music),
            JsonRead.String(ambientSound));
    }
}
