namespace DelveRpg.Kit.Session;

/// <summary>Something that happened this tick that a presentation might voice or show.</summary>
public enum CueKind
{
    Swing,
    MonsterHurt,
    MonsterDie,
    MonsterAlert,
    MonsterAttack,
    MonsterCast,
    PlayerHurt,
    Footstep,
    Wade,
    DoorOpen,
    DoorLocked,
    Pickup,
    PickupGold,
    Equip,
    Drink,
    Eat,
    Bow,
    Zap,
    Fizzle,
    PotBreak,
    Explode,
    Spikes,
    WallBolt,
    Stairs,
    LevelUp,
    Death,
    Escape,
}

/// <summary>
/// One cue: what happened, where on the floor (tile units), and whose it was
/// (a monster or item archetype id, when one is the subject).
/// </summary>
public readonly record struct RunCue(CueKind Kind, float X, float Y, string? Subject = null);

/// <summary>
/// The tick's cues: a read model of events, like the message log, for the
/// Host to voice. The session never reads them back; they are cleared at the
/// start of every tick.
/// </summary>
public sealed partial class RunSession
{
    /// <summary>The donor plays a footstep every 25 ticks of walking ([donor] entities/Player.java:605-609).</summary>
    public const int FootstepTicks = 25;

    private readonly List<RunCue> _cues = new();
    private int _footstepTicks = FootstepTicks;

    /// <summary>Cues raised during the last tick, in order.</summary>
    public IReadOnlyList<RunCue> Cues => _cues;

    private void Cue(CueKind kind, float x, float y, string? subject = null) =>
        _cues.Add(new RunCue(kind, x, y, subject));

    private void CueAtPlayer(CueKind kind, string? subject = null) =>
        Cue(kind, Player.Body.X, Player.Body.Y, subject);

    /// <summary>Footsteps while walking on the ground; wading in water sounds different.</summary>
    private void TickFootsteps()
    {
        if (!Player.Body.Grounded || PlayerSpeed < 0.01f)
        {
            _footstepTicks = Math.Min(_footstepTicks, FootstepTicks / 2);
            return;
        }

        if (--_footstepTicks > 0)
        {
            return;
        }

        _footstepTicks = FootstepTicks;
        bool wading = Level.At(Player.Body.TileX, Player.Body.TileY).Kind == World.TileKind.Water;
        CueAtPlayer(wading ? CueKind.Wade : CueKind.Footstep);
    }
}
