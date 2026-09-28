namespace DelveRpg.Kit.Inventory;

/// <summary>
/// Named equipment slots. The donor draws these as separate HUD button groups;
/// this port keeps three, and the session resolves what each contributes by
/// looking its item id up in the active ruleset.
/// </summary>
public sealed class Equipment
{
    public string? WeaponItemId { get; set; }

    public string? ArmorItemId { get; set; }

    public string? HelmetItemId { get; set; }
}
