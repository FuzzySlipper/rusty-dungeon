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

    /// <summary>One brain tick with the player fully lit (noticed within 18 tiles) unless told otherwise.</summary>
    private static void Think(MonsterState monster, PlayerState player, DungeonLevel level, float noticeRadius = 18f, IReadOnlyList<MonsterState>? others = null) =>
        MonsterBrain.Tick(monster, level, player, Tuning, 1f, noticeRadius, others ?? [monster], new ScriptedRandom());

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
            "test.monster.rat", "rat", body.Stats, 20, 2, 40, 1, "monster.rat"));
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
        Think(atThreshold, thresholdPlayer, thresholdLevel);
        Assert.Equal(MonsterBrainState.Fleeing, atThreshold.BrainState);

        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 4);
        Think(monster, player, level);
        Assert.Equal(MonsterBrainState.Fleeing, monster.BrainState);
    }

    [Fact]
    public void A_monster_above_the_threshold_stands_and_fights()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 6);
        Think(monster, player, level);
        Assert.NotEqual(MonsterBrainState.Fleeing, monster.BrainState);
    }

    [Fact]
    public void Detecting_the_player_starts_the_chase()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        Assert.Equal(MonsterBrainState.Idle, monster.BrainState);
        Think(monster, player, level);
        Assert.Equal(MonsterBrainState.Chasing, monster.BrainState);
    }

    [Fact]
    public void A_chasing_monster_stops_beside_the_player_instead_of_inside()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        for (int tick = 0; tick < 600; tick++)
        {
            Think(monster, player, level);
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

        Think(monster, player, level);
        Assert.Equal(MonsterBrainState.Chasing, monster.BrainState);

        level.Set(5, 4, Tile.DoorClosed);
        for (int i = 0; i < 40; i++)
        {
            Think(monster, player, level);
        }

        Assert.Equal(TileKind.DoorOpen, level.At(5, 4).Kind);
    }

    [Fact]
    public void A_chase_closes_the_distance()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        for (int i = 0; i < 30; i++)
        {
            Think(monster, player, level);
        }

        float distance = MathF.Abs(monster.Body.X - player.Body.X);
        Assert.True(distance < 1.5f, $"monster did not close in (distance {distance})");
    }

    [Fact]
    public void In_the_dark_a_monster_notices_only_a_close_player_unless_already_hurt()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        player.Body.X = 7.5f; // three tiles off, in sight

        Think(monster, player, level, noticeRadius: 3f);
        Assert.Equal(MonsterBrainState.Idle, monster.BrainState);

        Think(monster, player, level, noticeRadius: 3f + (15f * 0.1f));
        Assert.Equal(MonsterBrainState.Chasing, monster.BrainState);

        (MonsterState hurt, PlayerState far, DungeonLevel hall) = Scene(monsterHp: 20);
        far.Body.X = 7.5f;
        hurt.WasHit = true;
        Think(hurt, far, hall, noticeRadius: 3f);
        Assert.Equal(MonsterBrainState.Chasing, hurt.BrainState);
    }

    [Fact]
    public void An_idle_monster_wanders_from_tile_to_neighbouring_tile_and_an_ambusher_waits()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        player.Body.X = 7.5f;
        float startX = monster.Body.X;
        float startY = monster.Body.Y;
        for (int i = 0; i < 60; i++)
        {
            Think(monster, player, level, noticeRadius: 0f);
        }

        Assert.Equal(MonsterBrainState.Idle, monster.BrainState);
        float moved = MathF.Abs(monster.Body.X - startX) + MathF.Abs(monster.Body.Y - startY);
        Assert.True(moved > 0.3f, $"it stood still ({moved})");
        Assert.True(moved <= 2.01f, $"it ran off ({moved})"); // one wander step a tile, at 60% speed

        (MonsterState ambusher, PlayerState player2, DungeonLevel level2) = Scene(monsterHp: 20);
        var waiting = new MonsterState(ambusher.Body, ambusher.Archetype with { Ambushes = true });
        for (int i = 0; i < 60; i++)
        {
            Think(waiting, player2, level2, noticeRadius: 0f);
        }

        Assert.Equal(4.5f, waiting.Body.X);
        Assert.Equal(4.5f, waiting.Body.Y);
    }

    [Fact]
    public void A_monster_does_not_walk_into_another_and_shoves_it_aside()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        var other = new MonsterState(
            new ActorState(3, ActorKind.Monster, 5.3f, 4.5f, 20, monster.Body.Stats),
            monster.Archetype);
        MonsterState[] both = [monster, other];

        for (int i = 0; i < 20; i++)
        {
            Think(monster, player, level, others: both);
        }

        float gap = MathF.Abs(other.Body.X - monster.Body.X);
        Assert.True(gap >= Tuning.ActorSeparationTiles - 0.001f || other.VelocityX > 0f, $"gap {gap}");
        Assert.True(other.VelocityX > 0f, "the monster in the way was not pushed");
    }

    [Fact]
    public void A_chase_across_an_open_room_runs_the_diagonal()
    {
        (MonsterState monster, PlayerState player, DungeonLevel level) = Scene(monsterHp: 20);
        monster.Body.X = 1.5f;
        monster.Body.Y = 1.5f;
        player.Body.X = 6.5f;
        player.Body.Y = 6.5f;

        for (int i = 0; i < 20; i++)
        {
            Think(monster, player, level);
        }

        // A tile-by-tile chase moves along one axis at a time; a cut-corner
        // run gains on both together.
        Assert.True(monster.Body.X > 1.7f && monster.Body.Y > 1.7f, $"at {monster.Body.X}, {monster.Body.Y}");
        Assert.InRange(MathF.Abs((monster.Body.X - 1.5f) - (monster.Body.Y - 1.5f)), 0f, 0.15f);
    }
}
