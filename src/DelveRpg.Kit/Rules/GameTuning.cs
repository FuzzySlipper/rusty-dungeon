namespace DelveRpg.Kit.Rules;

/// <summary>
/// Adjustable gameplay values for one run composition. A ruleset fills these
/// from its tuning profile; call sites never hardcode them.
/// </summary>
public sealed record GameTuning
{
    /// <summary>Movement speed ceiling in tiles per tick.</summary>
    public float MaxWalkSpeed { get; init; } = 0.06f;

    /// <summary>Velocity added per tick while a move intent is held.</summary>
    public float WalkAcceleration { get; init; } = 0.02f;

    /// <summary>Velocity retained per tick without input.</summary>
    public float WalkFriction { get; init; } = 0.8f;

    /// <summary>Full charge of a swing without a weapon, in ticks (donor attackChargeTime).</summary>
    public int AttackChargeTicks { get; init; } = 40;

    /// <summary>Time between two attacks from the same actor, in ticks.</summary>
    public int AttackCooldownTicks { get; init; } = 30;

    /// <summary>Chance a physical attack is dodged outright.</summary>
    public float DodgeChance { get; init; } = 0.15f;

    /// <summary>Chance a dropped item carries a prefix/suffix enchantment.</summary>
    public float EnchantChance { get; init; } = 0.2f;

    /// <summary>Hit points at which a monster stops fighting and runs.</summary>
    public float FleeHpFraction { get; init; } = 0.25f;

    /// <summary>Ticks between escape-arc spawns while the objective is held.</summary>
    public int EscapeSpawnCadenceStartTicks { get; init; } = 600;

    /// <summary>Fastest escape-arc spawn cadence, in ticks.</summary>
    public int EscapeSpawnCadenceEndTicks { get; init; } = 60;

    /// <summary>Monsters per escape-arc spawn at the start of the arc.</summary>
    public int EscapeSpawnGroupStart { get; init; } = 3;

    /// <summary>Monsters per escape-arc spawn at full pressure.</summary>
    public int EscapeSpawnGroupEnd { get; init; } = 15;

    /// <summary>
    /// Closest two bodies come, centre to centre, in tiles. A chasing monster
    /// holds here instead of walking into the player, and the player cannot
    /// walk closer to a monster than this. Below the monsters' attack range.
    /// </summary>
    public float ActorSeparationTiles { get; init; } = 0.75f;

    /// <summary>
    /// How close a monster in sight notices the player standing in the dark,
    /// in tiles ([donor] Monster.java:554-566: 3).
    /// </summary>
    public float NoticeDarkTiles { get; init; } = 3f;

    /// <summary>
    /// How much further it notices a fully lit player ([donor] Monster.java:556:
    /// <c>visiblityMod × 15</c>).
    /// </summary>
    public float NoticeLightTiles { get; init; } = 15f;

    /// <summary>Chance a floor gets a locked vault, when one of its doors can wall off a side room.</summary>
    public float VaultChance { get; init; } = 0.6f;

    /// <summary>Chance per open floor tile of a trap ([donor] DelverGameMode.java generateTraps: 0.012).</summary>
    public float TrapChance { get; init; } = 0.012f;

    /// <summary>Chance a room hides a wall-bolt tripwire.</summary>
    public float WallBoltRoomChance { get; init; } = 0.1f;

    /// <summary>Chance per floor tile against a room wall of a pot.</summary>
    public float PotChance { get; init; } = 0.03f;

    /// <summary>Radius, in tiles, of the remembered map window around the player.</summary>
    public int SeenRadius { get; init; } = 5;

    /// <summary>Gold a fresh character starts with.</summary>
    public int StartingGold { get; init; } = 40;

    /// <summary>Eye height above the floor, in tiles.</summary>
    public float EyeHeight { get; init; } = 0.5f;
}
