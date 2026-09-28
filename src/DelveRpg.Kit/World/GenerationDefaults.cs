namespace DelveRpg.Kit.World;

/// <summary>
/// Generation defaults for one floor. Floor size grows a little with
/// difficulty; the counts keep a floor readable at the HUD minimap's window.
/// </summary>
public static class GenerationDefaults
{
    public static GenerationConfig For(Rules.FloorSpec floor)
    {
        int depth = Math.Max(1, floor.DungeonLevel);
        return new GenerationConfig(
            Width: 48,
            Height: 48,
            RoomCountMin: 6,
            RoomCountMax: 10,
            RoomSizeMin: 4,
            RoomSizeMax: 8,
            MonsterCount: 5 + (depth / 2),
            ItemCount: 3 + (depth / 3));
    }
}
