using Delver.Import.RelaxedJson;

namespace Delver.Import.Normalized;

/// <summary>Donor sound cue lists (alert/attack/die/hurt/idle/walk).</summary>
public sealed record NormalizedMonsterSounds(
    IReadOnlyList<string> Alert,
    IReadOnlyList<string> Attack,
    IReadOnlyList<string> Die,
    IReadOnlyList<string> Hurt,
    IReadOnlyList<string> Idle,
    IReadOnlyList<string> Walk);

/// <summary>One donor monster definition in stable reference form.</summary>
public sealed record NormalizedMonster(
    string Category,
    string Name,
    string Class,
    int? SpriteTex,
    string? SpriteAtlas,
    double? Hp,
    double? Damage,
    double? Level,
    double? Scale,
    NormalizedMonsterSounds Sounds);

/// <summary>
/// Pulls the monster table out of donor content documents. Donor shape
/// (data/monsters.dat): <c>{"monsters": {"CAVE": [ {…} ], …}}</c> — a
/// category map to arrays. Also accepts entity maps (data/entities.dat:
/// <c>{"entities": {"Traps": {"Spike Trap": {…}}}}</c> — category map to name
/// map), where the map key supplies the name.
/// </summary>
public static class MonsterTableReader
{
    public static IReadOnlyList<NormalizedMonster> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Read(RelaxedJsonReader.Parse(json));
    }

    public static IReadOnlyList<NormalizedMonster> Read(JsonValue document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<NormalizedMonster> rows = new();
        foreach ((string category, JsonValue entries) in EnumerateTables(document))
        {
            if (entries.Kind == JsonValueKind.Array)
            {
                foreach (JsonValue item in entries.Items)
                {
                    if (item.Kind == JsonValueKind.Object)
                    {
                        rows.Add(FromEntity(category, item, nameFallback: null));
                    }
                }
            }
            else if (entries.Kind == JsonValueKind.Object)
            {
                foreach (JsonProperty named in entries.Properties)
                {
                    if (named.Value.Kind == JsonValueKind.Object)
                    {
                        rows.Add(FromEntity(category, named.Value, named.Name));
                    }
                }
            }
        }

        return rows;
    }

    private static IEnumerable<(string Category, JsonValue Entries)> EnumerateTables(JsonValue document)
    {
        if (document.Kind != JsonValueKind.Object)
        {
            yield break;
        }

        // Donor roots carry the table under "monsters" or "entities"; a bare
        // category map (no wrapper) is accepted too.
        JsonValue table = document;
        foreach (string wrapper in new[] { "monsters", "entities" })
        {
            if (document.TryGetProperty(wrapper, out JsonValue candidate) && candidate.Kind == JsonValueKind.Object)
            {
                table = candidate;
                break;
            }
        }

        foreach (JsonProperty property in table.Properties)
        {
            if (property.Value.Kind is JsonValueKind.Array or JsonValueKind.Object)
            {
                yield return (property.Name, property.Value);
            }
        }
    }

    private static NormalizedMonster FromEntity(string category, JsonValue entity, string? nameFallback)
    {
        entity.TryGetProperty("name", out JsonValue name);
        entity.TryGetProperty("class", out JsonValue className);
        entity.TryGetProperty("tex", out JsonValue tex);
        entity.TryGetProperty("spriteAtlas", out JsonValue spriteAtlas);
        entity.TryGetProperty("maxHp", out JsonValue maxHp);
        entity.TryGetProperty("atk", out JsonValue atk);
        entity.TryGetProperty("baseLevel", out JsonValue baseLevel);
        entity.TryGetProperty("scale", out JsonValue scale);

        return new NormalizedMonster(
            category,
            JsonRead.StringOr(name, nameFallback ?? string.Empty),
            JsonRead.StringOr(className, string.Empty),
            JsonRead.Int(tex),
            JsonRead.String(spriteAtlas),
            JsonRead.Number(maxHp),
            JsonRead.Number(atk),
            JsonRead.Number(baseLevel),
            JsonRead.Number(scale),
            new NormalizedMonsterSounds(
                JsonRead.StringList(Sound(entity, "alertSound")),
                JsonRead.StringList(Sound(entity, "attackSound")),
                JsonRead.StringList(Sound(entity, "dieSound")),
                JsonRead.StringList(Sound(entity, "hurtSound")),
                JsonRead.StringList(Sound(entity, "idleSound")),
                JsonRead.StringList(Sound(entity, "walkSound"))));
    }

    private static JsonValue? Sound(JsonValue entity, string name) =>
        entity.TryGetProperty(name, out JsonValue value) ? value : null;
}
