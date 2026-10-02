using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Rulesets.Delver.Content;

namespace DelveRpg.Rulesets.Delver;

/// <summary>
/// The Kit's catalog seam, answered from one resolved Delver content pack.
/// Level windows follow the donor's idea that only level-appropriate monsters
/// and loot appear on a floor.
/// </summary>
public sealed class DelverCatalog : IRulesCatalog
{
    private readonly Dictionary<string, MonsterArchetype> _monsters;
    private readonly Dictionary<string, ItemArchetype> _items;
    private readonly Dictionary<string, DelverItemDefinition> _itemDefinitions;
    private readonly List<DelverMonsterDefinition> _monsterDefinitions;
    private readonly List<DelverLootDefinition> _loot;

    public DelverCatalog(DelverContentPack pack)
    {
        _monsterDefinitions = pack.Monsters;
        _loot = pack.Loot;
        _itemDefinitions = pack.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _monsters = pack.Monsters.ToDictionary(
            monster => monster.Id,
            monster => new MonsterArchetype(
                monster.Id,
                monster.DisplayName,
                new StatBlock(monster.Attack, monster.Defense, monster.Dexterity, monster.Speed, monster.Magic, monster.Endurance),
                monster.BaseHp,
                monster.AttackPower,
                monster.AttackCooldownTicks,
                monster.MonsterLevel,
                monster.DetectRange,
                monster.Sprite)
            {
                Ranged = monster.Ranged is DelverMonsterRangedDefinition ranged
                    ? new MonsterRangedAttack(
                        ranged.BaseDamage,
                        ranged.RandDamage,
                        ParseDamageType(ranged.DamageType),
                        ranged.Speed,
                        ranged.CooldownTicks,
                        ranged.MinDistance,
                        ranged.MaxDistance,
                        ranged.Sprite)
                    : null,
                ChasesTarget = monster.ChasesTarget,
                KeepsDistance = monster.KeepsDistance,
                PainChance = monster.PainChance,
                HurtTicks = monster.HurtTicks,
                AttackKnockback = monster.AttackKnockback,
            },
            StringComparer.Ordinal);
        _items = pack.Items.ToDictionary(
            item => item.Id,
            item => new ItemArchetype(
                item.Id,
                item.DisplayName,
                ParseKind(item.Kind),
                item.Power,
                item.ChargeTicks,
                item.HealAmount,
                item.Value,
                item.Sprite,
                item.RandDamage)
            {
                DamageType = ParseDamageType(item.DamageType),
                Knockback = item.Knockback,
                Range = item.Range,
                Charges = item.Charges,
                AutoFireTicks = item.AutoFireTicks,
                ProjectileSpeed = item.ProjectileSpeed,
                Accuracy = item.Accuracy,
                StackSize = item.StackSize,
                ProjectileSpriteId = item.ProjectileSprite,
            },
            StringComparer.Ordinal);
    }

    public MonsterArchetype? Monster(string id) =>
        _monsters.TryGetValue(id, out MonsterArchetype? archetype) ? archetype : null;

    public ItemArchetype? Item(string id) =>
        _items.TryGetValue(id, out ItemArchetype? archetype) ? archetype : null;

    public IReadOnlyList<string> MonstersForFloor(int dungeonLevel) =>
        _monsterDefinitions
            .Where(monster => dungeonLevel >= monster.MinDungeonLevel && dungeonLevel <= monster.MaxDungeonLevel)
            .Select(monster => monster.Id)
            .ToList();

    public IReadOnlyList<string> ItemsForFloor(int dungeonLevel) =>
        _itemDefinitions.Values
            .Where(item => item.Kind is not ("QuestOrb" or "Key")
                && dungeonLevel >= item.MinItemLevel && dungeonLevel <= item.MaxItemLevel)
            .Select(item => item.Id)
            .ToList();

    public string? RollLootId(IRandomSource random, int itemLevel)
    {
        List<DelverLootDefinition> eligible = _loot
            .Where(entry => itemLevel >= entry.MinItemLevel && itemLevel <= entry.MaxItemLevel)
            .ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        int totalWeight = eligible.Sum(entry => Math.Max(1, entry.Weight));
        int draw = random.Next(0, totalWeight);
        foreach (DelverLootDefinition entry in eligible)
        {
            draw -= Math.Max(1, entry.Weight);
            if (draw < 0)
            {
                return entry.ItemId;
            }
        }

        return eligible[^1].ItemId;
    }

    public string ObjectiveItemId => "delve.item.orb";

    public bool IsStackable(string id) =>
        _items.TryGetValue(id, out ItemArchetype? archetype)
        && archetype.Kind is ItemKind.Gold or ItemKind.Key or ItemKind.Potion or ItemKind.Food or ItemKind.Ammo;

    /// <summary>A damage type name, or null when the name is not one.</summary>
    public static DamageType? TryParseDamageType(string name) =>
        Enum.TryParse<DamageType>(name, ignoreCase: true, out DamageType parsed) && Enum.IsDefined(parsed) ? parsed : null;

    private static DamageType ParseDamageType(string name) =>
        TryParseDamageType(name) ?? throw new InvalidOperationException($"Unknown damage type '{name}'.");

    private static ItemKind ParseKind(string kind) =>
        Enum.TryParse<ItemKind>(kind, ignoreCase: true, out ItemKind parsed)
            ? parsed
            : throw new InvalidOperationException($"Unknown item kind '{kind}'.");
}
