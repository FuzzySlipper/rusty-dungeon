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

    /// <summary>A door that only a key opens (the donor's isLocked + takesKey door).</summary>
    DoorLocked,
}

/// <summary>One grid cell. The tile kind owns movement and sight blocking.</summary>
public readonly record struct Tile(TileKind Kind)
{
    public bool BlocksMovement => Kind is TileKind.Wall or TileKind.DoorClosed or TileKind.DoorLocked;

    public bool BlocksSight => Kind is TileKind.Wall or TileKind.DoorClosed or TileKind.DoorLocked;

    public bool IsWalkable => !BlocksMovement;

    public static readonly Tile Wall = new(TileKind.Wall);
    public static readonly Tile Floor = new(TileKind.Floor);
    public static readonly Tile DoorClosed = new(TileKind.DoorClosed);
    public static readonly Tile DoorOpen = new(TileKind.DoorOpen);
    public static readonly Tile DoorLocked = new(TileKind.DoorLocked);
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
    private readonly float[] _floors;
    private readonly float[] _ceilings;

    /// <summary>How far below its floor height a water tile's bed lies ([donor] tiles/Tile.java:167, 195: 0.4).</summary>
    public const float WaterDepth = 0.4f;

    /// <summary>Default ceiling height above a floor at height zero ([donor] Tile.java:76-77: one tile).</summary>
    public const float DefaultCeiling = 1f;

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
        _floors = new float[width * height];
        _ceilings = new float[width * height];
        Array.Fill(_ceilings, DefaultCeiling);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Difficulty level of this floor (drives monster and loot levels).</summary>
    public int DifficultyLevel { get; }

    public string Theme { get; }

    /// <summary>
    /// Monotonic change counter for the grid. Presentation rebuilds the scene
    /// when it moves, so every visible tile change (door open, new floor) must
    /// pass through a mutator — a stale mesh leaves phantom walls standing.
    /// </summary>
    public ulong Revision { get; private set; }

    public Tile At(int x, int y) => _tiles[(y * Width) + x];

    public void Set(int x, int y, Tile tile)
    {
        _tiles[(y * Width) + x] = tile;
        Revision++;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>A tile's floor height, in tiles; zero is the ground level of the start.</summary>
    public float FloorHeight(int x, int y) => InBounds(x, y) ? _floors[(y * Width) + x] : 0f;

    /// <summary>A tile's ceiling height, in tiles.</summary>
    public float CeilingHeight(int x, int y) => InBounds(x, y) ? _ceilings[(y * Width) + x] : DefaultCeiling;

    /// <summary>Where a body stands on a tile: its floor, or the bed under its water.</summary>
    public float StandHeight(int x, int y) =>
        FloorHeight(x, y) - (InBounds(x, y) && At(x, y).Kind == TileKind.Water ? WaterDepth : 0f);

    /// <summary>Set a tile's floor and ceiling heights (generation and restore).</summary>
    public void SetHeights(int x, int y, float floor, float ceiling)
    {
        _floors[(y * Width) + x] = floor;
        _ceilings[(y * Width) + x] = Math.Max(ceiling, floor + 0.5f);
        Revision++;
    }

    /// <summary>
    /// True when a body standing at <paramref name="height"/> can walk onto a
    /// tile: it is walkable and its standing height is no more than a step up.
    /// </summary>
    /// <summary>
    /// The height a climb out of a tile starts from: the feet, or for a
    /// wader the floor around the water, so water never traps
    /// ([donor] entities/Player.java:619-620 stepHeight + depth in water).
    /// </summary>
    public float ClimbBase(int x, int y, float feet) =>
        InBounds(x, y) && At(x, y).Kind == TileKind.Water ? Math.Max(feet, FloorHeight(x, y)) : feet;

    public bool CanStepOnto(int x, int y, float height, float stepHeight) =>
        IsWalkable(x, y) && StandHeight(x, y) <= height + stepHeight;

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
