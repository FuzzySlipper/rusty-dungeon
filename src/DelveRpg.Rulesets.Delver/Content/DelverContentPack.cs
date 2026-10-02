using System.Text.Json.Serialization;

namespace DelveRpg.Rulesets.Delver.Content;

/// <summary>
/// The typed shape of one content pack document. These fields adapt the
/// donor's data model (monsters.dat, items.dat, section.dat, loot lists) to
/// this product's own schema; the donor files themselves are never loaded at
/// runtime. See docs/research/delver-data-inventory.md for provenance.
/// Members with a default are settable: the source-generated reader keeps
/// an initializer only on a settable property and leaves an omitted init-only
/// member null or zero.
/// </summary>
public sealed class DelverContentPack
{
    public List<DelverMonsterDefinition> Monsters { get; set; } = new();

    public List<DelverItemDefinition> Items { get; set; } = new();

    public List<DelverSectionDefinition> Sections { get; set; } = new();

    public List<DelverLootDefinition> Loot { get; set; } = new();

    /// <summary>
    /// Item ids a fresh character starts with, in slot order; the first
    /// weapon, armor, and helmet are put on (donor data/player.dat
    /// startingInventory).
    /// </summary>
    public List<string> StartingKit { get; set; } = new();

    /// <summary>
    /// Swing timings by style name, in the donor's animation units: each
    /// style's quick and full swing (donor data/animations.dat lengths and
    /// actionTime). Weapons name a style with <c>swing</c>.
    /// </summary>
    public Dictionary<string, DelverSwingStyle> SwingStyles { get; set; } = new();

    /// <summary>
    /// Prefix and suffix enchantments for weapons and armor ([data] items.dat
    /// weaponEnchantments, weaponPrefixEnchantments, armorEnchantments,
    /// armorPrefixEnchantments).
    /// </summary>
    public List<DelverEnchantment> Enchantments { get; set; } = new();

    /// <summary>
    /// Decorations per floor theme ([data] generator/&lt;Theme&gt;/info.dat
    /// decorations and genInfos sprite clusters).
    /// </summary>
    public Dictionary<string, List<DelverDecor>> Decor { get; set; } = new();

    /// <summary>The potion items whose effects are shuffled across them each run, in order.</summary>
    public List<string> PotionColours { get; set; } = new();
}

/// <summary>One kind of decoration: its sprite, how often it is chosen, where it hangs, whether it blocks.</summary>
public sealed class DelverDecor
{
    public required string Sprite { get; init; }

    public int Weight { get; set; } = 1;

    public bool Ceiling { get; init; }

    public bool Solid { get; init; }
}

/// <summary>One enchantment, or a unique's fixed mods (then its id and slot are ignored).</summary>
public sealed class DelverEnchantment
{
    public string Id { get; set; } = "";

    public required string Name { get; init; }

    /// <summary>weaponPrefix, weaponSuffix, armorPrefix or armorSuffix.</summary>
    public string Slot { get; set; } = "weaponSuffix";

    public int AttackMod { get; init; }

    public int ArmorMod { get; init; }

    public int MagicMod { get; init; }

    public int MoveSpeedMod { get; init; }

    public int DamageMod { get; init; }

    public float AttackSpeedMod { get; init; }

    public float KnockbackMod { get; init; }

    public string DamageType { get; set; } = "Physical";
}

public sealed class DelverSwingStyle
{
    public DelverSwingTiming? Weak { get; init; }

    public DelverSwingTiming? Strong { get; init; }
}

public sealed class DelverSwingTiming
{
    public required float Length { get; init; }

    /// <summary>The donor animation's actionTime (6 when the donor omits it).</summary>
    public float ActionTime { get; set; } = 6f;
}

public sealed class DelverMonsterDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Sprite { get; set; } = "monster";

    public required int BaseHp { get; init; }

    public required int AttackPower { get; init; }

    public int AttackCooldownTicks { get; set; } = 45;

    public required int MonsterLevel { get; init; }

    /// <summary>A bolt cast from range; absent for a melee-only monster.</summary>
    public DelverMonsterRangedDefinition? Ranged { get; init; }

    /// <summary>False for a monster that holds its ground once alerted (donor chasetarget).</summary>
    public bool ChasesTarget { get; set; } = true;

    /// <summary>Stands still until it notices the player (donor AmbushMode.WaitToSee).</summary>
    public bool Ambushes { get; init; }

    /// <summary>Backs away from a close player (donor keepDistance).</summary>
    public bool KeepsDistance { get; init; }

    /// <summary>Base flinch chance on a hit (donor painChance).</summary>
    public float PainChance { get; set; } = 0.75f;

    /// <summary>Ticks a flinch holds it still: its donor hurt animation's length; 0 for none.</summary>
    public int HurtTicks { get; set; } = 22;

    /// <summary>Ticks from attack start to the donor damage frame; 0 hits at once.</summary>
    public int AttackWindupTicks { get; init; }

    /// <summary>Donor attackStartDistance.</summary>
    public float AttackStartDistance { get; set; } = 0.6f;

    /// <summary>Donor ImpulseAction lunge speed, tiles per tick; 0 for none.</summary>
    public float LungeSpeed { get; init; }

    /// <summary>Ticks into the attack the lunge comes.</summary>
    public int LungeAtTicks { get; init; }

    /// <summary>Shove its landed blow gives the player (donor DamageAction knockback).</summary>
    public float AttackKnockback { get; set; } = 0.05f;


    public int MinDungeonLevel { get; set; } = 1;

    public int MaxDungeonLevel { get; set; } = 99;

    public required int Attack { get; init; }

    public required int Defense { get; init; }

    public required int Dexterity { get; init; }

    public required int Speed { get; init; }

    public required int Magic { get; init; }

    public required int Endurance { get; init; }
}

