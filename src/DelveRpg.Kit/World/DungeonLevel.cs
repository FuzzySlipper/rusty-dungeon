namespace DelveRpg.Kit.World;

public enum TileKind : byte
{
    Wall,
    Floor,
    DoorClosed,
    DoorOpen,
    StairsDown,
    StairsUp,
    Water,
}

/// <summary>One grid cell. The tile kind owns movement and sight blocking.</summary>
public readonly record struct Tile(TileKind Kind)
{
    public bool BlocksMovement => Kind is TileKind.Wall or TileKind.DoorClosed;

    public bool BlocksSight => Kind is TileKind.Wall or TileKind.DoorClosed;

    public bool IsWalkable => !BlocksMovement;

    public static readonly Tile Wall = new(TileKind.Wall);
    public static readonly Tile Floor = new(TileKind.Floor);
    public static readonly Tile DoorClosed = new(TileKind.DoorClosed);
    public static readonly Tile DoorOpen = new(TileKind.DoorOpen);
    public static readonly Tile StairsDown = new(TileKind.StairsDown);
    public static readonly Tile StairsUp = new(TileKind.StairsUp);
    public static readonly Tile Water = new(TileKind.Water);
}

/// <summary>
/// One dungeon floor: a tile grid plus the spawn rows the session placed on
/// it. The grid is the single owner of walkability and sight blocking.
/// </summary>
public sealed class DungeonLevel
{
    private readonly Tile[] _tiles;

    public DungeonLevel(int width, int height, int dungeonLevel, string theme)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        DifficultyLevel = dungeonLevel;
        Theme = theme;
        _tiles = new Tile[width * height];
        Array.Fill(_tiles, Tile.Wall);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Difficulty level of this floor (drives monster and loot levels).</summary>
    public int DifficultyLevel { get; }

    public string Theme { get; }

    public Tile At(int x, int y) => _tiles[(y * Width) + x];

    public void Set(int x, int y, Tile tile) => _tiles[(y * Width) + x] = tile;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsWalkable(int x, int y) => InBounds(x, y) && At(x, y).IsWalkable;

    /// <summary>
    /// Reachable ground for connectivity and navigation: walkable tiles plus
    /// closed doors, which any actor can open. Spawn placement and monster
    /// paths care about this; movement and sight do not.
    /// </summary>
    public bool IsNavigable(int x, int y) => InBounds(x, y) && (At(x, y).IsWalkable || At(x, y).Kind == TileKind.DoorClosed);

    public bool BlocksSight(int x, int y) => !InBounds(x, y) || At(x, y).BlocksSight;

    /// <summary>Open a closed door in place; returns false when there is none.</summary>
    public bool TryOpenDoor(int x, int y)
    {
        if (!InBounds(x, y) || At(x, y).Kind != TileKind.DoorClosed)
        {
            return false;
        }

        Set(x, y, Tile.DoorOpen);
        return true;
    }
}
