using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Session;

/// <summary>
/// What makes one find differ from another of its kind: condition, prefix
/// and suffix enchantments, wear, uniques, and potions whose effects are
/// shuffled across their colours each run.
/// </summary>
public sealed partial class RunSession
{
    /// <summary>[donor] managers/ItemManager.java:77-103: each enchantment slot rolls nextFloat() > 0.8.</summary>
    private const double EnchantRollChance = 0.2;

    /// <summary>The same file: an enchanted find is unidentified one time in five.</summary>
    private const double UnidentifiedChance = 0.2;

    private readonly HashSet<string> _spawnedUniques = new(StringComparer.Ordinal);
    private readonly HashSet<PotionEffect> _knownPotions = new();
    private Dictionary<string, PotionEffect>? _potionEffects;

    /// <summary>The potion effects this run's drinking has identified.</summary>
    public IReadOnlyCollection<PotionEffect> KnownPotions => _knownPotions;

    /// <summary>The uniques already found this run; each turns up once at most.</summary>
    public IReadOnlyCollection<string> SpawnedUniques => _spawnedUniques;

    /// <summary>
    /// A newly found item: a bundle of its stack size and a wand at full
    /// charge. A weapon or armor piece also rolls the donor's condition
    /// (worn, normal, fine or excellent, never broken) and its prefix and
    /// suffix, each one time in five; an enchanted find is unidentified one
    /// time in five ([donor] managers/ItemManager.java:77-142, :194-244).
    /// Its item level is the floor's, held to the item's level window.
    /// </summary>
    private ItemInstance Fresh(ItemArchetype archetype)
    {
        var item = new ItemInstance(archetype.Id, Math.Max(1, archetype.StackSize), archetype.Charges)
        {
            ItemLevel = Math.Clamp(Level?.DifficultyLevel ?? 1, archetype.MinItemLevel, archetype.MaxItemLevel),
        };
        if (archetype.Unique || archetype.Kind is not (ItemKind.Weapon or ItemKind.RangedWeapon or ItemKind.Armor or ItemKind.Helmet))
        {
            return item;
        }

        bool weapon = archetype.Kind is ItemKind.Weapon or ItemKind.RangedWeapon;
        item = item with { Condition = (ItemCondition)(_random.Next(1, 5) - 2) };
        if (RollModification(weapon ? ModificationSlot.WeaponSuffix : ModificationSlot.ArmorSuffix) is ItemModification suffix)
        {
            item = item with { Suffix = suffix.Id, Unidentified = item.Unidentified || _random.Chance(UnidentifiedChance) };
        }

        if (RollModification(weapon ? ModificationSlot.WeaponPrefix : ModificationSlot.ArmorPrefix) is ItemModification prefix)
        {
            item = item with { Prefix = prefix.Id, Unidentified = item.Unidentified || _random.Chance(UnidentifiedChance) };
        }

        return item;
    }

    private ItemModification? RollModification(ModificationSlot slot)
    {
        IReadOnlyList<ItemModification> table = _rules.Modifications(slot);
        if (table.Count == 0 || !_random.Chance(EnchantRollChance))
        {
            return null;
        }

        return table[_random.Next(0, table.Count)];
    }

    /// <summary>The mods that apply: a unique's fixed ones always, enchantments once identified.</summary>
    private IEnumerable<ItemModification> ActiveMods(ItemArchetype archetype, ItemInstance item)
    {
        if (archetype.BaseMods is ItemModification fixedMods)
        {
            yield return fixedMods;
        }

        if (item.Unidentified || item.Condition == ItemCondition.Broken)
        {
            yield break;
        }

        foreach (string? id in new[] { item.Prefix, item.Suffix })
        {
            if (id is not null && _rules.Modification(id) is ItemModification mod)
            {
                yield return mod;
            }
        }
    }