/// <summary>A monster's ranged bolt (the donor monster's spell or projectile).</summary>
public sealed class DelverMonsterRangedDefinition
{
    public required int BaseDamage { get; init; }

    public int RandDamage { get; init; }

    /// <summary>Physical, Magic, Fire, Ice, Lightning, Poison, Paralyze.</summary>
    public string DamageType { get; set; } = "Magic";

    /// <summary>Bolt speed in tiles per tick.</summary>
    public required float Speed { get; init; }

    /// <summary>Ticks between casts (donor projectileAttackTime).</summary>
    public int CooldownTicks { get; set; } = 100;

    public float MinDistance { get; init; }

    public float MaxDistance { get; set; } = 30f;

    public string Sprite { get; set; } = "projectile.bolt";
}

public sealed class DelverItemDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Weapon, RangedWeapon, Wand, Ammo, Potion, Food, Gold, Key, Armor, Helmet, QuestOrb.</summary>
    public required string Kind { get; init; }

    public string Sprite { get; set; } = "item";

    /// <summary>A weapon's base damage; an armor piece's armor class.</summary>
    public int Power { get; init; }

    /// <summary>A weapon's random damage on top of its base (donor randDamage).</summary>
    public int RandDamage { get; init; }

    public int ChargeTicks { get; set; } = 40;

    /// <summary>Physical, Magic, Fire, Ice, Lightning, Poison, Paralyze.</summary>
    public string DamageType { get; set; } = "Physical";

    /// <summary>How hard a full-power blow or shot shoves (donor knockback).</summary>
    public float Knockback { get; init; }

    /// <summary>Donor weapon speed: scales swing playback.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>A swing style from the pack's swingStyles; empty for none.</summary>
    public string Swing { get; set; } = "";

    /// <summary>Uses before its condition drops a step (donor durability, 25 by default).</summary>
    public int Durability { get; set; } = 25;

    /// <summary>For a BagUpgrade: "hotbar" grows the hotbar, anything else the backpack.</summary>
    public string Bag { get; set; } = "backpack";

    /// <summary>A unique: found once a run from monster loot, never enchanted.</summary>
    public bool Unique { get; init; }

    /// <summary>A unique's fixed mods (donor baseMods).</summary>
    public DelverEnchantment? BaseMods { get; init; }

    /// <summary>A bow's range (donor Bow.range).</summary>
    public int Range { get; init; }

    /// <summary>A fresh wand's charges.</summary>
    public int Charges { get; init; }

    /// <summary>A wand that fires while held, once per this many ticks.</summary>
    public int AutoFireTicks { get; init; }

    /// <summary>A wand bolt's speed in tiles per tick.</summary>
    public float ProjectileSpeed { get; init; }

    /// <summary>A wand's aim, 1 dead on (donor shotAccuracy).</summary>
    public float Accuracy { get; set; } = 1f;

    /// <summary>How many one find holds (donor ItemStack count).</summary>
    public int StackSize { get; set; } = 1;

    /// <summary>The sprite a fired bolt flies as.</summary>
    public string ProjectileSprite { get; set; } = "";

    public int HealAmount { get; init; }

    public int Value { get; init; }

    public bool Stackable { get; init; }

    public int MinItemLevel { get; set; } = 1;

    public int MaxItemLevel { get; set; } = 99;
}

public sealed class DelverSectionDefinition
{
    public required string Name { get; init; }

    public required int DifficultyLevel { get; init; }

    public int SortOrder { get; init; }

    /// <summary>How many ordinary floors this section contributes.</summary>
    public required int Floors { get; init; }

    public List<DelverLevelTemplate> LevelTemplates { get; set; } = new();

    /// <summary>An optional themed floor appended after the ordinary floors.</summary>
    public DelverLevelTemplate? TransitionLevel { get; init; }
}

public sealed class DelverLevelTemplate
{
    public required string Theme { get; init; }

    public bool Generated { get; set; } = true;
}

public sealed class DelverLootDefinition
{
    public required string ItemId { get; init; }

    public int Weight { get; set; } = 1;

    public int MinItemLevel { get; set; } = 1;

    public int MaxItemLevel { get; set; } = 99;
}

/// <summary>JSON binding for pack documents (NativeAOT-safe source generation).</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(DelverContentPack))]
public sealed partial class DelverContentJsonContext : JsonSerializerContext;
