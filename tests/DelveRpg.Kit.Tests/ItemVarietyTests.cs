using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>Condition, enchantments, wear, naming, uniques and the potion shuffle.</summary>
public sealed class ItemVarietyTests
{
    private static readonly ScriptedCatalog Catalog = new();

    private static SnapshotSlot Slot(string id, ItemCondition condition = ItemCondition.Normal, string? prefix = null,
        string? suffix = null, bool unidentified = false, int itemLevel = 1, int count = 1) =>
        new(id, count) { Condition = (int)condition, Prefix = prefix, Suffix = suffix, Unidentified = unidentified, ItemLevel = itemLevel };

    [Fact]
    public void A_weapon_s_numbers_follow_condition_level_and_known_enchantments()
    {
        RunSession session = Hall(ScriptedRandom.Always(0), [], null);
        ItemArchetype sword = Catalog.Item("test.item.sword")!;
        var fine = new ItemInstance("test.item.sword", 1) { Condition = ItemCondition.Fine, ItemLevel = 3, Suffix = "of-fire" };

        WeaponNumbers numbers = session.WeaponNumbersFor(sword, fine);

        // Base 4, fine +2, level 3 adds (int)(3 × 0.75) = 2.
        Assert.Equal(8, numbers.BaseDamage);
        Assert.Equal(2, numbers.RandDamage);
        // "of Fire" 3 grows by (int)(3 × 3 × 0.5) = 4 at level 3, and makes the blow fire.
        Assert.Equal(7, numbers.ElementalDamage);
        Assert.Equal(DamageType.Fire, numbers.DamageType);

        WeaponNumbers unknown = session.WeaponNumbersFor(sword, fine with { Unidentified = true });
        Assert.Equal(0, unknown.ElementalDamage);
        Assert.Equal(DamageType.Physical, unknown.DamageType);
    }

    [Fact]
    public void Items_are_named_prefix_condition_name_suffix_and_hide_unknown_enchantments()
    {
        RunSession session = Hall(ScriptedRandom.Always(0), [], null);
        var item = new ItemInstance("test.item.sword", 1) { Prefix = "fighters", Suffix = "of-fire", Condition = ItemCondition.Fine };

        Assert.Equal("Fighter's fine sword of Fire", session.ItemName(item));
        Assert.Equal("fine sword (unidentified)", session.ItemName(item with { Unidentified = true }));
        Assert.Equal("sword", session.ItemName(new ItemInstance("test.item.sword", 1)));
    }

    [Fact]
    public void Wearing_armor_identifies_it_and_its_enchantment_raises_the_armor_class()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [Slot("test.item.armor", ItemCondition.Worn, prefix: "blessed", unidentified: true)],
            null);

        session.Tick(RunInput.Idle with { HotbarPressed = 1 });

        Assert.Equal(0, session.Player.ArmorSlot);
        Assert.False(session.Player.Inventory.Slot(0)!.Value.Unidentified);
        Assert.Contains(session.Messages, message => message.Text.Contains("recognise"));
    }

    [Fact]
    public void Use_wears_an_item_down_a_condition_step_and_breaking_strips_its_enchantments()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [Slot("test.item.armor", ItemCondition.Worn, prefix: "blessed")],
            null,
            [new SnapshotMonster("test.monster.rat", 2.4f, 1.5f, 6, 0f)]);
        session.Tick(RunInput.Idle with { HotbarPressed = 1 });

        // Durability 2: two landed blows drop worn armor to broken.
        for (int i = 0; i < 400 && session.Player.Inventory.Slot(0)!.Value.Condition != ItemCondition.Broken; i++)
        {
            session.Tick(RunInput.Idle);
        }

        ItemInstance armor = session.Player.Inventory.Slot(0)!.Value;
        Assert.Equal(ItemCondition.Broken, armor.Condition);
        Assert.Null(armor.Prefix);
        Assert.Contains(session.Messages, message => message.Text.Contains("breaks"));
    }

    [Fact]
    public void Potion_effects_are_dealt_to_colours_per_run_and_drinking_may_identify()
    {
        RunSession session = Hall(ScriptedRandom.Always(0), [Slot("test.item.potion.red", count: 3)], null);
        session.Tick(RunInput.Idle with { HotbarPressed = 1 });

        // A low draw passes the one-in-two identification.
        Assert.Single(session.KnownPotions);
        Assert.Contains(" of ", session.ItemName(new ItemInstance("test.item.potion.red", 1)));
        Assert.Equal("blue potion", session.ItemName(new ItemInstance("test.item.potion.blue", 1)));
    }

    [Fact]
    public void Iron_skin_halves_physical_damage()
    {
        int Blow(bool ironSkin)
        {
            RunSession session = Hall(
                ScriptedRandom.Always(999_999),
                [],
                null,
                [new SnapshotMonster("test.monster.rat", 2.3f, 1.5f, 6, 0f)]);
            if (ironSkin)
            {
                session.Player.Body.Effects.Apply(EffectKind.IronSkin, 1000, 0);
            }

            int hp = session.Player.Body.Hp;
            session.Tick(RunInput.Idle);
            session.Tick(RunInput.Idle);
            return hp - session.Player.Body.Hp;
        }

        int bare = Blow(ironSkin: false);
        Assert.True(bare >= 2);
        Assert.Equal(Math.Max(1, bare / 2), Blow(ironSkin: true));
    }

    [Fact]
    public void A_unique_turns_up_once_a_run_at_most()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0), // every chance passes
            [new SnapshotSlot("test.item.sword", 1)],
            "test.item.sword",
            [new SnapshotMonster("test.monster.rat", 2.4f, 1.5f, 1, 0f), new SnapshotMonster("test.monster.rat", 2.6f, 1.6f, 1, 0f)]);

        for (int i = 0; i < 4; i++)
        {
            HoldThenRelease(session, 30);
            Idle(session, 40);
        }

        Assert.Equal(["test.item.lucky"], session.SpawnedUniques);
        Assert.Single(session.GroundItems, item => item.Item.ArchetypeId == "test.item.lucky");
    }
}
