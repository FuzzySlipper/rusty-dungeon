using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class ProgressionTests
{
    [Fact]
    public void Experience_curve_matches_the_donor()
    {
        // next = (level*4)*(level*2)
        Assert.Equal(8, LevelProgression.ExperienceToNext(1));
        Assert.Equal(72, LevelProgression.ExperienceToNext(3));
        Assert.Equal(200, LevelProgression.ExperienceToNext(5));
    }

    [Fact]
    public void Maximum_hit_points_follow_the_donor_formula()
    {
        // maxHp = (int)(END*(END/3f)) + 4, then += (level-1)*0.5f (truncated).
        var stats = new StatBlock(4, 2, 4, 4, 2, 6);
        Assert.Equal(16, LevelProgression.MaxHitPoints(stats, 1)); // (int)(6*2) + 4
        Assert.Equal(16, LevelProgression.MaxHitPoints(stats, 2)); // + (int)0.5f
        Assert.Equal(17, LevelProgression.MaxHitPoints(stats, 3)); // + (int)1.0f

        var odd = new StatBlock(4, 2, 4, 4, 2, 5);
        Assert.Equal(12, LevelProgression.MaxHitPoints(odd, 1)); // (int)(5*1.666..) + 4
    }

    [Fact]
    public void A_level_up_heals_to_full_and_raises_the_maximum()
    {
        var player = new PlayerState(
            new ActorState(1, ActorKind.Player, 0f, 0f, LevelProgression.MaxHitPoints(new StatBlock(4, 2, 4, 4, 2, 6), 1),
                new StatBlock(4, 2, 4, 4, 2, 6)),
            new InventoryStore(6, 6));
        player.Body.Hp = 3;
        player.Level = 1;

        LevelProgression.ApplyLevelUp(player);

        Assert.Equal(2, player.Level);
        Assert.Equal(player.Body.MaxHp, player.Body.Hp);
        Assert.Equal(LevelProgression.MaxHitPoints(player.Body.Stats, 2), player.Body.MaxHp);
    }

    [Fact]
    public void Stat_offers_are_three_distinct_stats_and_apply_once()
    {
        var random = new ScriptedRandom(0, 1, 2, 0, 1, 2);
        IReadOnlyList<string> offers = LevelProgression.RollOffers(random);
        Assert.Equal(3, offers.Count);
        Assert.Equal(3, offers.Distinct().Count());

        var player = new PlayerState(
            new ActorState(1, ActorKind.Player, 0f, 0f, 10, new StatBlock(4, 2, 4, 4, 2, 6)),
            new InventoryStore(6, 6));
        LevelProgression.ApplyStatChoice(player, "endurance");
        Assert.Equal(7, player.Body.Stats.Endurance);
    }

    [Fact]
    public void Meta_progression_grows_the_flat_slot_grid()
    {
        MetaProgression fresh = MetaProgression.Fresh;
        Assert.Equal(6, fresh.HotbarSize);
        Assert.Equal(18, fresh.BackpackSize);
        MetaProgression upgraded = fresh with { HotbarUpgrades = 2, InventoryUpgrades = 1 };
        Assert.Equal(8, upgraded.HotbarSize);
        Assert.Equal(24, upgraded.BackpackSize);
    }
}
