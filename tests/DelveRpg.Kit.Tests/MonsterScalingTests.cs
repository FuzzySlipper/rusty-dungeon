using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using Xunit;

namespace DelveRpg.Kit.Tests;

/// <summary>Pins the donor's monster levelling (Monster.Init, Actor.initLevel).</summary>
public sealed class MonsterScalingTests
{
    [Theory]
    [InlineData(1, 1, 0, 1)]   // floor(1.5) = 1
    [InlineData(2, 1, 0, 3)]   // floor(3.0) = 3
    [InlineData(3, 1, 0, 4)]   // floor(4.5) = 4
    [InlineData(1, 1, 5, 5)]   // a base level is a floor
    [InlineData(2, 13, 0, 6)]  // outlevelled by 10: 3 + (int)(10 * 0.3)
    public void Spawn_level_follows_the_floor_and_the_player(int dungeonLevel, int playerLevel, int baseLevel, int expected) =>
        Assert.Equal(expected, MonsterScaling.SpawnLevel(dungeonLevel, playerLevel, baseLevel));

    [Fact]
    public void Each_level_adds_two_hit_points_and_two_to_the_attack_roll()
    {
        Assert.Equal(4, MonsterScaling.MaxHitPoints(4, 1));
        Assert.Equal(10, MonsterScaling.MaxHitPoints(4, 4));

        StatBlock stats = MonsterScaling.Stats(new StatBlock(4, 0, 1, 3, 0, 1), 4);
        Assert.Equal(10, stats.Attack);
        Assert.Equal(0, stats.Defense);
    }

    [Fact]
    public void Spawned_monsters_carry_their_level_and_keep_it_across_a_save()
    {
        var plan = new RunPlan([new FloorSpec(0, 2, "One", "Test", false)]);
        var session = new RunSession(
            new ScriptedCatalog(), new GameTuning(), plan, 99UL, MetaProgression.Fresh, new ScriptedRandom());
        Assert.NotEmpty(session.Monsters);

        int expected = MonsterScaling.SpawnLevel(2, session.Player.Level, 1);
        foreach (MonsterState monster in session.Monsters)
        {
            Assert.Equal(expected, monster.Level);
            Assert.Equal(MonsterScaling.MaxHitPoints(monster.Archetype.BaseHp, expected), monster.Body.MaxHp);
        }

        RunSnapshot snapshot = session.Capture();
        var restored = new RunSession(
            new ScriptedCatalog(), new GameTuning(), plan, snapshot, MetaProgression.Fresh, new ScriptedRandom());
        Assert.All(restored.Monsters, monster => Assert.Equal(expected, monster.Level));
    }
}
