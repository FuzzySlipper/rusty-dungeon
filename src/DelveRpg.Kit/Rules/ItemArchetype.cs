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
    /// <summary>
    /// The donor weapon speed: it scales the swing's playback
    /// (<c>speed × 0.25 + (DEX − 4) × 0.015</c>) and so its timing.
    /// </summary>
    public float Speed { get; init; } = 1f;

    /// <summary>The held-animation style the Host plays for this weapon ("dagger", "sword", ...).</summary>
    public string SwingStyle { get; init; } = "";

    /// <summary>The quick swing, below half charge; null for a weapon that does not swing.</summary>
    public SwingTiming? WeakSwing { get; init; }

    /// <summary>The full swing, from half charge up; null to always use the quick one.</summary>
    public SwingTiming? StrongSwing { get; init; }

    /// <summary>How hard a full-power blow or shot shoves its target (donor knockback).</summary>
    public float Knockback { get; init; }

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

/// <summary>
/// One swing's shape in the donor's animation units: its whole
/// <see cref="Length"/> and the <see cref="ActionTime"/> the blow is keyed to.
/// At playback speed <c>p</c> the swing lasts <c>Length / p</c> ticks, the blow
/// lands <c>ActionTime / p × 0.5</c> ticks in, and the next attack may start
/// after <c>0.75</c> of the swing ([donor] entities/items/Sword.java:56-69).
/// </summary>
public sealed record SwingTiming(float Length, float ActionTime);
