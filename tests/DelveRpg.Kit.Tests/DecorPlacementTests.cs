using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class DecorPlacementTests
{
    private static DungeonLevel Room()
    {
        var level = new DungeonLevel(30, 30, 1, "Test");
        for (int y = 1; y < 29; y++)
        {
            for (int x = 1; x < 29; x++)
            {
                level.Set(x, y, Tile.Floor);
            }
        }

        return level;
    }

    private static readonly DecorKind[] Kinds =
    [
        new("decor.rocks", 3, false, false),
        new("decor.stalagmite", 1, false, true),
        new("decor.roots", 1, true, false),
    ];

    [Fact]
    public void Decorations_cluster_on_open_floor_and_repeat_for_the_same_grid()
    {
        IReadOnlyList<Decor> placed = DecorPlacement.Place(Room(), Kinds, 0.1f);

        Assert.NotEmpty(placed);
        Assert.Equal(placed, DecorPlacement.Place(Room(), Kinds, 0.1f));
        Assert.All(placed, decor => Assert.Equal(TileKind.Floor, Room().At((int)MathF.Floor(decor.X), (int)MathF.Floor(decor.Y)).Kind));
        Assert.Contains(placed, decor => decor.OnCeiling);
        Assert.All(placed.Where(decor => decor.Solid), decor => Assert.Equal(0.5f, decor.X - MathF.Floor(decor.X), 3));
    }

    [Fact]
    public void A_bare_theme_or_zero_chance_places_nothing()
    {
        Assert.Empty(DecorPlacement.Place(Room(), [], 0.1f));
        Assert.Empty(DecorPlacement.Place(Room(), Kinds, 0f));
    }
}
