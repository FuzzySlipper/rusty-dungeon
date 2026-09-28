using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class FogMapTests
{
    private static DungeonLevel OpenRoomWithWall()
    {
        var level = new DungeonLevel(9, 9, 1, "Test");
        for (int y = 1; y < 8; y++)
        {
            for (int x = 1; x < 8; x++)
            {
                level.Set(x, y, Tile.Floor);
            }
        }

        level.Set(5, 4, Tile.Wall);
        return level;
    }

    [Fact]
    public void Walls_block_sight()
    {
        DungeonLevel level = OpenRoomWithWall();
        Assert.False(LineOfSight.CanSee(level, 2, 4, 7, 4));
        Assert.True(LineOfSight.CanSee(level, 2, 4, 4, 4));
    }

    [Fact]
    public void Explored_tiles_stay_explored_after_leaving()
    {
        DungeonLevel level = OpenRoomWithWall();
        var fog = new FogMap(level.Width, level.Height);

        fog.Reveal(level, 2, 2, 2);
        Assert.True(fog.IsExplored(2, 2));

        fog.Reveal(level, 7, 7, 1);
        Assert.True(fog.IsExplored(2, 2));
        Assert.False(fog.IsVisible(2, 2));
    }

    [Fact]
    public void Closed_doors_block_sight_until_opened()
    {
        var level = new DungeonLevel(9, 3, 1, "Test");
        for (int x = 0; x < 9; x++)
        {
            level.Set(x, 1, Tile.Floor);
        }

        level.Set(5, 1, Tile.DoorClosed);
        Assert.False(LineOfSight.CanSee(level, 1, 1, 8, 1));
        level.TryOpenDoor(5, 1);
        Assert.True(LineOfSight.CanSee(level, 1, 1, 8, 1));
    }
}
