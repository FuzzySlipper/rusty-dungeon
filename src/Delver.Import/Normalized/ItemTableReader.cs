using Delver.Import.RelaxedJson;

namespace Delver.Import.Normalized;

/// <summary>One donor item definition in stable reference form.</summary>
public sealed record NormalizedItem(
    string Category,
    string Name,
    string Class,
    string ItemType,
    int? SpriteTex,
    double? Cost,
    double? BaseDamage,
    double? RandDamage,
    int? MinItemLevel,
    int? MaxItemLevel);

/// <summary>
/// Pulls the item table out of donor content documents. Donor shape
/// (data/items.dat): top-level category arrays — <c>{"unique": [ {…} ],
/// "melee": [ {…} ], …}</c> — optionally wrapped as <c>{"items": {…}}</c>.
/// A category map of named objects is accepted as well; the map key supplies
/// the name.
/// </summary>
public static class ItemTableReader
{
    public static IReadOnlyList<NormalizedItem> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Read(RelaxedJsonReader.Parse(json));
    }

    public static IReadOnlyList<NormalizedItem> Read(JsonValue document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<NormalizedItem> rows = new();
        foreach ((string category, JsonValue entries) in EnumerateCategories(document))
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

    private static IEnumerable<(string Category, JsonValue Entries)> EnumerateCategories(JsonValue document)
    {
        if (document.Kind != JsonValueKind.Object)
        {
            yield break;
        }

        JsonValue table = document;
        if (document.TryGetProperty("items", out JsonValue candidate) && candidate.Kind == JsonValueKind.Object)
        {
            table = candidate;
        }

        foreach (JsonProperty property in table.Properties)
        {
            if (property.Value.Kind is JsonValueKind.Array or JsonValueKind.Object)
            {
                yield return (property.Name, property.Value);
            }
        }
    }

    private static NormalizedItem FromEntity(string category, JsonValue entity, string? nameFallback)
    {
        entity.TryGetProperty("name", out JsonValue name);
        entity.TryGetProperty("class", out JsonValue className);
        entity.TryGetProperty("itemType", out JsonValue itemType);
        entity.TryGetProperty("tex", out JsonValue tex);
        entity.TryGetProperty("cost", out JsonValue cost);
        entity.TryGetProperty("baseDamage", out JsonValue baseDamage);
        entity.TryGetProperty("randDamage", out JsonValue randDamage);
        entity.TryGetProperty("minItemLevel", out JsonValue minItemLevel);
        entity.TryGetProperty("maxItemLevel", out JsonValue maxItemLevel);

        return new NormalizedItem(
            category,
            JsonRead.StringOr(name, nameFallback ?? string.Empty),
            JsonRead.StringOr(className, string.Empty),
            JsonRead.StringOr(itemType, string.Empty),
            JsonRead.Int(tex),
            JsonRead.Number(cost),
            JsonRead.Number(baseDamage),
            JsonRead.Number(randDamage),
            JsonRead.Int(minItemLevel),
            JsonRead.Int(maxItemLevel));
    }
}
