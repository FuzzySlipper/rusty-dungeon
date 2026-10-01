using DelveRpg.Rulesets.Delver;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Rulesets.Delver.Tests;

/// <summary>Shared helpers: the repository's real content tree and inline fixtures.</summary>
public static class ContentFixtures
{
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content", "delve")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository content tree not found.");
    }

    public static Func<string, string?> RealContent() => path =>
    {
        string full = Path.Combine(RepositoryRoot(), "content", path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    };

    public static Func<string, string?> Fixture(string bundle, string pack, string tuning) => path => path switch
    {
        "delve/bundles/delve-run.json" => bundle,
        "delve/content-packs/delve-core.json" => pack,
        "delve/packs/test.json" => FixturePackJson,
        "delve/tuning/test.json" => tuning,
        _ => null,
    };

    public const string FixtureBundleJson =
        """{"kind":"delve.game-bundle","id":"delve.run","ruleset":"delver","packs":["delve-core"],"tuning":"test"}""";

    public const string FixturePackDescriptorJson =
        """{"kind":"delver.content-pack","id":"delve-core","ruleset":"delver","payload":"packs/test.json"}""";

    public const string FixtureTuningJson =
        """{"attackChargeTicks":12,"startingGold":7}""";

    public const string FixturePackJson =
        """
        {
          "monsters": [
            {"id":"m.rat","displayName":"rat","baseHp":5,"attackPower":1,"monsterLevel":1,
             "minDungeonLevel":1,"maxDungeonLevel":3,"attack":1,"defense":1,"dexterity":1,"speed":1,"magic":0,"endurance":1}
          ],
          "items": [
            {"id":"i.sword","displayName":"sword","kind":"Weapon","power":3},
            {"id":"i.gold","displayName":"gold","kind":"Gold","value":10,"stackable":true},
            {"id":"i.orb","displayName":"orb","kind":"QuestOrb"}
          ],
          "sections": [
            {"name":"Test","difficultyLevel":2,"sortOrder":1,"floors":1,
             "levelTemplates":[{"theme":"Test"}],
             "transitionLevel":{"theme":"Test"}}
          ],
          "loot": [{"itemId":"i.gold","weight":2,"minItemLevel":1,"maxItemLevel":9}]
        }
        """;
}

public sealed class DelverCompositionTests
{
    [Fact]
    public void The_shipped_content_loads_and_validates()
    {
        DelverComposition composition = DelverComposition.Load(ContentFixtures.RealContent());

        Assert.Equal("delver", composition.Bundle.Ruleset);
        Assert.NotEmpty(composition.Pack.Monsters);
        Assert.NotEmpty(composition.Pack.Items);
        Assert.NotEmpty(composition.Pack.Sections);
        Assert.True(composition.Tuning.AttackChargeTicks > 0);
    }

    [Fact]
    public void The_shipped_pack_names_every_loot_item()
    {
        DelverComposition composition = DelverComposition.Load(ContentFixtures.RealContent());
        var itemIds = composition.Pack.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (DelverLootDefinition entry in composition.Pack.Loot)
        {
            Assert.Contains(entry.ItemId, itemIds);
        }
    }

    [Fact]
    public void A_bundle_for_another_ruleset_is_refused()
    {
        string bundle = """{"kind":"delve.game-bundle","id":"delve.run","ruleset":"other","packs":["delve-core"],"tuning":"test"}""";
        var load = () => DelverComposition.Load(ContentFixtures.Fixture(
            bundle, ContentFixtures.FixturePackDescriptorJson, ContentFixtures.FixtureTuningJson));
        Assert.Throws<InvalidOperationException>(load);
    }

    [Fact]
    public void A_pack_with_duplicate_ids_is_refused()
    {
        string pack = ContentFixtures.FixturePackJson.Replace(
            """{"id":"i.sword","displayName":"sword","kind":"Weapon","power":3}""",
            """{"id":"i.gold","displayName":"sword","kind":"Weapon","power":3}""");
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => ContentFixtures.FixtureBundleJson,
            "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
            "delve/packs/test.json" => pack,
            "delve/tuning/test.json" => ContentFixtures.FixtureTuningJson,
            _ => null,
        });
        Assert.Throws<InvalidOperationException>(load);
    }

    [Fact]
    public void A_starting_kit_entry_without_an_item_names_the_entry()
    {
        string pack = ContentFixtures.FixturePackJson.Replace(
            "\"monsters\": [", "\"startingKit\": [\"i.nothing\"],\n  \"monsters\": [");
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => ContentFixtures.FixtureBundleJson,
            "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
            "delve/packs/test.json" => pack,
            "delve/tuning/test.json" => ContentFixtures.FixtureTuningJson,
            _ => null,
        });
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("i.nothing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_payload_names_the_missing_path()
    {
        Func<string, string?> read = path => path == "delve/packs/test.json" ? null
            : ContentFixtures.Fixture(
                ContentFixtures.FixtureBundleJson,
                ContentFixtures.FixturePackDescriptorJson,
                ContentFixtures.FixtureTuningJson)(path);
        var load = () => DelverComposition.Load(read);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("packs/test.json", error.Message, StringComparison.Ordinal);
    }
}
