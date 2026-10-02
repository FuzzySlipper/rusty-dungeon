using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Rules;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Rulesets.Delver.Tests;

/// <summary>The shipped enchantment tables, uniques and potion colours.</summary>
public sealed class ItemContentTests
{
    private static readonly DelverCatalog Catalog = new(DelverComposition.Load(ContentFixtures.RealContent()).Pack);

    [Fact]
    public void The_four_donor_enchantment_tables_are_shipped()
    {
        Assert.Equal(14, Catalog.Modifications(ModificationSlot.WeaponSuffix).Count);
        Assert.Equal(19, Catalog.Modifications(ModificationSlot.WeaponPrefix).Count);
        Assert.Equal(10, Catalog.Modifications(ModificationSlot.ArmorSuffix).Count);
        Assert.Equal(23, Catalog.Modifications(ModificationSlot.ArmorPrefix).Count);
        ItemModification burning = Catalog.Modifications(ModificationSlot.WeaponSuffix).Single(mod => mod.Name == "of Burning");
        Assert.Equal((5, DamageType.Fire), (burning.DamageMod, burning.DamageType));
    }

    [Fact]
    public void Uniques_drop_only_as_uniques_and_carry_their_fixed_mods()
    {
        Assert.Equal(7, Catalog.UniqueItemIds.Count); // five weapons, the bag and the belt pouch
        for (int level = 1; level <= 16; level++)
        {
            Assert.DoesNotContain(Catalog.ItemsForFloor(level), id => Catalog.UniqueItemIds.Contains(id));
        }

        ItemArchetype ashen = Catalog.Item("delve.item.ashen")!;
        Assert.True(ashen.Unique);
        Assert.Equal(DamageType.Fire, ashen.DamageType);
        Assert.Equal(2, ashen.BaseMods!.ArmorMod);
        Assert.Equal(1000, Catalog.Item("delve.item.lucky-dagger")!.Durability);
    }

    [Fact]
    public void Seven_potion_colours_share_out_the_seven_effects()
    {
        Assert.Equal(7, Catalog.PotionColourIds.Count);
        Assert.Equal(Enum.GetValues<PotionEffect>().Length, Catalog.PotionColourIds.Count);
        Assert.All(Catalog.PotionColourIds, id => Assert.Equal(ItemKind.Potion, Catalog.Item(id)!.Kind));
    }

    [Fact]
    public void A_bad_enchantment_slot_is_named_at_load()
    {
        string pack = ContentFixtures.FixturePackJson.Replace(
            "\"monsters\": [",
            "\"enchantments\": [{\"id\":\"e.x\",\"name\":\"of Nothing\",\"slot\":\"ringSuffix\"}],\n  \"monsters\": [");
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => ContentFixtures.FixtureBundleJson,
            "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
            "delve/packs/test.json" => pack,
            "delve/tuning/test.json" => ContentFixtures.FixtureTuningJson,
            _ => null,
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("of Nothing", error.Message, StringComparison.Ordinal);
    }
}
