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
            "m", "m", body.Stats, 8, 2, 40, 1, "monster.m"));

        Assert.Equal(0u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));

        monster.BrainState = MonsterBrainState.Chasing;
        Assert.InRange(DelveSceneRenderer.MonsterFrame(definition, monster, 15), 0u, 3u);

        monster.AttackElapsedTicks = 0; // the wind-up just began
        Assert.Equal(4u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));
        monster.AttackElapsedTicks = 29; // the last cell
        Assert.Equal(10u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));
        monster.AttackElapsedTicks = 35;  // played out: back to walking
        Assert.InRange(DelveSceneRenderer.MonsterFrame(definition, monster, 15), 0u, 3u);
    }

    [Fact]
    public void A_flinch_or_death_stagger_shows_the_hurt_cells_over_everything()
    {
        var definition = new DelveSpriteDefinition
        {
            Frame = 0,
            Walk = new DelveSpriteAnimation { Start = 0, End = 3, Speed = 30 },
            Attack = new DelveSpriteAnimation { Start = 4, End = 10, Speed = 30 },
            Hurt = new DelveSpriteAnimation { Start = 11, End = 14, Speed = 22 },
        };
        var body = new ActorState(7, ActorKind.Monster, 1.5f, 1.5f, 8, new StatBlock(2, 1, 5, 5, 0, 2));
        var monster = new MonsterState(body, new MonsterArchetype(
            "m", "m", body.Stats, 8, 2, 40, 1, "monster.m") { HurtTicks = 22 });
        monster.BrainState = MonsterBrainState.Chasing;
        monster.AttackElapsedTicks = 0;

        monster.HurtTicksRemaining = 22; // the flinch just began
        Assert.Equal(11u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));

        // Dying past the end of the hurt cells holds the last one.
        monster.HurtTicksRemaining = 0;
        monster.DyingTicksRemaining = 3;
        Assert.Equal(14u, DelveSceneRenderer.MonsterFrame(definition, monster, 15));
    }

    [Fact]
    public void A_play_once_animation_holds_its_last_cell()
    {
        var die = new DelveSpriteAnimation { Start = 15, End = 17, Speed = 30 };
        Assert.Equal(15, die.FrameOnce(0));
        Assert.Equal(16, die.FrameOnce(10));
        Assert.Equal(17, die.FrameOnce(29));
        Assert.Equal(17, die.FrameOnce(5000));
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

public sealed class ArtManifestDefaultsTests
{
    [Fact]
    public void Omitted_sprite_fields_keep_their_defaults()
    {
        DelveSpriteManifest manifest = System.Text.Json.JsonSerializer.Deserialize(
            """{"sprites":{"s":{"atlas":"a.png","frame":3,"walk":{"start":0,"end":3}}}}""",
            DelveSpriteJsonContext.Default.DelveSpriteManifest)!;

        Assert.Equal(string.Empty, manifest.ArtContentPrefix);
        Assert.Empty(manifest.Atlases);
        DelveSpriteDefinition sprite = manifest.Sprites["s"];
        Assert.Equal(1f, sprite.Size);
        Assert.Equal(30, sprite.Walk!.Speed);
    }

    [Fact]
    public void Omitted_tile_manifest_fields_keep_their_defaults()
    {
        DelveArtManifest manifest = System.Text.Json.JsonSerializer.Deserialize(
            """{"schema":"delve.art.tiles.v1"}""", DelveArtJsonContext.Default.DelveArtManifest)!;

        Assert.Equal(string.Empty, manifest.ArtContentPrefix);
        Assert.Empty(manifest.Atlases);
        Assert.Empty(manifest.Tiles);
    }
}

public sealed class SpriteLightingTests
{
    [Fact]
    public void A_manifest_without_lighting_draws_sprites_unlit()
    {
        DelveSpriteManifest manifest = System.Text.Json.JsonSerializer.Deserialize(
            """{"sprites":{}}""", DelveSpriteJsonContext.Default.DelveSpriteManifest)!;
        Assert.Equal(SpriteLightingMode.Unlit, manifest.Lighting.EngineMode);
    }

    [Theory]
    [InlineData("derived-gradient", SpriteLightingMode.DerivedGradient)]
    [InlineData("authored-normal", SpriteLightingMode.AuthoredNormal)]
    [InlineData("synthetic", SpriteLightingMode.Synthetic)]
    public void Lighting_modes_name_the_engine_modes(string mode, SpriteLightingMode expected) =>
        Assert.Equal(expected, new DelveSpriteLighting { Mode = mode }.EngineMode);

    [Fact]
    public void An_unknown_lighting_mode_is_refused_by_name()
    {
        var lighting = new DelveSpriteLighting { Mode = "glossy" };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => lighting.EngineMode);
        Assert.Contains("glossy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Authored_normals_without_a_staged_normal_sheet_draw_unlit()
    {
        var lighting = new DelveSpriteLighting { Mode = "authored-normal", Strength = 1.5f };
        SpriteMaterialDescriptor material = DelveSpriteAssets.CutoutMaterial(lighting, null);

        Assert.Equal(SpriteLightingMode.Unlit, material.Lighting);
        Assert.Equal(SpriteAlphaMode.Mask, material.AlphaMode);
    }
}
