namespace DelveRpg.Kit.Progression;

/// <summary>
/// The run-independent record that survives permadeath. The donor keeps this
/// outside the run save; the run directory is deleted when the run ends.
/// </summary>
public sealed record MetaProgression(
    int Gold,
    int Wins,
    int Deaths,
    int HotbarUpgrades,
    int InventoryUpgrades)
{
    public static MetaProgression Fresh { get; } = new(0, 0, 0, 0, 0);

    public int HotbarSize => 6 + HotbarUpgrades;

    public int BackpackSize => 18 + (InventoryUpgrades * 6);
}
