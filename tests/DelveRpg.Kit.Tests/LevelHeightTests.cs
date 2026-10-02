using DelveRpg.Kit.Ai;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

/// <summary>Floor and ceiling heights, room pieces, and walking, jumping and falling over them.</summary>
public sealed class LevelHeightTests
{
    private static GenerationConfig Config => new(
        Width: 32,
        Height: 32,
        RoomCountMin: 5,
        RoomCountMax: 8,
        RoomSizeMin: 4,
        RoomSizeMax: 7,
        MonsterCount: 4,
        ItemCount: 3);

    private static FloorSpec Floor => new(0, 2, "Test Section", "TestTheme", false);

    private static RunInput Forward => RunInput.Idle with { MoveY = 1f };

    [Fact]
    public void Generated_floors_keep_every_step_walkable_and_a_tile_of_headroom()
    {
        for (ulong seed = 0; seed < 40; seed++)
        {
            GeneratedLevel generated = DungeonGenerator.Generate(
                new SplitMixRandom(SplitMixRandom.FloorSeed(seed, 0)), Config, new GameTuning(), Floor, ["m1"], ["i1"]);
            DungeonLevel level = generated.Level;

            Assert.True(LevelShaping.SteepestStep(level) <= LevelShaping.MaxWalkStep, $"seed {seed}");
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    if (level.IsNavigable(x, y))
                    {
                        Assert.True(level.CeilingHeight(x, y) - level.FloorHeight(x, y) >= 1f - 0.001f, $"seed {seed} at {x},{y}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Some_generated_floors_are_not_flat()
    {
        bool varied = false;
        for (ulong seed = 0; seed < 20 && !varied; seed++)
        {
            DungeonLevel level = DungeonGenerator.Generate(
                new SplitMixRandom(SplitMixRandom.FloorSeed(seed, 0)), Config, new GameTuning(), Floor, ["m1"], ["i1"]).Level;
            varied = LevelShaping.SteepestStep(level) > 0f;
        }

        Assert.True(varied);
    }

    [Fact]
    public void A_template_stamps_pillars_daises_water_and_its_markers()
    {
        DungeonLevel level = OpenRoom(11, 11);
        var template = new RoomTemplate("t", [
            ".......",
            ".T#.M..",
            "..^*~..",
            "..L.o..",
            ".......",
        ]);

        StampedMarkers markers = LevelShaping.StampTemplates(
            ScriptedRandom.Always(0), level, [new RoomBounds(1, 1, 9, 9)], default, [template], 1f);

        // 9x9 room at (1,1); the 7x5 piece is centred at (2,3).
        Assert.Equal(TileKind.Wall, level.At(4, 4).Kind);
        Assert.Equal(LevelShaping.DaisRise, level.FloorHeight(4, 5));
        Assert.True(level.CeilingHeight(4, 5) >= LevelShaping.DaisRise + 1f);
        Assert.Equal(TileKind.Water, level.At(6, 5).Kind);
        Assert.Equal([(6, 4)], markers.Monsters);
        Assert.Equal([(5, 5), (4, 6)], markers.Loot);
        Assert.Equal([(6, 6)], markers.Pots);
        WallTorch torch = Assert.Single(markers.Torches);
        Assert.Equal((3, 4), (torch.TileX, torch.TileY));
        Assert.Equal(TileKind.Wall, level.At(torch.TileX + torch.WallDx, torch.TileY + torch.WallDy).Kind);
    }

    [Fact]
    public void A_template_that_would_cut_the_floor_apart_is_taken_back()
    {
        DungeonLevel level = OpenRoom(5, 7);
        var wall = new RoomTemplate("cut", ["###"]);

        StampedMarkers markers = LevelShaping.StampTemplates(
            ScriptedRandom.Always(0), level, [new RoomBounds(1, 1, 3, 5)], default, [wall], 1f);

        Assert.Empty(markers.Monsters);
        for (int x = 1; x <= 3; x++)
        {
            Assert.Equal(TileKind.Floor, level.At(x, 3).Kind);
        }
    }

    [Fact]
    public void Room_torches_hang_on_room_walls()
    {
        DungeonLevel level = OpenRoom(14, 6);
        List<WallTorch> torches = LevelShaping.RoomTorches(ScriptedRandom.Always(0), level, [new RoomBounds(1, 1, 12, 4)]);

        Assert.NotEmpty(torches);
        Assert.All(torches, torch =>
        {
            Assert.Equal(TileKind.Floor, level.At(torch.TileX, torch.TileY).Kind);
            Assert.Equal(TileKind.Wall, level.At(torch.TileX + torch.WallDx, torch.TileY + torch.WallDy).Kind);
        });
    }

    [Fact]
    public void The_player_walks_up_a_step_onto_higher_floor()
    {
        RunSession session = RangedCombatTests.Hall(ScriptedRandom.Always(999_999), [], null);
        for (int x = 4; x < 13; x++)
        {
            session.Level.SetHeights(x, 1, 0.25f, 1.25f);
        }

        Walk(session, Forward, 90);

        Assert.True(session.Player.Body.X > 5f);
        Assert.Equal(0.25f, session.Player.Body.Z, 3);
    }

    [Fact]
    public void A_dais_stops_a_walk_but_not_a_jump()
    {
        RunSession walker = RangedCombatTests.Hall(ScriptedRandom.Always(999_999), [], null);
        RaiseFrom(walker.Level, 4, LevelShaping.DaisRise);
        Walk(walker, Forward, 90);
        Assert.True(walker.Player.Body.X < 4f);

        RunSession jumper = RangedCombatTests.Hall(ScriptedRandom.Always(999_999), [], null);
        RaiseFrom(jumper.Level, 4, LevelShaping.DaisRise);
        Walk(jumper, Forward with { JumpPressed = true }, 90);
        Walk(jumper, Forward, 60);
        Assert.True(jumper.Player.Body.X > 4.5f);
        Assert.Equal(LevelShaping.DaisRise, jumper.Player.Body.Z, 3);
    }

    [Fact]
    public void Walking_off_a_ledge_falls_over_several_ticks()
    {
        RunSession session = RangedCombatTests.Hall(ScriptedRandom.Always(999_999), [], null);
        for (int x = 1; x < 4; x++)
        {
            session.Level.SetHeights(x, 1, 0.3f, 1.3f);
        }

        session.Player.Body.Z = 0.3f;
        int airborne = 0;
        for (int tick = 0; tick < 120; tick++)
        {
            session.Tick(Forward);
            airborne += session.Player.Body.Grounded ? 0 : 1;
        }

        Assert.True(airborne > 3);
        Assert.Equal(0f, session.Player.Body.Z, 3);
        Assert.True(session.Player.Body.Grounded);
    }

    [Fact]
    public void A_wader_climbs_back_out_of_water()
    {
        RunSession session = RangedCombatTests.Hall(ScriptedRandom.Always(999_999), [], null);
        session.Level.Set(1, 1, Tile.Water);
        session.Level.Set(2, 1, Tile.Water);
        session.Player.Body.Z = session.Level.StandHeight(1, 1);

        Walk(session, Forward, 120);

        Assert.True(session.Player.Body.X > 4f);
        Assert.Equal(0f, session.Player.Body.Z, 3);
    }

    [Fact]
    public void A_monster_path_climbs_a_step_at_a_time()
    {
        DungeonLevel level = OpenRoom(9, 3);
        RaiseFrom(level, 5, 0.5f);

        Assert.Empty(GridPathfinder.FindPath(level, 1, 1, 7, 1, 32, 0.35f));
        Assert.NotEmpty(GridPathfinder.FindPath(level, 1, 1, 7, 1, 32));

        level.SetHeights(4, 1, 0.25f, 1.25f);
        Assert.NotEmpty(GridPathfinder.FindPath(level, 1, 1, 7, 1, 32, 0.35f));
    }

    [Fact]
    public void Heights_survive_a_save()
    {
        RunSession session = RangedCombatTests.Hall(ScriptedRandom.Always(999_999), [], null);
        session.Level.SetHeights(5, 1, 0.4375f, 2.2f);
        session.Level.SetHeights(6, 1, -0.25f, 1f);

        RunSession restored = new(
            new ScriptedCatalog(), new GameTuning(), new RunPlan([new FloorSpec(0, 1, "One", "Test", false)]),
            session.Capture(), MetaProgression.Fresh, ScriptedRandom.Always(999_999));

        Assert.Equal(0.4375f, restored.Level.FloorHeight(5, 1));
        Assert.Equal(2.2f, restored.Level.CeilingHeight(5, 1), 4);
        Assert.Equal(-0.25f, restored.Level.FloorHeight(6, 1));
    }

    private static void Walk(RunSession session, RunInput input, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            session.Tick(input);
        }
    }

    private static void RaiseFrom(DungeonLevel level, int fromX, float height)
    {
        for (int x = fromX; x < level.Width - 1; x++)
        {
            level.SetHeights(x, 1, height, height + 1f);
        }
    }

    /// <summary>A walled level whose inside is all floor.</summary>
    private static DungeonLevel OpenRoom(int width, int height)
    {
        var level = new DungeonLevel(width, height, 1, "Test");
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                level.Set(x, y, Tile.Floor);
            }
        }

        return level;
    }
}
