using System.Text.Json;
using System.Text.Json.Serialization;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Rulesets.Delver.Content;

/// <summary>The bundle manifest: which ruleset, packs, and tuning one run loads.</summary>
public sealed class DelverBundleDefinition
{
    public required string Kind { get; init; }

    public required string Id { get; init; }

    public required string Ruleset { get; init; }

    public List<string> Packs { get; init; } = new();

    public required string Tuning { get; init; }
}

/// <summary>A pack descriptor: an id plus the payload document it admits.</summary>
public sealed class DelverPackDescriptor
{
    public required string Kind { get; init; }

    public required string Id { get; init; }

    public required string Ruleset { get; init; }

    /// <summary>Payload path inside the product's content tree (under 'delve/').</summary>
    public required string Payload { get; init; }
}

/// <summary>Typed tuning document; every field maps onto the Kit's <see cref="GameTuning"/>.</summary>
public sealed class DelverTuningDefinition
{
    public float MaxWalkSpeed { get; init; } = 0.06f;

    public float WalkAcceleration { get; init; } = 0.02f;

    public float WalkFriction { get; init; } = 0.8f;

    public int AttackChargeTicks { get; init; } = 40;

    public int AttackCooldownTicks { get; init; } = 30;

    public float DodgeChance { get; init; } = 0.15f;

    public float EnchantChance { get; init; } = 0.2f;

    public float FleeHpFraction { get; init; } = 0.25f;

    public int EscapeSpawnCadenceStartTicks { get; init; } = 600;

    public int EscapeSpawnCadenceEndTicks { get; init; } = 60;

    public int EscapeSpawnGroupStart { get; init; } = 3;

    public int EscapeSpawnGroupEnd { get; init; } = 15;

    public int SeenRadius { get; init; } = 5;

    public int StartingGold { get; init; } = 40;

    public float EyeHeight { get; init; } = 0.5f;

    public GameTuning ToTuning() => new()
    {
        MaxWalkSpeed = MaxWalkSpeed,
        WalkAcceleration = WalkAcceleration,
        WalkFriction = WalkFriction,
        AttackChargeTicks = AttackChargeTicks,
        AttackCooldownTicks = AttackCooldownTicks,
        DodgeChance = DodgeChance,
        EnchantChance = EnchantChance,
        FleeHpFraction = FleeHpFraction,
        EscapeSpawnCadenceStartTicks = EscapeSpawnCadenceStartTicks,
        EscapeSpawnCadenceEndTicks = EscapeSpawnCadenceEndTicks,
        EscapeSpawnGroupStart = EscapeSpawnGroupStart,
        EscapeSpawnGroupEnd = EscapeSpawnGroupEnd,
        SeenRadius = SeenRadius,
        StartingGold = StartingGold,
        EyeHeight = EyeHeight,
    };
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(DelverBundleDefinition))]
[JsonSerializable(typeof(DelverPackDescriptor))]
[JsonSerializable(typeof(DelverTuningDefinition))]
public sealed partial class DelverCompositionJsonContext : JsonSerializerContext;

