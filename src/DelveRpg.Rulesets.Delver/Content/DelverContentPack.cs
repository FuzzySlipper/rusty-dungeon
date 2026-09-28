using System.Text.Json.Serialization;

namespace DelveRpg.Rulesets.Delver.Content;

/// <summary>
/// The typed shape of one content pack document. These fields adapt the
/// donor's data model (monsters.dat, items.dat, section.dat, loot lists) to
/// this product's own schema; the donor files themselves are never loaded at
/// runtime. See docs/research/delver-data-inventory.md for provenance.
/// </summary>
public sealed class DelverContentPack
{
    public List<DelverMonsterDefinition> Monsters { get; init; } = new();

    public List<DelverItemDefinition> Items { get; init; } = new();

    public List<DelverSectionDefinition> Sections { get; init; } = new();

    public List<DelverLootDefinition> Loot { get; init; } = new();
}

public sealed class DelverMonsterDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Sprite { get; init; } = "monster";

    public required int BaseHp { get; init; }

    public required int AttackPower { get; init; }

    public int AttackCooldownTicks { get; init; } = 45;

    public required int MonsterLevel { get; init; }

    public bool IsRanged { get; init; }

    public int DetectRange { get; init; } = 8;

    public int MinDungeonLevel { get; init; } = 1;

    public int MaxDungeonLevel { get; init; } = 99;

    public required int Attack { get; init; }

    public required int Defense { get; init; }

    public required int Dexterity { get; init; }

    public required int Speed { get; init; }

    public required int Magic { get; init; }

    public required int Endurance { get; init; }
}

public sealed class DelverItemDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Weapon, RangedWeapon, Wand, Potion, Food, Gold, Key, Armor, Helmet, QuestOrb.</summary>
    public required string Kind { get; init; }

    public string Sprite { get; init; } = "item";

    public int Power { get; init; }

    public int ChargeTicks { get; init; } = 40;

    public int HealAmount { get; init; }

    public int Value { get; init; }

    public bool Stackable { get; init; }

    public int MinItemLevel { get; init; } = 1;

    public int MaxItemLevel { get; init; } = 99;
}

public sealed class DelverSectionDefinition
{
    public required string Name { get; init; }

    public required int DifficultyLevel { get; init; }

    public int SortOrder { get; init; }

    /// <summary>How many ordinary floors this section contributes.</summary>
    public required int Floors { get; init; }

    public List<DelverLevelTemplate> LevelTemplates { get; init; } = new();

    /// <summary>An optional themed floor appended after the ordinary floors.</summary>
    public DelverLevelTemplate? TransitionLevel { get; init; }
}

public sealed class DelverLevelTemplate
{
    public required string Theme { get; init; }

    public bool Generated { get; init; } = true;
}

public sealed class DelverLootDefinition
{
    public required string ItemId { get; init; }

    public int Weight { get; init; } = 1;

    public int MinItemLevel { get; init; } = 1;

    public int MaxItemLevel { get; init; } = 99;
}

/// <summary>JSON binding for pack documents (NativeAOT-safe source generation).</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(DelverContentPack))]
public sealed partial class DelverContentJsonContext : JsonSerializerContext;
