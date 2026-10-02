using DelveRpg.Kit.Combat;

namespace DelveRpg.Kit.Rules;

/// <summary>
/// An item's condition, worst to best. Normal is zero so a plain instance is
/// unharmed. A weapon's base damage moves by twice the value and armor by
/// the value, the donor's <c>ordinal × 2 − 4</c> and <c>ordinal − 2</c>
/// ([donor] entities/items/Weapon.java:155, Armor.java:59, Item.java:35).
/// </summary>
public enum ItemCondition
{
    Broken = -2,
    Worn = -1,
    Normal = 0,
    Fine = 1,
    Excellent = 2,
}

/// <summary>Which enchantment table a modification belongs to.</summary>
public enum ModificationSlot
{
    WeaponPrefix,
    WeaponSuffix,
    ArmorPrefix,
    ArmorSuffix,
}

/// <summary>
/// A prefix or suffix enchantment, or a unique's fixed mods
/// ([donor] entities/items/ItemModification.java:8-49). Whole-number mods
/// grow with the item's level by <c>mod × level × 0.5</c> and fractional
/// ones by <c>mod × level × 0.05</c> (:54-104).
/// </summary>
public sealed record ItemModification(
    string Id,
    string Name,
    ModificationSlot Slot,
    int AttackMod = 0,
    int ArmorMod = 0,
    int MagicMod = 0,
    int MoveSpeedMod = 0,
    int DamageMod = 0,
    float AttackSpeedMod = 0f,
    float KnockbackMod = 0f,
    DamageType DamageType = DamageType.Physical)
{
    public bool IsPrefix => Slot is ModificationSlot.WeaponPrefix or ModificationSlot.ArmorPrefix;

    public int Scaled(int mod, int itemLevel) => mod + (int)(mod * itemLevel * 0.5f);

    public float Scaled(float mod, int itemLevel) => mod + (mod * itemLevel * 0.05f);
}

/// <summary>
/// What a potion does, assigned to the colours anew each run
/// ([donor] entities/items/Potion.java:7, :37-57, :203-221).
/// </summary>
public enum PotionEffect
{
    Healing,
    ResistMagic,
    Restoration,
    Poison,
    Regeneration,
    IronSkin,
    Paralysis,
}
