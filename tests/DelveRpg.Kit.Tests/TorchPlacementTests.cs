using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

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
            // It hangs in a floor tile, against the wall it points at.
            Assert.Equal(TileKind.Floor, level.At(torch.TileX, torch.TileY).Kind);
            Assert.Equal(TileKind.Wall, level.At(torch.TileX + torch.WallDx, torch.TileY + torch.WallDy).Kind);
            Assert.Equal((int)MathF.Floor(torch.SpriteX), torch.TileX);
            Assert.Equal((int)MathF.Floor(torch.SpriteY), torch.TileY);
        }

        for (int i = 0; i < torches.Count; i++)
        {
            for (int j = i + 1; j < torches.Count; j++)
            {
                bool apart = Math.Abs(torches[i].TileX - torches[j].TileX) >= TorchPlacement.Spacing
                    || Math.Abs(torches[i].TileY - torches[j].TileY) >= TorchPlacement.Spacing;
                Assert.True(apart);
            }
        }
    }

    [Fact]
    public void A_grid_with_no_open_floor_gets_no_torches()
    {
        Assert.Empty(TorchPlacement.Place(new DungeonLevel(8, 8, 1, "Test")));
    }

    [Fact]
    public void Torchlight_falls_off_over_the_donor_range_and_walls_shadow_it()
    {
        DungeonLevel level = Hall(12, 5);
        var torch = new WallTorch(3, 1, 0, -1); // on the north wall of tile (3, 1)
        WallTorch[] torches = [torch];

        // Within half the range the donor's doubled falloff saturates at 1.
        Assert.Equal(1f, LightLevel.At(level, torches, torch.LightX, torch.LightY + 1f));
        // At 2.4 of 3.2: 2 × (1 − 0.75) = 0.5.
        Assert.Equal(0.5f, LightLevel.At(level, torches, torch.LightX + 2.4f, torch.LightY), 3);
        Assert.Equal(0f, LightLevel.At(level, torches, torch.LightX + 3.3f, torch.LightY));

        // A wall between the torch and the point leaves it dark.
        level.Set(4, 1, Tile.Wall);
        level.Set(4, 2, Tile.Wall);
        level.Set(4, 3, Tile.Wall);
        Assert.Equal(0f, LightLevel.At(level, torches, 5.5f, 1.5f));
    }
}
