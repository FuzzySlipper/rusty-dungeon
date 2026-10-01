using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Actors;

public enum ActorKind
{
    Player,
    Monster,
}

/// <summary>
/// Shared mutable body state for anything that stands in the dungeon. One
/// instance is the single owner of that actor's position and hit points.
/// </summary>
public sealed class ActorState
{
    public ActorState(long id, ActorKind kind, float x, float y, int maxHp, StatBlock stats)
    {
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        MaxHp = maxHp;
        Hp = maxHp;
        Stats = stats;
    }

    public long Id { get; }

    public ActorKind Kind { get; }

    /// <summary>Position in tile units; tile (x, y) covers [x, x+1) × [y, y+1).</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>Facing in radians, measured clockwise from north (−Y).</summary>
    public float Facing { get; set; }

    /// <summary>Look pitch in degrees; the player's alone is meaningful.</summary>
    public float PitchDegrees { get; set; }

    public int Hp { get; set; }

    public int MaxHp { get; set; }

    public StatBlock Stats { get; set; }

    public int AttackCooldownRemaining { get; set; }

    public Effects.EffectSet Effects { get; } = new();

    public bool Alive => Hp > 0;

    public int TileX => (int)MathF.Floor(X);

    public int TileY => (int)MathF.Floor(Y);
}

/// <summary>The player character of one run.</summary>
public sealed class PlayerState
{
    public PlayerState(ActorState body, Inventory.InventoryStore inventory)
    {
        Body = body;
        Inventory = inventory;
    }

    public ActorState Body { get; }

    public Inventory.InventoryStore Inventory { get; }

    public Inventory.Equipment Equipment { get; } = new();

    public int Gold { get; set; }

    public int Keys { get; set; }

    public int Experience { get; set; }

    public int Level { get; set; } = 1;

    public bool HoldingOrb { get; set; }

    /// <summary>Weapon wind-up progress in ticks.</summary>
    public int AttackCharge { get; set; }

    /// <summary>Which hotbar slot the wielded weapon lives in; −1 when bare-handed.</summary>
    public int WieldedSlot { get; set; } = -1;
}

/// <summary>A monster instance on the current floor.</summary>
public sealed class MonsterState
{
    public MonsterState(ActorState body, MonsterArchetype archetype, int level = 1)
    {
        Body = body;
        Archetype = archetype;
        Level = level;
    }

    public ActorState Body { get; }

    public MonsterArchetype Archetype { get; }

    /// <summary>The level this monster spawned at (see <see cref="Progression.MonsterScaling"/>).</summary>
    public int Level { get; }

    public MonsterBrainState BrainState { get; set; } = MonsterBrainState.Idle;

    /// <summary>Ticks until the brain re-evaluates its path.</summary>
    public int PathRefreshRemaining { get; set; }

    public IReadOnlyList<(int X, int Y)> Path { get; set; } = Array.Empty<(int X, int Y)>();

    public int PathIndex { get; set; }
}

public enum MonsterBrainState
{
    Idle,
    Chasing,
    Fleeing,
}
