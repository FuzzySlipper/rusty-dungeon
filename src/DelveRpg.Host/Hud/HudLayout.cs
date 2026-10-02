namespace DelveRpg.Host.Hud;

/// <summary>A screen rectangle in viewport fractions, from the lower left (the Engine's sprite placement space).</summary>
public readonly record struct HudRect(float X, float Y, float Width, float Height);

/// <summary>
/// Where the donor-style HUD sits on screen, in viewport fractions. One owner
/// for both halves of the HUD: the Engine draws item icons into these
/// rectangles and the DOM draws slot frames, keys, counts and the health bar
/// over the same ones (the projection carries them), because the DOM cannot
/// show content-bundle images.
/// <list type="bullet">
/// <item>The hotbar is one row of square-ish slots centred along the bottom,
/// and the open inventory stacks the backpack in rows of the same width above
/// it ([donor] ui/Hotbar.java:80-100, columns = hotbar size).</item>
/// <item>The health bar sits at the bottom left
/// ([donor] gfx/GlRenderer.java:1603-1624).</item>
/// </list>
/// </summary>
public static class HudLayout
{
    /// <summary>A slot is a tenth of the screen tall and as wide on a 16:10 screen.</summary>
    public const float SlotWidth = 0.0625f;
    public const float SlotHeight = 0.1f;
    private const float Bottom = 0.02f;
    private const float RowGap = 0.01f;

    /// <summary>An icon fills the middle of its slot.</summary>
    private const float IconInset = 0.15f;

    public static readonly HudRect HealthBar = new(0.02f, Bottom, 0.15f, 0.05f);

    /// <summary>Hotbar slot <paramref name="index"/> of <paramref name="hotbarSize"/>.</summary>
    public static HudRect HotbarSlot(int index, int hotbarSize) => Cell(index, 0, hotbarSize);

    /// <summary>
    /// Backpack slot <paramref name="index"/> (0 = first after the hotbar), in
    /// rows of the hotbar's width rising above it.
    /// </summary>
    public static HudRect BackpackSlot(int index, int hotbarSize)
    {
        int columns = Math.Max(1, hotbarSize);
        return Cell(index % columns, 1 + (index / columns), columns);
    }

    /// <summary>The part of a slot its item icon covers.</summary>
    public static HudRect Icon(HudRect slot) => new(
        slot.X + (slot.Width * IconInset),
        slot.Y + (slot.Height * IconInset),
        slot.Width * (1f - (2f * IconInset)),
        slot.Height * (1f - (2f * IconInset)));

    private static HudRect Cell(int column, int row, int columns) => new(
        0.5f - (columns * SlotWidth / 2f) + (column * SlotWidth),
        Bottom + (row * SlotHeight) + (row > 0 ? RowGap : 0f),
        SlotWidth,
        SlotHeight);
}
