namespace DelveRpg.Kit.Session;

/// <summary>One transient message line with its remaining display time.</summary>
public readonly record struct RunMessage(string Text, int RemainingTicks);

/// <summary>One item lying on the floor.</summary>
public sealed record GroundItem(Inventory.ItemInstance Item, int X, int Y);
