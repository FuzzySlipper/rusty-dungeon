namespace DelveRpg.Kit.Progression;

/// <summary>
/// The run-independent record that survives permadeath. The donor keeps this
/// outside the run save; the run directory is deleted when the run ends.
/// Gold is one purse: a run starts with it and every run, won or lost, puts
/// back what it ends with ([donor] game/Game.java:405-413, 918;
/// GameOverScreen.java:175-183; WinScreen.java:103-105).
/// </summary>
public sealed record MetaProgression(
    int Gold,
    int Wins,
    int Deaths,
    int HotbarUpgrades,
    int InventoryUpgrades)
{
    public static MetaProgression Fresh { get; } = new(0, 0, 0, 0, 0);

    /// <summary>Hotbar slots: six here (the donor starts at five), one more per belt expansion.</summary>
    public int HotbarSize => 6 + HotbarUpgrades;

    /// <summary>Backpack slots: the donor's 18, one more per bag expansion ([donor] entities/Player.java:137-138, 283-286).</summary>
    public int BackpackSize => 18 + InventoryUpgrades;

    /// <summary>Gear bought in camp for the next run, as (item id, item level).</summary>
    public IReadOnlyList<StashedItem>? Stash { get; init; }
}

/// <summary>One item waiting in camp for the next descent.</summary>
public sealed record StashedItem(string ItemId, int ItemLevel);
