using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Rulesets.Delver.Tests;

public sealed class DelverCatalogAndTuningTests
{
    private static readonly DelverComposition Composition =
        DelverComposition.Load(ContentFixtures.RealContent());

    private static DelverRuleset Ruleset() => new(Composition);

    [Fact]
    public void Every_tuning_field_reaches_the_kit()
    {
        // The shipped profile must map one-to-one onto GameTuning: a silently
        // dropped field would tune nothing.
        Assert.Equal(0.06f, Composition.Tuning.MaxWalkSpeed);
        Assert.Equal(0.02f, Composition.Tuning.WalkAcceleration);
        Assert.Equal(0.8f, Composition.Tuning.WalkFriction);
        Assert.Equal(40, Composition.Tuning.AttackChargeTicks);
        Assert.Equal(30, Composition.Tuning.AttackCooldownTicks);
        Assert.Equal(0.15f, Composition.Tuning.DodgeChance);
        Assert.Equal(0.2f, Composition.Tuning.EnchantChance);
        Assert.Equal(0.25f, Composition.Tuning.FleeHpFraction);
        Assert.Equal(600, Composition.Tuning.EscapeSpawnCadenceStartTicks);
        Assert.Equal(60, Composition.Tuning.EscapeSpawnCadenceEndTicks);
        Assert.Equal(3, Composition.Tuning.EscapeSpawnGroupStart);
        Assert.Equal(15, Composition.Tuning.EscapeSpawnGroupEnd);
        Assert.Equal(5, Composition.Tuning.SeenRadius);
        Assert.Equal(40, Composition.Tuning.StartingGold);
        Assert.Equal(0.5f, Composition.Tuning.EyeHeight);
        Assert.Equal(3f, Composition.Tuning.NoticeDarkTiles);
        Assert.Equal(15f, Composition.Tuning.NoticeLightTiles);
        Assert.Equal(0.6f, Composition.Tuning.VaultChance);
        Assert.Equal(0.012f, Composition.Tuning.TrapChance);
        Assert.Equal(0.1f, Composition.Tuning.WallBoltRoomChance);
        Assert.Equal(0.03f, Composition.Tuning.PotChance);
        Assert.Equal(0.05f, Composition.Tuning.DecorChance);
        Assert.Equal(0.35f, Composition.Tuning.RoomTemplateChance);
        Assert.Equal(0.35f, Composition.Tuning.StepHeight);
        Assert.Equal(0.05f, Composition.Tuning.JumpVelocity);
        Assert.Equal(0.0035f, Composition.Tuning.Gravity);
        Assert.Equal(0.04f, Composition.Tuning.WaterDrag);
    }

    [Fact]
    public void Every_theme_draws_the_shared_room_templates_and_its_own()
    {
        var catalog = new DelverCatalog(Composition.Pack);
        IReadOnlyList<Kit.World.RoomTemplate> sewer = catalog.RoomTemplatesFor("Sewer");
        IReadOnlyList<Kit.World.RoomTemplate> unknown = catalog.RoomTemplatesFor("Nowhere");

        Assert.Contains(sewer, template => template.Id == "pillar-hall");
        Assert.Contains(sewer, template => template.Id == "sewer-pool");
        Assert.Contains(unknown, template => template.Id == "pillar-hall");
        Assert.DoesNotContain(unknown, template => template.Id == "sewer-pool");
    }

    [Fact]
    public void A_room_template_with_an_unknown_marker_fails_the_load()
    {
        string pack = ContentFixtures.FixturePackJson.TrimEnd().TrimEnd('}')
            + ",\"roomTemplates\":{\"Any\":[{\"id\":\"bad\",\"rows\":[\"..?\"]}]}}";
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => ContentFixtures.FixtureBundleJson,
            "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
            "delve/packs/test.json" => pack,
            "delve/tuning/test.json" => ContentFixtures.FixtureTuningJson,
            _ => null,
        });
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("room template 'bad' (Any) uses unknown marker '?'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Loot_rolls_return_an_item_at_every_level()
    {
        var ruleset = Ruleset();
        for (int level = 1; level <= 10; level++)
        {
            var random = new CyclingRandom(level);
            int rolled = 0;
            for (int draw = 0; draw < 8; draw++)
            {
                string? id = ruleset.Catalog.RollLootId(random, level);
                if (id is null)
                {
                    continue;
                }

                rolled++;
                Assert.NotNull(ruleset.Catalog.Item(id));
                Assert.Contains(Composition.Pack.Loot, entry =>
                    entry.ItemId == id && level >= entry.MinItemLevel && level <= entry.MaxItemLevel);
            }

            Assert.True(rolled > 0, $"no loot rolled for level {level}");
        }
    }

    [Fact]
    public void Monster_windows_are_inclusive_at_both_edges()
    {
        DelverCatalog catalog = Ruleset().Catalog;
        Assert.Contains("delve.monster.giant-rat", catalog.MonstersForFloor(1));
        Assert.Contains("delve.monster.giant-rat", catalog.MonstersForFloor(5));
        Assert.DoesNotContain("delve.monster.giant-rat", catalog.MonstersForFloor(6));
        Assert.DoesNotContain("delve.monster.stone-golem", catalog.MonstersForFloor(8));
        Assert.Contains("delve.monster.stone-golem", catalog.MonstersForFloor(9));
        Assert.Contains("delve.monster.stone-golem", catalog.MonstersForFloor(16));
        Assert.DoesNotContain("delve.monster.stone-golem", catalog.MonstersForFloor(17));
    }

    [Fact]
    public void A_section_without_floors_fails_the_load()
    {
        // The fixture section becomes floors:0 with no transition floor.
        string pack = ContentFixtures.FixturePackJson
            .Replace("\"floors\":1", "\"floors\":0")
            .Replace("\"transitionLevel\":{\"theme\":\"Test\"}", "\"transitionLevel\":null");
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => ContentFixtures.FixtureBundleJson,
            "delve/content-packs/delve-core.json" => ContentFixtures.FixturePackDescriptorJson,
            "delve/packs/test.json" => pack,
            "delve/tuning/test.json" => ContentFixtures.FixtureTuningJson,
            _ => null,
        });
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("contributes no floors", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_json_names_its_document()
    {
        var load = () => DelverComposition.Load(path => path switch
        {
            "delve/bundles/delve-run.json" => "{not json",
            _ => null,
        });
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(load);
        Assert.Contains("delve/bundles/delve-run.json", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_or_finished_save_is_refused_at_resume()
    {
        DelverRuleset ruleset = Ruleset();
        var draws = new Kit.Random.SplitMixRandom(1UL);

        RunSession live = ruleset.CreateSession(7UL, MetaProgression.Fresh, draws);
        RunSnapshot stale = live.Capture() with { RunIndex = 99 };
        Assert.Null(ruleset.ResumeSession(stale, MetaProgression.Fresh, draws));

        RunSnapshot finished = live.Capture() with { Phase = RunPhase.Dead };
        Assert.Null(ruleset.ResumeSession(finished, MetaProgression.Fresh, draws));

        Assert.NotNull(ruleset.ResumeSession(live.Capture(), MetaProgression.Fresh, draws));
    }
}
