using DelveRpg.Host.Hud;
using Xunit;

namespace DelveRpg.Host.Tests;

public sealed class HudLayoutTests
{
    [Fact]
    public void The_hotbar_is_one_row_centred_along_the_bottom()
    {
        HudRect first = HudLayout.HotbarSlot(0, 6);
        HudRect last = HudLayout.HotbarSlot(5, 6);

        Assert.Equal(0.5f, (first.X + last.X + last.Width) / 2f, 4);
        Assert.Equal(first.Y, last.Y);
        Assert.Equal(first.X + first.Width, HudLayout.HotbarSlot(1, 6).X, 4);
        Assert.True(first.Y + first.Height < 0.2f);
    }

    [Fact]
    public void The_backpack_stacks_rows_of_the_hotbar_width_above_it()
    {
        HudRect hotbar = HudLayout.HotbarSlot(0, 6);
        HudRect firstBag = HudLayout.BackpackSlot(0, 6);
        HudRect seventhBag = HudLayout.BackpackSlot(6, 6);

        Assert.Equal(hotbar.X, firstBag.X, 4);
        Assert.True(firstBag.Y >= hotbar.Y + hotbar.Height);
        Assert.Equal(hotbar.X, seventhBag.X, 4);
        Assert.Equal(firstBag.Y + firstBag.Height, seventhBag.Y, 4);
    }

    [Fact]
    public void An_icon_sits_inside_its_slot_and_the_health_bar_clears_the_hotbar()
    {
        HudRect slot = HudLayout.HotbarSlot(2, 6);
        HudRect icon = HudLayout.Icon(slot);

        Assert.True(icon.X > slot.X && icon.X + icon.Width < slot.X + slot.Width);
        Assert.True(icon.Y > slot.Y && icon.Y + icon.Height < slot.Y + slot.Height);
        Assert.True(HudLayout.HealthBar.X + HudLayout.HealthBar.Width < HudLayout.HotbarSlot(0, 10).X);
    }
}
