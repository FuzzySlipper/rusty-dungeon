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
        // 25% of 20 max hp is 5, and the rule is "at or below": both 5 and 4 run.
        (MonsterState atThreshold, PlayerState thresholdPlayer, DungeonLevel thresholdLevel) = Scene(monsterHp: 5);
        MonsterBrain.Tick(atThreshold, thresholdLevel, thresholdPlayer, Tuning, 1f);
        Assert.Equal(MonsterBrainState.Fleeing, atThreshold.BrainState);

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
    public void A_chasing_monster_stops_beside_the_player_instead_of_inside()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        for (int tick = 0; tick < 600; tick++)
        {
            MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        }

        float dx = monster.Body.X - player.Body.X;
        float dy = monster.Body.Y - player.Body.Y;
        float distance = MathF.Sqrt((dx * dx) + (dy * dy));
        Assert.Equal(MonsterBrainState.Chasing, monster.BrainState);
        Assert.InRange(distance, Tuning.ActorSeparationTiles - 0.05f, Tuning.ActorSeparationTiles + 0.001f);
    }

    [Fact]
    public void A_chasing_monster_opens_the_door_in_its_path()
    {
        // Detection needs line of sight (a closed door blocks it); once the
        // chase is on, a closed door in the path gets opened, not obeyed.
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        level.Set(5, 4, Tile.DoorOpen);
        monster.Body.X = 4.5f;
        player.Body.X = 7.5f;

        MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        Assert.Equal(MonsterBrainState.Chasing, monster.BrainState);

        level.Set(5, 4, Tile.DoorClosed);
        for (int i = 0; i < 40; i++)
        {
            MonsterBrain.Tick(monster, level, player, Tuning, 1f);
        }

        Assert.Equal(TileKind.DoorOpen, level.At(5, 4).Kind);
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
