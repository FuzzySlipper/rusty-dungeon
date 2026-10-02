namespace DelveRpg.Kit.Session;

/// <summary>One transient message line with its remaining display time.</summary>
public readonly record struct RunMessage(string Text, int RemainingTicks);

/// <summary>One item lying on the floor; the id is stable while it lies there.</summary>
public sealed record GroundItem(long Id, Inventory.ItemInstance Item, int X, int Y);
