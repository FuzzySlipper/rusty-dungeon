using DelveRpg.Kit.Combat;

namespace DelveRpg.Kit.Rules;

public enum ItemKind
{
    Weapon,
    RangedWeapon,
    Wand,
    Ammo,
    Potion,
    Food,
    Gold,
    Key,
    Armor,
    Helmet,
    QuestOrb,
}

/// <summary>
/// A ruleset-supplied item definition. <see cref="Power"/> reads as a weapon's
/// base damage, absorbed armor class for armor, and magnitude for consumables;
/// <see cref="RandDamage"/> is a weapon's random damage on top;
/// <see cref="ChargeTicks"/> is the wind-up to a full-power swing.
/// </summary>
public sealed record ItemArchetype(
    string Id,
    string DisplayName,
    ItemKind Kind,
    int Power,
    int ChargeTicks,
    int HealAmount,
    int Value,
    string SpriteId,
    int RandDamage = 0)
{
    /// <summary>What a weapon's blows or bolts are made of.</summary>
    public DamageType DamageType { get; init; } = DamageType.Physical;

    /// <summary>A bow's reach: a full-charge arrow leaves at range/8 tiles per tick.</summary>
    public int Range { get; init; }

    /// <summary>Bolts a fresh wand holds.</summary>
    public int Charges { get; init; }

    /// <summary>A wand that fires while held, once per this many ticks; 0 fires on release.</summary>
    public int AutoFireTicks { get; init; }

    /// <summary>A wand bolt's speed in tiles per tick.</summary>
    public float ProjectileSpeed { get; init; }

    /// <summary>A wand's aim: 1 is dead on; lower scatters up to (1 − accuracy) × 45°.</summary>
    public float Accuracy { get; init; } = 1f;

    /// <summary>How many a fresh find holds (a bundle of arrows); 1 for most items.</summary>
    public int StackSize { get; init; } = 1;

    /// <summary>The sprite a fired bolt or arrow flies as; empty when the item fires nothing.</summary>
    public string ProjectileSpriteId { get; init; } = "";
}
