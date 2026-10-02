namespace DelveRpg.Kit.Rules;

public enum ItemKind
{
    Weapon,
    RangedWeapon,
    Wand,
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
    int RandDamage = 0);
