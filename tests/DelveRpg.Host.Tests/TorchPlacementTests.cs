using DelveRpg.Host.Presentation;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Host.Tests;

public sealed class TorchPlacementTests
{
    private static DungeonLevel Hall(int width, int height)
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

    [Fact]
    public void Torches_hang_on_walls_spaced_apart_and_repeat_for_the_same_grid()
    {
        DungeonLevel level = Hall(30, 20);
        IReadOnlyList<WallTorch> torches = TorchPlacement.Place(level);

        Assert.NotEmpty(torches);
        Assert.Equal(torches, TorchPlacement.Place(Hall(30, 20)));
        foreach (WallTorch torch in torches)
        {
            // The sprite sits against a wall face: inside a floor tile, at its edge.
            int tileX = (int)MathF.Floor(torch.SpritePosition.X);
            int tileY = (int)MathF.Floor(torch.SpritePosition.Z);
            Assert.Equal(TileKind.Floor, level.At(tileX, tileY).Kind);
            float fx = torch.SpritePosition.X - tileX;
            float fy = torch.SpritePosition.Z - tileY;
            Assert.True(fx < 0.1f || fx > 0.9f || fy < 0.1f || fy > 0.9f);
        }

        for (int i = 0; i < torches.Count; i++)
        {
            for (int j = i + 1; j < torches.Count; j++)
            {
                bool apart = MathF.Abs(torches[i].SpritePosition.X - torches[j].SpritePosition.X) >= TorchPlacement.Spacing - 1
                    || MathF.Abs(torches[i].SpritePosition.Z - torches[j].SpritePosition.Z) >= TorchPlacement.Spacing - 1;
                Assert.True(apart);
            }
        }
    }

    [Fact]
    public void A_grid_with_no_open_floor_gets_no_torches()
    {
        Assert.Empty(TorchPlacement.Place(new DungeonLevel(8, 8, 1, "Test")));
    }
}
