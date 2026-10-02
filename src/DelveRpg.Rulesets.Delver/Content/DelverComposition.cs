using System.Text.Json;
using System.Text.Json.Serialization;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Rulesets.Delver.Content;

/// <summary>
/// The bundle manifest: which ruleset, packs, and tuning one run loads.
/// Members with a default are settable: the source-generated reader keeps
/// an initializer only on a settable property and leaves an omitted init-only
/// member null or zero.
/// </summary>
public sealed class DelverBundleDefinition
{
    public required string Kind { get; init; }

    public required string Id { get; init; }

    public required string Ruleset { get; init; }

    public List<string> Packs { get; set; } = new();

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

/// <summary>
/// Typed tuning document; every field maps onto the Kit's <see cref="GameTuning"/>,
/// and an omitted field keeps the Kit default (settable, see the bundle note).
/// </summary>
public sealed class DelverTuningDefinition
{
    public float MaxWalkSpeed { get; set; } = 0.06f;

    public float WalkAcceleration { get; set; } = 0.02f;

    public float WalkFriction { get; set; } = 0.8f;

    public int AttackChargeTicks { get; set; } = 40;

    public int AttackCooldownTicks { get; set; } = 30;

    public float DodgeChance { get; set; } = 0.15f;

    public float EnchantChance { get; set; } = 0.2f;

    public float FleeHpFraction { get; set; } = 0.25f;

    public int EscapeSpawnCadenceStartTicks { get; set; } = 600;

    public int EscapeSpawnCadenceEndTicks { get; set; } = 60;

    public int EscapeSpawnGroupStart { get; set; } = 3;

    public int EscapeSpawnGroupEnd { get; set; } = 15;

    public float ActorSeparationTiles { get; set; } = 0.75f;

    public float NoticeDarkTiles { get; set; } = 3f;

    public float NoticeLightTiles { get; set; } = 15f;

    public int SeenRadius { get; set; } = 5;

    public int StartingGold { get; set; } = 40;

    public float EyeHeight { get; set; } = 0.5f;

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
        ActorSeparationTiles = ActorSeparationTiles,
        NoticeDarkTiles = NoticeDarkTiles,
        NoticeLightTiles = NoticeLightTiles,
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
        StartingKit = left.StartingKit.Concat(right.StartingKit).ToList(),
        SwingStyles = left.SwingStyles.Concat(right.SwingStyles).ToDictionary(entry => entry.Key, entry => entry.Value),
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

        foreach (DelverItemDefinition item in pack.Items)
        {
            if (DelverCatalog.TryParseDamageType(item.DamageType) is null)
            {
                problems.Add($"item '{item.Id}' names unknown damage type '{item.DamageType}'");
            }

            if (item.Swing.Length > 0 && !pack.SwingStyles.ContainsKey(item.Swing))
            {
                problems.Add($"item '{item.Id}' names unknown swing style '{item.Swing}'");
            }

            if (string.Equals(item.Kind, "RangedWeapon", StringComparison.OrdinalIgnoreCase) && item.Range <= 0)
            {
                problems.Add($"bow '{item.Id}' has no range");
            }

            if (string.Equals(item.Kind, "Wand", StringComparison.OrdinalIgnoreCase) && (item.ProjectileSpeed <= 0f || item.ProjectileSprite.Length == 0))
            {
                problems.Add($"wand '{item.Id}' needs a projectile speed and sprite");
            }
        }

        foreach (DelverMonsterDefinition monster in pack.Monsters)
        {
            if (monster.Ranged is DelverMonsterRangedDefinition ranged
                && (DelverCatalog.TryParseDamageType(ranged.DamageType) is null || ranged.Speed <= 0f))
            {
                problems.Add($"monster '{monster.Id}' has a ranged attack without a known damage type and a speed");
            }
        }

        foreach (string itemId in pack.StartingKit)
        {
            if (pack.Items.All(item => item.Id != itemId))
            {
                problems.Add($"starting kit entry '{itemId}' names no item");
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
