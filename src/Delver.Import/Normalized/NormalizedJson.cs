using Delver.Import.RelaxedJson;

namespace Delver.Import.Normalized;

/// <summary>
/// Emits normalized tables as <see cref="JsonValue"/> trees so every
/// reference artifact goes through one canonical writer (sorted keys,
/// invariant numbers).
/// </summary>
public static class NormalizedJson
{
    /// <summary>Normalized monster table document.</summary>
    public static JsonValue Monsters(IEnumerable<NormalizedMonster> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return JsonValue.Array(rows.Select(Monster));
    }

    /// <summary>Normalized item table document.</summary>
    public static JsonValue Items(IEnumerable<NormalizedItem> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return JsonValue.Array(rows.Select(Item));
    }

    /// <summary>Normalized section table document.</summary>
    public static JsonValue Sections(IEnumerable<NormalizedSection> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return JsonValue.Array(rows.Select(Section));
    }

    private static JsonValue Monster(NormalizedMonster row)
    {
        List<JsonProperty> properties = new();
        Add(properties, "category", JsonValue.String(row.Category));
        Add(properties, "name", JsonValue.String(row.Name));
        Add(properties, "class", JsonValue.String(row.Class));
        Add(properties, "spriteTex", Number(row.SpriteTex));
        Add(properties, "spriteAtlas", Text(row.SpriteAtlas));
        Add(properties, "hp", Number(row.Hp));
        Add(properties, "damage", Number(row.Damage));
        Add(properties, "level", Number(row.Level));
        Add(properties, "scale", Number(row.Scale));
        Add(properties, "sounds", JsonValue.Object(
            new JsonProperty("alert", Strings(row.Sounds.Alert)),
            new JsonProperty("attack", Strings(row.Sounds.Attack)),
            new JsonProperty("die", Strings(row.Sounds.Die)),
            new JsonProperty("hurt", Strings(row.Sounds.Hurt)),
            new JsonProperty("idle", Strings(row.Sounds.Idle)),
            new JsonProperty("walk", Strings(row.Sounds.Walk))));
        return JsonValue.Object(properties);
    }

    private static JsonValue Item(NormalizedItem row)
    {
        List<JsonProperty> properties = new();
        Add(properties, "category", JsonValue.String(row.Category));
        Add(properties, "name", JsonValue.String(row.Name));
        Add(properties, "class", JsonValue.String(row.Class));
        Add(properties, "itemType", JsonValue.String(row.ItemType));
        Add(properties, "spriteTex", Number(row.SpriteTex));
        Add(properties, "cost", Number(row.Cost));
        Add(properties, "baseDamage", Number(row.BaseDamage));
        Add(properties, "randDamage", Number(row.RandDamage));
        Add(properties, "minItemLevel", Number(row.MinItemLevel));
        Add(properties, "maxItemLevel", Number(row.MaxItemLevel));
        return JsonValue.Object(properties);
    }

    private static JsonValue Section(NormalizedSection row)
    {
        List<JsonProperty> properties = new();
        Add(properties, "sortOrder", JsonValue.Number(row.SortOrder));
        Add(properties, "difficultyLevel", JsonValue.Number(row.DifficultyLevel));
        Add(properties, "name", JsonValue.String(row.Name));
        Add(properties, "floors", JsonValue.Number(row.Floors));
        Add(properties, "templates", JsonValue.Array(row.Templates.Select(Template)));
        Add(properties, "transition", row.Transition is null ? JsonValue.Null : Template(row.Transition));
        return JsonValue.Object(properties);
    }

    private static JsonValue Template(NormalizedLevelTemplate row)
    {
        List<JsonProperty> properties = new();
        Add(properties, "class", JsonValue.String(row.Class));
        Add(properties, "theme", Text(row.Theme));
        Add(properties, "levelName", Text(row.LevelName));
        Add(properties, "fogStart", Number(row.FogStart));
        Add(properties, "fogEnd", Number(row.FogEnd));
        Add(properties, "darkness", Number(row.Darkness));
        if (row.Generated is not null)
        {
            Add(properties, "generated", JsonValue.Bool(row.Generated.Value));
        }

        Add(properties, "roomGeneratorType", Text(row.RoomGeneratorType));
        Add(properties, "music", Text(row.Music));
        Add(properties, "ambientSound", Text(row.AmbientSound));
        return JsonValue.Object(properties);
    }

    private static JsonValue? Text(string? text) => text is null ? null : JsonValue.String(text);

    private static JsonValue? Number(double? value) => value is null ? null : JsonValue.Number(value.Value);

    private static JsonValue? Number(int? value) => value is null ? null : JsonValue.Number(value.Value);

    private static JsonValue Strings(IReadOnlyList<string> values) =>
        JsonValue.Array(values.Select(JsonValue.String));

    private static void Add(List<JsonProperty> properties, string name, JsonValue? value)
    {
        if (value is not null)
        {
            properties.Add(new JsonProperty(name, value));
        }
    }
}
