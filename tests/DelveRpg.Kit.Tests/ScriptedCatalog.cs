using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Tests;

/// <summary>A minimal catalog so session tests never need the Delver ruleset.</summary>
public sealed class ScriptedCatalog : IRulesCatalog
{
    private readonly Dictionary<string, MonsterArchetype> _monsters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemArchetype> _items = new(StringComparer.Ordinal);
    private readonly List<(string ItemId, int Weight, int Min, int Max)> _loot = new();

    public ScriptedCatalog()
    {
        _monsters["test.monster.rat"] = new MonsterArchetype(
            "test.monster.rat", "rat", new StatBlock(2, 1, 5, 5, 0, 2), 6, 2, 40, 1, 8, "monster.rat");
        _items["test.item.sword"] = new ItemArchetype("test.item.sword", "sword", ItemKind.Weapon, 4, 30, 0, 5, "item.sword")
        {
            Knockback = 0.4f,
        };
        _items["test.item.potion"] = new ItemArchetype("test.item.potion", "potion", ItemKind.Potion, 0, 0, 10, 3, "item.potion");
        _items["test.item.gold"] = new ItemArchetype("test.item.gold", "gold", ItemKind.Gold, 0, 0, 0, 25, "item.gold");
        _items["test.item.orb"] = new ItemArchetype("test.item.orb", "the orb", ItemKind.QuestOrb, 0, 0, 0, 0, "item.orb");
        _items["test.item.bow"] = new ItemArchetype("test.item.bow", "bow", ItemKind.RangedWeapon, 3, 36, 0, 25, "item.bow", 6)
        {
            Range = 8,
        };
        _items["test.item.arrows"] = new ItemArchetype("test.item.arrows", "arrows", ItemKind.Ammo, 0, 0, 0, 10, "item.arrow")
        {
            StackSize = 6,
        };
        _items["test.item.wand"] = new ItemArchetype("test.item.wand", "wand", ItemKind.Wand, 1, 40, 0, 40, "item.wand", 1)
        {
            DamageType = DamageType.Magic,
            Charges = 3,
            AutoFireTicks = 15,
            ProjectileSpeed = 0.45f,
            ProjectileSpriteId = "projectile.spark",
        };
        _items["test.item.fire-wand"] = _items["test.item.wand"] with
        {
            Id = "test.item.fire-wand",
            DamageType = DamageType.Fire,
            AutoFireTicks = 0,
        };
        _monsters["test.monster.eye"] = new MonsterArchetype(
            "test.monster.eye", "eye", new StatBlock(4, 0, 1, 2, 0, 12), 2, 0, 60, 1, 8, "monster.golem")
        {
            ChasesTarget = false,
            Ranged = new MonsterRangedAttack(3, 3, DamageType.Magic, 0.1f, 100, 0f, 30f, "projectile.eyebolt"),
        };
        _monsters["test.monster.skulker"] = _monsters["test.monster.rat"] with
        {
            Id = "test.monster.skulker",
            KeepsDistance = true,
        };
        _loot.Add(("test.item.potion", 1, 1, 99));
    }

    public MonsterArchetype? Monster(string id) =>
        _monsters.TryGetValue(id, out MonsterArchetype? value) ? value : null;

    public ItemArchetype? Item(string id) =>
        _items.TryGetValue(id, out ItemArchetype? value) ? value : null;

    public IReadOnlyList<string> MonstersForFloor(int dungeonLevel) => ["test.monster.rat"];

    public IReadOnlyList<string> ItemsForFloor(int dungeonLevel) => ["test.item.potion", "test.item.gold"];

    public string? RollLootId(IRandomSource random, int itemLevel) => "test.item.potion";

    public string ObjectiveItemId => "test.item.orb";

    public bool IsStackable(string id) =>
        Item(id)?.Kind is ItemKind.Gold or ItemKind.Key or ItemKind.Potion or ItemKind.Food or ItemKind.Ammo;
}
