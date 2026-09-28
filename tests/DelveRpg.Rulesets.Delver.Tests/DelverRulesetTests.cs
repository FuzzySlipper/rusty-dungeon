using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Rulesets.Delver.Tests;

/// <summary>Cycles through fixed draws so a weighted table is exercised from every offset.</summary>
file sealed class CyclingRandom : Kit.Random.IRandomSource
{
    private int _value;

    public CyclingRandom(int start) => _value = start;

    public int Next(int minimum, int maximum)
    {
        int draw = minimum + (_value % Math.Max(1, maximum - minimum));
        _value += 97;
        return draw;
    }
}

public sealed class DelverRulesetTests
{
    private static DelverRuleset Ruleset() =>
        new(DelverComposition.Load(ContentFixtures.RealContent()));

    [Fact]
    public void The_run_plan_is_the_sections_in_sort_order()
    {
        RunPlan plan = Ruleset().BuildRunPlan(99UL);

        // Sections: 2 + (2+1) + 2 + (2+1) + (1+1) floors.
        Assert.Equal(12, plan.FloorCount);
        Assert.Equal("The Sewers", plan.FloorAt(0).SectionName);
        Assert.Equal("The Frozen Deep", plan.FloorAt(11).SectionName);
    }

    [Fact]
    public void A_floor_dungeon_level_is_the_section_difficulty_plus_the_floor()
    {
        RunPlan plan = Ruleset().BuildRunPlan(5UL);

        // First section: difficultyLevel 1 with two floors, then the next section starts at 3.
        Assert.Equal(1, plan.FloorAt(0).DungeonLevel);
        Assert.Equal(2, plan.FloorAt(1).DungeonLevel);
        Assert.Equal(3, plan.FloorAt(2).DungeonLevel);
    }

    [Fact]
    public void Transition_floors_trail_their_section()
    {
        RunPlan plan = Ruleset().BuildRunPlan(5UL);

        Assert.False(plan.FloorAt(1).IsTransition);
        Assert.False(plan.FloorAt(3).IsTransition);
        Assert.True(plan.FloorAt(4).IsTransition); // Forgotten Temple's transition floor
        Assert.Equal(5, plan.FloorAt(4).DungeonLevel); // difficultyLevel 3 + floors 2
    }

    [Fact]
    public void The_same_seed_selects_the_same_templates()
    {
        RunPlan left = Ruleset().BuildRunPlan(1234UL);
        RunPlan right = Ruleset().BuildRunPlan(1234UL);
        for (int i = 0; i < left.FloorCount; i++)
        {
            Assert.Equal(left.FloorAt(i).Theme, right.FloorAt(i).Theme);
        }
    }

    [Fact]
    public void Catalog_windows_respect_dungeon_levels()
    {
        DelverRuleset ruleset = Ruleset();
        Assert.Contains("delve.monster.giant-rat", ruleset.Catalog.MonstersForFloor(1));
        Assert.DoesNotContain("delve.monster.stone-golem", ruleset.Catalog.MonstersForFloor(1));
        Assert.Contains("delve.monster.stone-golem", ruleset.Catalog.MonstersForFloor(10));
    }

    [Fact]
    public void Floor_item_tables_never_leak_the_objective_or_keys()
    {
        DelverRuleset ruleset = Ruleset();
        IReadOnlyList<string> items = ruleset.Catalog.ItemsForFloor(3);
        Assert.Contains("delve.item.potion-of-healing", items);
        Assert.DoesNotContain("delve.item.orb", items);
        Assert.DoesNotContain("delve.item.iron-key", items);
    }

    [Fact]
    public void Loot_rolls_stay_inside_the_level_window()
    {
        DelverComposition composition = DelverComposition.Load(ContentFixtures.RealContent());
        var ruleset = new DelverRuleset(composition);
        for (int level = 1; level <= 10; level++)
        {
            var random = new CyclingRandom(level);
            for (int draw = 0; draw < 8; draw++)
            {
                string? rolled = ruleset.Catalog.RollLootId(random, level);
                if (rolled is null)
                {
                    continue;
                }

                Assert.NotNull(ruleset.Catalog.Item(rolled));
                Assert.Contains(composition.Pack.Loot, entry =>
                    entry.ItemId == rolled && level >= entry.MinItemLevel && level <= entry.MaxItemLevel);
            }
        }
    }

    [Fact]
    public void A_fresh_session_starts_on_the_first_floor_with_starting_gold()
    {
        RunSession session = Ruleset().CreateSession(77UL, Kit.Progression.MetaProgression.Fresh);
        Assert.Equal(0, session.RunIndex);
        Assert.Equal(RunPhase.Playing, session.Phase);
        Assert.Equal(40, session.Player.Gold);
    }
}
