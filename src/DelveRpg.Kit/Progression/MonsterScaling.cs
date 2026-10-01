using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Progression;

/// <summary>
/// The donor's monster levelling, kept as written. A monster spawns at
/// <c>max(floor(dungeonLevel * 1.5), baseLevel)</c>, a little higher when the
/// player has outlevelled the floor ([donor] entities/Monster.java:346-366
/// <c>Init</c>), and each level past the first adds 2 hit points and widens
/// its attack roll by 2 ([donor] entities/Actor.java:122-139 <c>initLevel</c>,
/// which raises <c>atk</c> by the level, and Monster.java:1240, which rolls
/// <c>atk + level</c>). The donor's ±1 random jitter above difficulty 4 is
/// left out, so a regenerated floor's monsters come back at the same level.
/// </summary>
public static class MonsterScaling
{
    public static int SpawnLevel(int dungeonLevel, int playerLevel, int baseLevel)
    {
        int levelDifficulty = (int)(dungeonLevel * 1.5f);
        int level = levelDifficulty;
        if (playerLevel > levelDifficulty)
        {
            level = (int)(level + ((playerLevel - levelDifficulty) * 0.3f));
        }

        return Math.Max(Math.Max(level, 1), baseLevel);
    }

    public static int MaxHitPoints(int baseHp, int level) => baseHp + (2 * (level - 1));

    /// <summary>The archetype's stats with the attack roll widened for its level.</summary>
    public static StatBlock Stats(StatBlock baseStats, int level) =>
        baseStats with { Attack = baseStats.Attack + (2 * (level - 1)) };
}
