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
/// A ruleset-supplied item definition. <see cref="Power"/> reads as weapon
/// damage for weapons, absorbed armor class for armor, and magnitude for
/// consumables; <see cref="ChargeTicks"/> is the wind-up before a weapon hit.
/// </summary>
public sealed record ItemArchetype(
    string Id,
    string DisplayName,
    ItemKind Kind,
    int Power,
    int ChargeTicks,
    int HealAmount,
    int Value,
    string SpriteId);