    /// <summary>
    /// One weapon's numbers as the donor's Weapon works them out
    /// ([donor] entities/items/Weapon.java:153-192): base damage moves with
    /// condition (−4..+4), physical damage mods and <c>0.75 × item level</c>
    /// past level 1; the random part grows by the same level share; an
    /// elemental mod adds its damage on top and makes the blow its type.
    /// </summary>
    public WeaponNumbers WeaponNumbersFor(ItemArchetype archetype, ItemInstance item)
    {
        int levelBonus = item.ItemLevel > 1 ? (int)(item.ItemLevel * 0.75f) : 0;
        int baseDamage = archetype.Power + ((int)item.Condition * 2) + levelBonus;
        int elemental = 0;
        DamageType damageType = archetype.DamageType;
        float attackSpeed = 0f;
        float knockback = 0f;
        foreach (ItemModification mod in ActiveMods(archetype, item))
        {
            int damage = mod.Scaled(mod.DamageMod, item.ItemLevel);
            if (mod.DamageType == DamageType.Physical)
            {
                baseDamage += damage;
            }
            else
            {
                elemental += damage;
                damageType = mod.DamageType;
            }

            attackSpeed += mod.Scaled(mod.AttackSpeedMod, item.ItemLevel);
            knockback += mod.Scaled(mod.KnockbackMod, item.ItemLevel);
        }

        return new WeaponNumbers(Math.Max(1, baseDamage), archetype.RandDamage + levelBonus, elemental, damageType, attackSpeed, knockback);
    }

    /// <summary>The wielded weapon's instance, when the wielded slot still holds it.</summary>
    private ItemInstance? WieldedInstance(ItemArchetype weapon) =>
        Player.WieldedSlot >= 0 && Player.Inventory.Slot(Player.WieldedSlot) is ItemInstance held && held.ArchetypeId == weapon.Id
            ? held
            : null;

    private WeaponNumbers WieldedNumbers(ItemArchetype weapon) =>
        WeaponNumbersFor(weapon, WieldedInstance(weapon) ?? new ItemInstance(weapon.Id, 1));

    /// <summary>
    /// The player's stats with the attack and magic mods of what they wield
    /// and wear ([donor] rpg/Stats.java:105-130 addItemStats).
    /// </summary>
    public StatBlock EffectiveStats()
    {
        StatBlock stats = Player.Body.Stats;
        foreach ((ItemArchetype archetype, ItemInstance item) in EquippedItems())
        {
            foreach (ItemModification mod in ActiveMods(archetype, item))
            {
                stats = stats with
                {
                    Attack = stats.Attack + mod.Scaled(mod.AttackMod, item.ItemLevel),
                    Magic = stats.Magic + mod.Scaled(mod.MagicMod, item.ItemLevel),
                };
            }
        }

        return stats;
    }

    private IEnumerable<(ItemArchetype Archetype, ItemInstance Item)> EquippedItems()
    {
        foreach (int slot in new[] { Player.WieldedSlot, Player.ArmorSlot, Player.HelmetSlot })
        {
            if (slot >= 0 && slot < Player.Inventory.Capacity
                && Player.Inventory.Slot(slot) is ItemInstance item
                && _rules.Item(item.ArchetypeId) is ItemArchetype archetype)
            {
                yield return (archetype, item);
            }
        }
    }

    /// <summary>
    /// Armor class from what is worn: each piece's armor, moved by its
    /// condition (−2..+2), plus armor mods from anything equipped
    /// ([donor] entities/items/Armor.java:44-61).
    /// </summary>
    private int GearArmorClass()
    {
        int armor = 0;
        foreach ((ItemArchetype archetype, ItemInstance item) in EquippedItems())
        {
            if (archetype.Kind is ItemKind.Armor or ItemKind.Helmet)
            {
                armor += archetype.Power + (int)item.Condition;
            }

            foreach (ItemModification mod in ActiveMods(archetype, item))
            {
                armor += mod.Scaled(mod.ArmorMod, item.ItemLevel);
            }
        }

        return Math.Max(0, armor);
    }

