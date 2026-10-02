using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

/// <summary>Floors left behind come back as they were left.</summary>
public sealed class FloorMemoryTests
{
    private static RunPlan Plan => new(
    [
        new FloorSpec(0, 1, "One", "Test", false),
        new FloorSpec(1, 2, "Two", "Test", false),
    ]);

    private static RunSession NewRun() =>
        new(new ScriptedCatalog(), new GameTuning(), Plan, 2024UL, MetaProgression.Fresh, ScriptedRandom.Always(999_999));

    /// <summary>Stand beside the first tile of a kind, facing it, and use it.</summary>
    private static void UseTile(RunSession session, TileKind kind)
    {
        DungeonLevel level = session.Level;
        for (int y = 1; y < level.Height - 1; y++)
        {
            for (int x = 1; x < level.Width - 1; x++)
            {
                if (level.At(x, y).Kind != kind)
                {
                    continue;
                }

                foreach ((int dx, int dy, float facing) in new[] { (-1, 0, MathF.PI / 2f), (1, 0, -MathF.PI / 2f), (0, 1, 0f), (0, -1, MathF.PI) })
                {
                    if (level.IsWalkable(x + dx, y + dy) && level.At(x + dx, y + dy).Kind == TileKind.Floor)
                    {
                        session.Player.Body.X = x + dx + 0.5f;
                        session.Player.Body.Y = y + dy + 0.5f;
                        session.Player.Body.Facing = facing;
                        session.Tick(RunInput.Idle with { UsePressed = true });
                        return;
                    }
                }
            }
        }

        throw new InvalidOperationException($"No usable {kind} on this floor.");
    }

    [Fact]
    public void Climbing_back_finds_the_floor_as_it_was_left_and_arrives_at_its_way_down()
    {
        RunSession session = NewRun();
        MonsterState wounded = session.Monsters[0];
        wounded.Body.Hp = 1;
        long woundedAt = (long)(wounded.Body.X * 1000);
        int items = session.GroundItems.Count;
        session.Level.Set(1, 1, Tile.Floor); // a mark only the left floor carries

        UseTile(session, TileKind.StairsDown);
        Assert.Equal(1, session.RunIndex);

        UseTile(session, TileKind.StairsUp);
        Assert.Equal(0, session.RunIndex);
        Assert.Contains(session.Messages, message => message.Text.Contains("as you left it"));
        Assert.Equal(TileKind.Floor, session.Level.At(1, 1).Kind);
        Assert.Equal(items, session.GroundItems.Count);
        Assert.Contains(session.Monsters, monster => monster.Body.Hp == 1);
        Assert.Equal(TileKind.StairsDown, session.Level.At(session.Player.Body.TileX, session.Player.Body.TileY).Kind);
    }

    [Fact]
    public void Floors_left_behind_survive_the_save_boundary()
    {
        RunSession session = NewRun();
        session.Monsters[0].Body.Hp = 1;
        UseTile(session, TileKind.StairsDown);

        RunSnapshot snapshot = session.Capture();
        SnapshotFloorState left = Assert.Single(snapshot.VisitedFloors!);
        Assert.Equal(0, left.RunIndex);

        RunSession restored = new(new ScriptedCatalog(), new GameTuning(), Plan, snapshot, MetaProgression.Fresh, ScriptedRandom.Always(999_999));
        UseTile(restored, TileKind.StairsUp);
        Assert.Contains(restored.Monsters, monster => monster.Body.Hp == 1);
    }
}
