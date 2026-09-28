namespace DelveRpg.Kit.Session;

/// <summary>
/// One tick of semantic player intent. The Host translates Engine input events
/// into this and the session interprets it; no Engine types cross the seam.
/// </summary>
public readonly record struct RunInput(
    float MoveX,
    float MoveY,
    float LookYawDegrees,
    float LookPitchDegrees,
    bool AttackHeld,
    bool UsePressed,
    int HotbarPressed,
    bool InventoryToggled,
    bool MapToggled,
    bool MenuConfirm,
    bool MenuCancel,
    bool MenuUp,
    bool MenuDown)
{
    public static RunInput Idle => new(0f, 0f, 0f, 0f, false, false, 0, false, false, false, false, false, false);
}
