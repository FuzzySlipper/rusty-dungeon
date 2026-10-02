using DelveRpg.Kit.Combat;

namespace DelveRpg.Kit.Rules;

/// <summary>
/// A ruleset-supplied monster definition. Neutral on purpose: the Kit decides
/// how these values act; the ruleset decides what they mean.
/// </summary>
public sealed record MonsterArchetype(
    string Id,
    string DisplayName,
    StatBlock Stats,
    int BaseHp,
    int AttackPower,
    int AttackCooldownTicks,
    int MonsterLevel,
    string SpriteId)
{
    /// <summary>The bolt this monster casts from range; null for a melee-only monster.</summary>
    public MonsterRangedAttack? Ranged { get; init; }

    /// <summary>False for a monster that holds its ground once alerted instead of chasing.</summary>
    public bool ChasesTarget { get; init; } = true;

    /// <summary>
    /// Stands still until it notices the player instead of wandering (the
    /// donor's AmbushMode.WaitToSee).
    /// </summary>
    public bool Ambushes { get; init; }

    /// <summary>A monster that backs away from a player closer than three tiles.</summary>
    public bool KeepsDistance { get; init; }

    /// <summary>Base chance a hit makes it flinch (donor painChance).</summary>
    public float PainChance { get; init; } = 0.75f;

    /// <summary>How long a flinch holds it still: its hurt animation's length, 0 for none.</summary>
    public int HurtTicks { get; init; } = 22;

    /// <summary>
    /// Ticks from the start of its melee attack to the blow: its attack
    /// animation's time to the donor damage frame. 0 hits at once.
    /// </summary>
    public int AttackWindupTicks { get; init; }

    /// <summary>How far off it starts an attack, beyond its 0.3 body (donor attackStartDistance).</summary>
    public float AttackStartDistance { get; init; } = 0.6f;

    /// <summary>A lunge toward the player during the wind-up, in tiles per tick; 0 for none.</summary>
    public float LungeSpeed { get; init; }

    /// <summary>Ticks into the attack the lunge comes.</summary>
    public int LungeAtTicks { get; init; }

    /// <summary>How hard its landed melee blow shoves the player, in tiles per tick.</summary>
    public float AttackKnockback { get; init; } = 0.05f;
}

/// <summary>
/// A monster's ranged bolt: <c>baseDamage + roll(0..randDamage)</c> flying
/// straight at <see cref="Speed"/> tiles per tick, cast once per
/// <see cref="CooldownTicks"/> plus up to half a second of jitter while the
/// player is in sight between the two distances.
/// </summary>
public sealed record MonsterRangedAttack(
    int BaseDamage,
    int RandDamage,
    DamageType DamageType,
    float Speed,
    int CooldownTicks,
    float MinDistance,
    float MaxDistance,
    string SpriteId);
