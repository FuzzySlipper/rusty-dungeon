using DelveRpg.Kit.Combat;

namespace DelveRpg.Kit.World;

/// <summary>
/// Spikes that spring from a floor tile when something moves over it — the
/// donor's ProximitySpikes ([donor] entities/Spikes.java; [data]
/// entities.dat ProximitySpikes): they rise over 10 ticks, strike for 2 once
/// per body at full extension, stay up 20 ticks, sink over 10 and rearm
/// after 80.
/// </summary>
public sealed class SpikeTrap
{
    public const int RiseTicks = 10;
    public const int UpTicks = 20;
    public const int RearmTicks = 80;
    public const int Damage = 2;

    public SpikeTrap(long id, int tileX, int tileY)
    {
        Id = id;
        TileX = tileX;
        TileY = tileY;
    }

    public long Id { get; }

    public int TileX { get; }

    public int TileY { get; }

    public SpikePhase Phase { get; set; } = SpikePhase.Armed;

    /// <summary>Ticks spent in the current phase.</summary>
    public int PhaseTicks { get; set; }

    /// <summary>How far out the spikes are, 0 (flush, hidden) to 1 (fully out).</summary>
    public float Extension => Phase switch
    {
        SpikePhase.Rising => Math.Min(1f, PhaseTicks / (float)RiseTicks),
        SpikePhase.Up => 1f,
        SpikePhase.Sinking => Math.Max(0f, 1f - (PhaseTicks / (float)RiseTicks)),
        _ => 0f,
    };

    /// <summary>Bodies already struck this extension (the donor's hitAlready).</summary>
    public HashSet<long> Struck { get; } = new();
}

public enum SpikePhase
{
    Armed,
    Rising,
    Up,
    Sinking,
    Resting,
}

/// <summary>
/// A tile that fires a trigger id when touched — a visible pressure plate
/// that any body sets off, or a hidden tripwire only the player sets off
/// ([donor] entities/triggers/Trigger.java PLAYER_TOUCHED / ANY_TOUCHED,
/// [data] entities.dat PressureTrap, Magic Missile Trap). It fires once per
/// step onto it, then waits out its reset time.
/// </summary>
public sealed class TouchTrigger
{
    public TouchTrigger(long id, int tileX, int tileY, string targetId, bool isPlate, int resetTicks)
    {
        Id = id;
        TileX = tileX;
        TileY = tileY;
        TargetId = targetId;
        IsPlate = isPlate;
        ResetTicks = resetTicks;
    }

    public long Id { get; }

    public int TileX { get; }

    public int TileY { get; }

    /// <summary>The trigger id it fires; every effect with that id answers.</summary>
    public string TargetId { get; }

    /// <summary>A visible plate any body presses; otherwise a hidden tripwire for the player.</summary>
    public bool IsPlate { get; }

    public int ResetTicks { get; }

    public int ResetRemaining { get; set; }

    /// <summary>Something stands on it now; it fires again only after being left.</summary>
    public bool Pressed { get; set; }
}

public enum TrapEffectKind
{
    /// <summary>A fire burst for 6 + half the dungeon level ([donor] entities/triggers/TriggeredTrap.java:35).</summary>
    FireBurst,

    /// <summary>A poison burst for 2 (TriggeredTrap.java:44).</summary>
    PoisonBurst,

    /// <summary>Whoever stands there is thrown elsewhere on the floor (TriggeredTrap.java:39-41).</summary>
    Teleport,

    /// <summary>A magic bolt from a wall at the player (the donor's Magic Missile Trap spawner).</summary>
    WallBolt,
}

/// <summary>What a trigger sets off, at a place: answers every trigger that names its id.</summary>
public sealed record TrapEffect(string TriggerId, TrapEffectKind Kind, float X, float Y);

public enum PotKind
{
    /// <summary>[data] entities.dat Pot_0: 2 hit points, a surprise half the time.</summary>
    Sturdy,

    /// <summary>Pot_1: 1 hit point, a surprise half the time.</summary>
    Fragile,

    /// <summary>Pot_Exploding: 1 hit point, bursts when broken.</summary>
    Exploding,
}

/// <summary>A breakable pot ([donor] entities/Breakable.java). Any damage counts.</summary>
public sealed class Pot
{
    public Pot(long id, PotKind kind, float x, float y)
    {
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        Hp = kind == PotKind.Sturdy ? 2 : 1;
    }

    public long Id { get; }

    public PotKind Kind { get; }

    public float X { get; }

    public float Y { get; }

    public int Hp { get; set; }

    public int TileX => (int)MathF.Floor(X);

    public int TileY => (int)MathF.Floor(Y);
}

/// <summary>A burst going off: a moment of fire, poison or force the Host draws.</summary>
public sealed record Burst(long Id, float X, float Y, DamageType DamageType, long AtTick);

/// <summary>Every trap, trigger and pot on one floor, as generation placed them.</summary>
public sealed record FloorFeatures(
    IReadOnlyList<SpikeTrap> Spikes,
    IReadOnlyList<TouchTrigger> Triggers,
    IReadOnlyList<TrapEffect> Effects,
    IReadOnlyList<Pot> Pots)
{
    public static FloorFeatures None { get; } = new([], [], [], []);
}
