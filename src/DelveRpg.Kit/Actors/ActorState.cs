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

    /// <summary>Height of the feet above the start's ground level, in tiles.</summary>
    public float Z { get; set; }

    /// <summary>Vertical velocity in tiles per tick; the player's alone jumps and falls.</summary>
    public float VelocityZ { get; set; }

    /// <summary>True while standing on the floor rather than in the air.</summary>
    public bool Grounded { get; set; } = true;

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

    /// <summary>
    /// How visible the player is, 0 (dark) to 1 (lit, or attacking this tick):
    /// the donor's visiblityMod. Monsters notice within <c>3 + 15 × this</c> tiles.
    /// </summary>
    public float Visibility { get; set; }

    /// <summary>Ticks left on the red hurt flash; set whenever the player takes damage.</summary>
    public int HurtFlashRemaining { get; set; }

    /// <summary>The swing or shot playing out, for its blow and for the held animation; null at rest.</summary>
    public SwingState? Swing { get; set; }

    /// <summary>Which hotbar slot the wielded weapon lives in; −1 when bare-handed.</summary>
    public int WieldedSlot { get; set; } = -1;

    /// <summary>Which slot the worn body armor lives in; −1 when none.</summary>
    public int ArmorSlot { get; set; } = -1;

    /// <summary>Which slot the worn helmet lives in; −1 when none.</summary>
    public int HelmetSlot { get; set; } = -1;
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

    /// <summary>Knockback velocity in tiles per tick; it slides off under floor friction.</summary>
    public float VelocityX { get; set; }

    public float VelocityY { get; set; }

    /// <summary>Ticks the monster stands stunned by a blow's knockback.</summary>
    public int StunTicksRemaining { get; set; }

    /// <summary>Ticks left in the hurt flinch after a pain roll; it cannot move meanwhile.</summary>
    public int HurtTicksRemaining { get; set; }

    /// <summary>Ticks left in the death stagger; above zero the monster is dying and does nothing.</summary>
    public int DyingTicksRemaining { get; set; }

    public bool IsDying => DyingTicksRemaining > 0 || !Body.Alive;

    /// <summary>Ticks until a blow being wound up lands; zero when not attacking.</summary>
    public int AttackWindupRemaining { get; set; }

    /// <summary>Ticks since the current or last melee attack began; the attack animation reads it.</summary>
    public int AttackElapsedTicks { get; set; } = int.MaxValue;

    /// <summary>The neighbouring tile an idle monster is wandering to; null when it has none.</summary>
    public (int X, int Y)? WanderTarget { get; set; }

    /// <summary>The tile it last wandered from, so it does not double straight back.</summary>
    public (int X, int Y)? WanderFrom { get; set; }

    /// <summary>Ticks until the wander picks again.</summary>
    public int WanderTicksRemaining { get; set; }

    /// <summary>True while an idle monster stands resting instead of wandering.</summary>
    public bool WanderResting { get; set; }

    /// <summary>True once the player has hurt it: it then notices the player on sight at any range.</summary>
    public bool WasHit { get; set; }

    /// <summary>Ticks until this monster may cast its ranged bolt again.</summary>
    public int RangedCooldownRemaining { get; set; }

    /// <summary>What the monster has caught (arrows shot into it); dropped where it falls.</summary>
    public List<Inventory.ItemInstance> Carried { get; } = new();
}

public enum MonsterBrainState
{
    Idle,
    Chasing,
    Fleeing,
}

/// <summary>
/// One swing in progress. <see cref="Strong"/> picks the full-charge
/// animation; <see cref="Power"/> is the charge it was released at;
/// <see cref="HitAtTicks"/> is when the blow lands (already true of a shot,
/// whose effect is immediate).
/// </summary>
public sealed class SwingState
{
    public SwingState(bool strong, float power, int lengthTicks, int hitAtTicks, bool landed)
    {
        Strong = strong;
        Power = power;
        LengthTicks = lengthTicks;
        HitAtTicks = hitAtTicks;
        Landed = landed;
    }

    public bool Strong { get; }

    public float Power { get; }

    public int LengthTicks { get; }

    public int HitAtTicks { get; }

    public int ElapsedTicks { get; set; }

    public bool Landed { get; set; }

    /// <summary>How far through the swing, 0 to 1.</summary>
    public float Progress => LengthTicks <= 0 ? 1f : Math.Min(1f, ElapsedTicks / (float)LengthTicks);
}
