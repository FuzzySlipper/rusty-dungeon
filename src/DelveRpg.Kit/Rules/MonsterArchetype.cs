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
    bool IsRanged,
    int DetectRange,
    string SpriteId);
