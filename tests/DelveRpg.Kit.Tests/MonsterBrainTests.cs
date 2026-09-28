using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Ai;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class MonsterBrainTests
{
    private static readonly GameTuning Tuning = new();

    private static (MonsterState Monster, PlayerState Player, DungeonLevel Level) Scene(int monsterHp)
    {
        var level = new DungeonLevel(9, 9, 1, "Test");
        for (int y = 1; y < 8; y++)
        {
            for (int x = 1; x < 8; x++)
            {
                level.Set(x, y, Tile.Floor);
            }
        }

        var body = new ActorState(2, ActorKind.Monster, 4.5f, 4.5f, 20, new StatBlock(2, 1, 5, 5, 0, 4))
        {
            Hp = monsterHp,
        };
        var monster = new MonsterState(body, new MonsterArchetype(
            "test.monster.rat", "rat", body.Stats, 20, 2, 40, 1, false, 8, "monster.rat"));
        var player = new PlayerState(
            new ActorState(1, ActorKind.Player, 6.5f, 4.5f, 16, new StatBlock(4, 2, 4, 4, 2, 6)),
            new InventoryStore(6, 6));
        return (monster, player, level);
    }

    [Fact]
    public void A_wounded_monster_flees_below_the_threshold()
    {
        // 25% of 20 max hp is 5: at 4 hp the monster runs.
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 4);
        MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        Assert.Equal(MonsterBrainState.Fleeing, monster.BrainState);
    }

    [Fact]
    public void A_monster_above_the_threshold_stands_and_fights()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 6);
        MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        Assert.NotEqual(MonsterBrainState.Fleeing, monster.BrainState);
    }

    [Fact]
    public void Detecting_the_player_starts_the_chase()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        Assert.Equal(MonsterBrainState.Idle, monster.BrainState);
        MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        Assert.Equal(MonsterBrainState.Chasing, monster.BrainState);
    }

    [Fact]
    public void A_chase_closes_the_distance()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        for (int i = 0; i < 30; i++)
        {
            MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        }

        float distance = MathF.Abs(monster.Body.X - player.Body.X);
        Assert.True(distance < 1.5f, $"monster did not close in (distance {distance})");
    }
}
