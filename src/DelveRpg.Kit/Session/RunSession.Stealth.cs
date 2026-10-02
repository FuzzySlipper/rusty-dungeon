using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

/// <summary>
/// The donor's light-based stealth ([donor] entities/Player.java:852-854,
/// :1371, :1620, entities/Monster.java:554-566). The player's visibility is
/// the light where they stand, squared and capped at 1. The torch they
/// carry does not count, as the donor's player light is not a held item.
/// Attacking makes them fully visible for that tick. A monster in sight
/// notices them inside <c>3 + 15 × visibility</c> tiles: 3 in the dark,
/// 18 under a torch.
/// </summary>
public sealed partial class RunSession
{
    private bool _attackedThisTick;

    /// <summary>How close a monster in sight notices the player this tick.</summary>
    public float NoticeRadius => _tuning.NoticeDarkTiles + (_tuning.NoticeLightTiles * Player.Visibility);

    private void TickStealth()
    {
        float light = LightLevel.At(Level, Torches, Player.Body.X, Player.Body.Y);
        Player.Visibility = _attackedThisTick ? 1f : Math.Min(1f, light * light);
        _attackedThisTick = false;
    }
}