    /// <summary>
    /// Use wears an item: after its durability in uses its condition drops a
    /// step, and a broken item loses its enchantments
    /// ([donor] entities/Item.java wasUsed, Weapon.java:107-133).
    /// </summary>
    private void Wear(int slot)
    {
        if (slot < 0 || Player.Inventory.Slot(slot) is not ItemInstance item
            || _rules.Item(item.ArchetypeId) is not ItemArchetype archetype
            || item.Condition == ItemCondition.Broken)
        {
            return;
        }

        if (item.Wear + 1 < archetype.Durability)
        {
            Player.Inventory.RestoreSlot(slot, item with { Wear = item.Wear + 1 });
            return;
        }

        ItemInstance worn = item with { Wear = 0, Condition = item.Condition - 1 };
        if (worn.Condition == ItemCondition.Broken)
        {
            worn = worn with { Prefix = null, Suffix = null, Unidentified = false };
            ShowMessage($"Your {archetype.DisplayName} breaks!");
        }
        else
        {
            ShowMessage($"Your {archetype.DisplayName} is wearing down.");
        }

        Player.Inventory.RestoreSlot(slot, worn);
    }

    /// <summary>
    /// Equipping makes an unidentified item's enchantments known. The donor
    /// needs an Identify scroll or a shop for that; there are neither here.
    /// </summary>
    private void IdentifyOnEquip(int slot)
    {
        if (slot >= 0 && Player.Inventory.Slot(slot) is ItemInstance { Unidentified: true } item)
        {
            ItemInstance known = item with { Unidentified = false };
            Player.Inventory.RestoreSlot(slot, known);
            ShowMessage($"You recognise the {ItemName(known)}.");
        }
    }

    /// <summary>
    /// An item's name the donor's way, "{prefix} {condition} {name}
    /// {suffix}": normal condition is not named, enchantments show only once
    /// known, and a suffix is skipped when the name already has "of"
    /// ([donor] entities/Item.java:463-483). A potion is its colour, with
    /// its effect once known.
    /// </summary>
    public string ItemName(ItemInstance item)
    {
        if (_rules.Item(item.ArchetypeId) is not ItemArchetype archetype)
        {
            return item.ArchetypeId;
        }

        if (archetype.Kind == ItemKind.Potion && PotionEffectOf(archetype.Id) is PotionEffect effect && _knownPotions.Contains(effect))
        {
            return $"{archetype.DisplayName} of {PotionName(effect)}";
        }

        var parts = new List<string>();
        if (!item.Unidentified && item.Prefix is string prefixId && _rules.Modification(prefixId) is ItemModification prefix)
        {
            parts.Add(prefix.Name);
        }

        if (item.Condition != ItemCondition.Normal)
        {
            parts.Add(item.Condition.ToString().ToLowerInvariant());
        }

        parts.Add(archetype.DisplayName);
        if (!item.Unidentified && item.Suffix is string suffixId && _rules.Modification(suffixId) is ItemModification suffix
            && !archetype.DisplayName.Contains(" of ", StringComparison.Ordinal))
        {
            parts.Add(suffix.Name);
        }

        string name = string.Join(' ', parts);
        return item.Unidentified ? $"{name} (unidentified)" : name;
    }

    /// <summary>
    /// A unique turns up in monster loot at <c>2% × min(1, level / 6)</c>,
    /// one not yet found this run ([donor] managers/ItemManager.java:150-177, 406-417).
    /// </summary>
    private string? RollUnique()
    {
        float chance = 0.02f * Math.Min(1f, Level.DifficultyLevel / 6f);
        if (!_random.Chance(chance))
        {
            return null;
        }

        List<string> unfound = _rules.UniqueItemIds.Where(id => !_spawnedUniques.Contains(id)).ToList();
        if (unfound.Count == 0)
        {
            return null;
        }

        string found = unfound[_random.Next(0, unfound.Count)];
        _spawnedUniques.Add(found);
        return found;
    }

