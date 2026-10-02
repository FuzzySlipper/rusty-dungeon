using DelveRpg.Kit.Rules;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Rulesets.Delver.Tests;

/// <summary>
/// A content document may omit any optional field and gets the schema's
/// default, never null or zero; an omission the schema cannot default fails
/// the load with the named all-or-nothing message.
/// </summary>
public sealed class OptionalFieldDefaultsTests
{
    private const string MinimalPack =
        """
        {
          "monsters": [
            {"id":"m.rat","displayName":"rat","baseHp":5,"attackPower":1,"monsterLevel":1,
             "attack":1,"defense":1,"dexterity":1,"speed":1,"magic":0,"endurance":1}
          ],
          "items": [{"id":"i.sword","displayName":"sword","kind":"Weapon"}],
          "sections": [
            {"name":"Test","difficultyLevel":1,"floors":1,"levelTemplates":[{"theme":"Test"}]}
          ],
          "loot": [{"itemId":"i.sword"}]
        }
        """;

    private const string Items = "\"items\": [{\"id\":\"i.sword\",\"displayName\":\"sword\",\"kind\":\"Weapon\"}],";
    private const string Loot = ",\n  \"loot\": [{\"itemId\":\"i.sword\"}]";
    private const string Templates = ",\"levelTemplates\":[{\"theme\":\"Test\"}]";

    private static Func<string, string?> Content(string pack, string tuning = "{}", string? bundle = null) => path => path switch
    {
        "delve/bundles/delve-run.json" => bundle ?? ContentFixtures.FixtureBundleJson,
        "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
        "delve/packs/test.json" => pack,
        "delve/tuning/test.json" => tuning,
        _ => null,
    };

    [Fact]
    public void Omitted_pack_fields_keep_their_schema_defaults()
    {
        DelverContentPack pack = DelverComposition.Load(Content(MinimalPack)).Pack;

        DelverMonsterDefinition monster = pack.Monsters.Single();
        Assert.Equal("monster", monster.Sprite);
        Assert.Equal(45, monster.AttackCooldownTicks);
        Assert.True(monster.ChasesTarget);
        Assert.Equal(0.75f, monster.PainChance);
        Assert.Equal(22, monster.HurtTicks);
        Assert.Equal(0.6f, monster.AttackStartDistance);
        Assert.Equal(0.05f, monster.AttackKnockback);
        Assert.Equal(1, monster.MinDungeonLevel);
        Assert.Equal(99, monster.MaxDungeonLevel);

        DelverItemDefinition item = pack.Items.Single();
        Assert.Equal("item", item.Sprite);
        Assert.Equal(40, item.ChargeTicks);
        Assert.Equal(1, item.MinItemLevel);
        Assert.Equal(99, item.MaxItemLevel);

        DelverLootDefinition loot = pack.Loot.Single();
        Assert.Equal(1, loot.Weight);
        Assert.Equal(1, loot.MinItemLevel);
        Assert.Equal(99, loot.MaxItemLevel);

        Assert.True(pack.Sections.Single().LevelTemplates.Single().Generated);
        Assert.Empty(pack.StartingKit);
    }

    [Fact]
    public void Omitted_pack_lists_load_empty_or_fail_by_name()
    {
        string withoutLootOrKit = MinimalPack.Replace(Loot, string.Empty);
        Assert.Empty(DelverComposition.Load(Content(withoutLootOrKit)).Pack.Loot);

        string withoutItems = MinimalPack.Replace(Items, string.Empty).Replace(Loot, string.Empty);
        AssertRefusedNaming(Content(withoutItems), "defines no items");

        string withoutTemplates = MinimalPack.Replace(Templates, string.Empty);
        AssertRefusedNaming(Content(withoutTemplates), "has no level templates");

        string bundleWithoutPacks =
            """{"kind":"delve.game-bundle","id":"delve.run","ruleset":"delver","tuning":"test"}""";
        AssertRefusedNaming(Content(MinimalPack, bundle: bundleWithoutPacks), "defines no monsters");
    }

    [Fact]
    public void An_empty_tuning_profile_is_the_kits_default_tuning()
    {
        GameTuning tuning = DelverComposition.Load(Content(MinimalPack)).Tuning;
        Assert.Equal(new GameTuning(), tuning);
    }

    private static void AssertRefusedNaming(Func<string, string?> content, string expected)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => DelverComposition.Load(content));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }
}
