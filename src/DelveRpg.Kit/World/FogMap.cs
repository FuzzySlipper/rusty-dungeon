namespace DelveRpg.Kit.World;

/// <summary>
/// The remembered map: tiles the player has seen stay explored, exactly like
/// the donor's sticky per-tile flag. Visibility itself is recomputed per visit;
/// the remembered map feeds the HUD minimap and the map view.
/// </summary>
public sealed class FogMap
{
    private readonly bool[] _explored;
    private readonly bool[] _visible;

    public FogMap(int width, int height)
    {
        Width = width;
        Height = height;
        _explored = new bool[width * height];
        _visible = new bool[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsExplored(int x, int y) => _explored[(y * Width) + x];

    public bool IsVisible(int x, int y) => _visible[(y * Width) + x];

    /// <summary>
    /// Recompute the visible window around the observer and merge it into the
    /// remembered map. Tiles within the square window that can be seen become
    /// visible and stay explored afterwards.
    /// </summary>
    public void Reveal(DungeonLevel level, int observerX, int observerY, int radius)
    {
        Array.Clear(_visible);
        for (int y = observerY - radius; y <= observerY + radius; y++)
        {
            for (int x = observerX - radius; x <= observerX + radius; x++)
            {
                if (!level.InBounds(x, y))
                {
                    continue;
                }

                if (LineOfSight.CanSee(level, observerX, observerY, x, y))
                {
                    int index = (y * Width) + x;
                    _visible[index] = true;
                    _explored[index] = true;
                }
            }
        }
    }
}