    /// <summary>
    /// This run's colour-to-effect deal: the colours are shuffled once per
    /// run from its seed and handed the effects in order
    /// ([donor] managers/ItemManager.java:295-316).
    /// </summary>
    private PotionEffect? PotionEffectOf(string colourId)
    {
        if (_potionEffects is null)
        {
            IReadOnlyList<string> colours = _rules.PotionColourIds;
            var order = colours.ToList();
            var shuffle = new SplitMixRandom(_runSeed ^ 0x9E3779B97F4A7C15UL);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = shuffle.Next(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            PotionEffect[] effects = Enum.GetValues<PotionEffect>();
            _potionEffects = new Dictionary<string, PotionEffect>(StringComparer.Ordinal);
            for (int i = 0; i < order.Count && i < effects.Length; i++)
            {
                _potionEffects[order[i]] = effects[i];
            }
        }

        return _potionEffects.TryGetValue(colourId, out PotionEffect effect) ? effect : null;
    }

    /// <summary>
    /// Drink a potion: its effect for this run applies, and drinking
    /// identifies it half the time ([donor] entities/items/Potion.java:37-82, 203-221).
    /// A potion outside the shuffle heals by its authored amount.
    /// </summary>
    private void DrinkPotion(ItemArchetype potion)
    {
        if (PotionEffectOf(potion.Id) is not PotionEffect effect)
        {
            Player.Body.Hp = Math.Min(Player.Body.MaxHp, Player.Body.Hp + potion.HealAmount);
            ShowMessage($"You drink the {potion.DisplayName}.");
            return;
        }

        EffectSet effects = Player.Body.Effects;
        switch (effect)
        {
            case PotionEffect.Healing:
                Player.Body.Hp = Math.Min(Player.Body.MaxHp, Player.Body.Hp + _random.Next(4, 9));
                break;
            case PotionEffect.ResistMagic:
                effects.Apply(EffectKind.MagicResist, 1000, 0);
                break;
            case PotionEffect.Restoration:
                Player.Body.Hp = Player.Body.MaxHp;
                effects.Clear();
                break;
            case PotionEffect.Poison:
                effects.Apply(EffectKind.Poison, 1600, 1, intervalTicks: 160);
                break;
            case PotionEffect.Regeneration:
                Player.Body.Hp = Math.Min(Player.Body.MaxHp, Player.Body.Hp + 1);
                effects.Apply(EffectKind.Regenerating, 1600, 1, intervalTicks: 160);
                break;
            case PotionEffect.IronSkin:
                effects.Apply(EffectKind.IronSkin, 1000, 0);
                break;
            case PotionEffect.Paralysis:
                effects.Apply(EffectKind.Paralyzed, 1000, 0);
                break;
        }

        bool known = _knownPotions.Contains(effect);
        if (!known && _random.Chance(0.5))
        {
            _knownPotions.Add(effect);
            ShowMessage($"That must be a potion of {PotionName(effect)}.");
        }
        else
        {
            ShowMessage(known ? $"You drink the potion of {PotionName(effect)}." : $"You drink the {potion.DisplayName}.");
        }
    }

    /// <summary>[data] strings.dat potion names.</summary>
    public static string PotionName(PotionEffect effect) => effect switch
    {
        PotionEffect.Healing => "Healing",
        PotionEffect.ResistMagic => "Resist Magic",
        PotionEffect.Restoration => "Restoration",
        PotionEffect.Poison => "Poison",
        PotionEffect.Regeneration => "Regeneration",
        PotionEffect.IronSkin => "Iron Skin",
        _ => "Paralyzation",
    };
}

/// <summary>A wielded weapon's worked-out numbers.</summary>
public readonly record struct WeaponNumbers(
    int BaseDamage,
    int RandDamage,
    int ElementalDamage,
    DamageType DamageType,
    float AttackSpeedBonus,
    float KnockbackBonus);