/// <summary>One fully resolved run composition: the pack and tuning a run uses.</summary>
public sealed record DelverComposition(DelverBundleDefinition Bundle, DelverContentPack Pack, GameTuning Tuning)
{
    /// <summary>
    /// Resolve a composition through one text source (the Engine content
    /// snapshot at runtime, the content directory in tests). Paths follow the
    /// content layout documented in content/README.md.
    /// </summary>
    public static DelverComposition Load(Func<string, string?> readText, string bundleId = "delve-run")
    {
        string bundlePath = $"delve/bundles/{bundleId}.json";
        string? bundleText = readText(bundlePath)
            ?? throw new InvalidOperationException($"Missing bundle manifest '{bundlePath}'.");
        DelverBundleDefinition bundle = Deserialize<DelverBundleDefinition>(
            bundleText, DelverCompositionJsonContext.Default.DelverBundleDefinition, bundlePath);
        if (bundle.Ruleset != "delver")
        {
            throw new InvalidOperationException($"Bundle '{bundle.Id}' selects ruleset '{bundle.Ruleset}', which this product does not compile.");
        }

        var pack = new DelverContentPack();
        foreach (string packId in bundle.Packs)
        {
            string? descriptorText = readText($"delve/content-packs/{packId}.json")
                ?? throw new InvalidOperationException($"Missing content pack descriptor 'delve/content-packs/{packId}.json'.");
            DelverPackDescriptor descriptor = Deserialize<DelverPackDescriptor>(
                descriptorText, DelverCompositionJsonContext.Default.DelverPackDescriptor,
                $"delve/content-packs/{packId}.json");
            string? payloadText = readText($"delve/{descriptor.Payload}")
                ?? throw new InvalidOperationException($"Missing content pack payload '{descriptor.Payload}'.");
            DelverContentPack document = Deserialize<DelverContentPack>(
                payloadText, DelverContentJsonContext.Default.DelverContentPack, $"delve/{descriptor.Payload}");
            pack = Merge(pack, document);
        }

        string? tuningText = readText($"delve/tuning/{bundle.Tuning}.json")
            ?? throw new InvalidOperationException($"Missing tuning profile 'delve/tuning/{bundle.Tuning}.json'.");
        DelverTuningDefinition tuning = Deserialize<DelverTuningDefinition>(
            tuningText, DelverCompositionJsonContext.Default.DelverTuningDefinition,
            $"delve/tuning/{bundle.Tuning}.json");

        Validate(bundle, pack);
        return new DelverComposition(bundle, pack, tuning.ToTuning());
    }

    /// <summary>Parse one content document; a malformed file names its path.</summary>
    private static T Deserialize<T>(string text, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, string path)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(text, typeInfo)
                ?? throw new InvalidOperationException($"Content document '{path}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Content document '{path}' did not parse: {exception.Message}", exception);
        }
    }

    private static DelverContentPack Merge(DelverContentPack left, DelverContentPack right) => new()
    {
        Monsters = left.Monsters.Concat(right.Monsters).ToList(),
        Items = left.Items.Concat(right.Items).ToList(),
        Sections = left.Sections.Concat(right.Sections).ToList(),
        Loot = left.Loot.Concat(right.Loot).ToList(),
    };

    /// <summary>All-or-nothing validation at load; a bad pack fails the load, not a run.</summary>
    private static void Validate(DelverBundleDefinition bundle, DelverContentPack pack)
    {
        var problems = new List<string>();
        if (pack.Monsters.Count == 0)
        {
            problems.Add("the pack defines no monsters");
        }

        if (pack.Items.Count == 0)
        {
            problems.Add("the pack defines no items");
        }

        if (pack.Sections.Count == 0)
        {
            problems.Add("the pack defines no sections");
        }

        if (pack.Monsters.Select(monster => monster.Id).Distinct().Count() != pack.Monsters.Count)
        {
            problems.Add("monster ids repeat");
        }

        if (pack.Items.Select(item => item.Id).Distinct().Count() != pack.Items.Count)
        {
            problems.Add("item ids repeat");
        }

        foreach (DelverLootDefinition entry in pack.Loot)
        {
            if (pack.Items.All(item => item.Id != entry.ItemId))
            {
                problems.Add($"loot entry '{entry.ItemId}' names no item");
            }
        }

        foreach (DelverSectionDefinition section in pack.Sections)
        {
            int contributions = section.Floors + (section.TransitionLevel is null ? 0 : 1);
            if (contributions <= 0)
            {
                problems.Add($"section '{section.Name}' contributes no floors");
            }

            if (section.Floors > 0 && section.LevelTemplates.Count == 0)
            {
                problems.Add($"section '{section.Name}' contributes floors but has no level templates");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bundle '{bundle.Id}' is not loadable: {string.Join("; ", problems)}.");
        }
    }
}
