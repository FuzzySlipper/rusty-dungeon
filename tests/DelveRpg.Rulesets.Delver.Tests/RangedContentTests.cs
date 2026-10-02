using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Rules;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Rulesets.Delver.Tests;

/// <summary>The shipped pack's bows, wands and casting monster, and what a broken one names.</summary>
public sealed class RangedContentTests
{
    private static readonly DelverCatalog Catalog =
        new(DelverComposition.Load(ContentFixtures.RealContent()).Pack);

    [Fact]
    public void The_hunting_bow_shoots_stacked_arrows_and_the_wand_carries_magic_charges()
    {
        ItemArchetype bow = Catalog.Item("delve.item.hunting-bow")!;
        Assert.Equal(ItemKind.RangedWeapon, bow.Kind);
        Assert.Equal(7, bow.Range);

        ItemArchetype arrows = Catalog.Item("delve.item.arrows")!;
        Assert.Equal(ItemKind.Ammo, arrows.Kind);
        Assert.True(Catalog.IsStackable(arrows.Id));
        Assert.True(arrows.StackSize > 1);

        ItemArchetype wand = Catalog.Item("delve.item.wand-of-sparks")!;
        Assert.Equal(DamageType.Magic, wand.DamageType);
        Assert.Equal(30, wand.Charges);
        Assert.True(wand.ProjectileSpeed > 0f);
        Assert.NotEmpty(wand.ProjectileSpriteId);
    }

    [Fact]
    public void The_stone_golem_casts_from_where_it_stands_and_the_donor_melee_monsters_do_not()
    {
        MonsterArchetype golem = Catalog.Monster("delve.monster.stone-golem")!;
        Assert.False(golem.ChasesTarget);
        MonsterRangedAttack bolt = Assert.IsType<MonsterRangedAttack>(golem.Ranged);
        Assert.Equal((3, 3, DamageType.Magic), (bolt.BaseDamage, bolt.RandDamage, bolt.DamageType));

        Assert.Null(Catalog.Monster("delve.monster.giant-spider")!.Ranged);
        Assert.Null(Catalog.Monster("delve.monster.wraith")!.Ranged);
    }

    [Fact]
    public void Shipped_weapons_carry_the_donor_swing_timings()
    {
        ItemArchetype dagger = Catalog.Item("delve.item.rusty-dagger")!;
        Assert.Equal("dagger", dagger.SwingStyle);
        Assert.Equal(new SwingTiming(7.5f, 5f), dagger.WeakSwing);
        Assert.Equal(new SwingTiming(11.25f, 6.5f), dagger.StrongSwing);
        Assert.Equal(0.5f, Catalog.Item("delve.item.wand-of-sparks")!.Speed);
        Assert.Null(Catalog.Item("delve.item.hunting-bow")!.StrongSwing);
    }

    [Fact]
    public void An_unknown_damage_type_and_a_bow_without_range_are_named_at_load()
    {
        string pack = ContentFixtures.FixturePackJson.Replace(
            """{"id":"i.sword","displayName":"sword","kind":"Weapon","power":3},""",
            """
            {"id":"i.sword","displayName":"sword","kind":"Weapon","power":3,"damageType":"Plasma"},
            {"id":"i.bow","displayName":"bow","kind":"RangedWeapon","power":3,"swing":"lute"},
            """);
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => ContentFixtures.FixtureBundleJson,
            "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
            "delve/packs/test.json" => pack,
            "delve/tuning/test.json" => ContentFixtures.FixtureTuningJson,
            _ => null,
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("Plasma", error.Message, StringComparison.Ordinal);
        Assert.Contains("'i.bow' has no range", error.Message, StringComparison.Ordinal);
        Assert.Contains("unknown swing style 'lute'", error.Message, StringComparison.Ordinal);
    }
}
