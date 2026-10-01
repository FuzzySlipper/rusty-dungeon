using System.Numerics;
using DelveRpg.Host.Presentation;
using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Ai;
using DelveRpg.Kit.Rules;
using Rusty.Engine;
using Xunit;

namespace DelveRpg.Host.Tests;

public sealed class DelveSpriteAssetsTests
{
    [Fact]
    public void A_donor_animation_spreads_its_cells_over_the_sequence_and_loops()
    {
        // Donor SpriteAnimation: speed is the whole sequence in ticks.
        var walk = new DelveSpriteAnimation { Start = 32, End = 35, Speed = 30 };
        Assert.Equal(32, walk.FrameAt(0));
        Assert.Equal(33, walk.FrameAt(8));
        Assert.Equal(35, walk.FrameAt(29));
        Assert.Equal(32, walk.FrameAt(30));
    }

    [Fact]
    public void Cell_frames_index_column_then_row_from_the_top_left()
    {
        SpriteAtlasFrame[] frames = DelveSpriteAssets.CellFrames(new DelveArtAtlas { Columns = 8, Rows = 4 });

        Assert.Equal(32, frames.Length);
        SpriteAtlasFrame cell = frames[11]; // column 3, row 1
        Assert.Equal(11u, cell.FrameId);
        Assert.InRange(cell.UvMin.X, 3f / 8f, (3f / 8f) + 0.001f);
        Assert.InRange(cell.UvMin.Y, 1f / 4f, (1f / 4f) + 0.001f);
        Assert.InRange(cell.UvMax.X, (4f / 8f) - 0.001f, 4f / 8f);
        Assert.InRange(cell.UvMax.Y, (2f / 4f) - 0.001f, 2f / 4f);
    }

    [Fact]
    public void A_monster_walks_while_hunting_rests_while_idle_and_shows_its_blow()
    {
        var definition = new DelveSpriteDefinition
        {
            Frame = 0,
            Walk = new DelveSpriteAnimation { Start = 0, End = 3, Speed = 30 },
            Attack = new DelveSpriteAnimation { Start = 4, End = 10, Speed = 30 },
        };
        var body = new ActorState(7, ActorKind.Monster, 1.5f, 1.5f, 8, new StatBlock(2, 1, 5, 5, 0, 2));
        var monster = new MonsterState(body, new MonsterArchetype(
            "m", "m", body.Stats, 8, 2, 40, 1, false, 7, "monster.m"));

        Assert.Equal(0u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));

        monster.BrainState = MonsterBrainState.Chasing;
        Assert.InRange(DelveSceneRenderer.MonsterFrame(definition, monster, 15), 0u, 3u);

        body.AttackCooldownRemaining = 40; // the blow just landed
        Assert.Equal(4u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));
        body.AttackCooldownRemaining = 5;  // long after: back to walking
        Assert.InRange(DelveSceneRenderer.MonsterFrame(definition, monster, 15), 0u, 3u);
    }

    [Fact]
    public void The_held_weapon_rests_low_right_rises_on_charge_and_sweeps_on_the_blow()
    {
        DelveSceneRenderer.HeldPose rest = DelveSceneRenderer.HeldWeaponPose(0f, 0f);
        DelveSceneRenderer.HeldPose charged = DelveSceneRenderer.HeldWeaponPose(1f, 0f);
        DelveSceneRenderer.HeldPose blow = DelveSceneRenderer.HeldWeaponPose(0f, 0.5f);

        Assert.True(rest.Position.X > 0f && rest.Position.Y < 0f && rest.Position.Z < 0f);
        Assert.True(charged.Position.Y > rest.Position.Y);
        Assert.True(charged.RollDegrees > rest.RollDegrees);
        Assert.True(blow.Position.X < rest.Position.X);
        Assert.True(blow.RollDegrees < rest.RollDegrees);
        DelveSceneRenderer.HeldPose settled = DelveSceneRenderer.HeldWeaponPose(0f, 1f);
        Assert.True(Vector3.Distance(rest.Position, settled.Position) < 0.001f);
    }
}
