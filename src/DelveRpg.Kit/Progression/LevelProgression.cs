using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Progression;

/// <summary>
/// Character growth formulas. The donor's numbers are kept where the survey
/// verified them: the experience curve, the endurance-driven hit point
/// maximum, and the full heal on level up.
/// </summary>
public static class LevelProgression
{
    /// <summary>Total experience needed to reach the next character level.</summary>
    public static int ExperienceToNext(int level) => level * 4 * (level * 2);

    /// <summary>
    /// Maximum hit points for one level and endurance.
    /// Donor: <c>maxHp = (int)(END * (END / 3f)) + 4; maxHp += (level - 1) * 0.5f</c>
    /// (LevelUpOverlay.applyStats); the float division and truncation are kept.
    /// </summary>
    public static int MaxHitPoints(StatBlock stats, int level)
    {
        int baseHp = (int)(stats.Endurance * (stats.Endurance / 3f)) + 4;
        return baseHp + (int)((level - 1) * 0.5f);
    }

    /// <summary>
    /// Advance one level: the donor heals to full and raises the maximum with
    /// the new level. The caller grants the stat choice.
    /// </summary>
    public static void ApplyLevelUp(PlayerState player)
    {
        player.Level++;
        player.Body.MaxHp = MaxHitPoints(player.Body.Stats, player.Level);
        player.Body.Hp = player.Body.MaxHp;
    }

    /// <summary>Three distinct stat offers for a level-up choice.</summary>
    public static IReadOnlyList<string> RollOffers(Random.IRandomSource random)
    {
        string[] pool = ["attack", "defense", "dexterity", "speed", "magic", "endurance"];
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return Array.AsReadOnly(pool.Take(3).ToArray());
    }

    public static void ApplyStatChoice(PlayerState player, string stat)
    {
        StatBlock stats = player.Body.Stats;
        player.Body.Stats = stat switch
        {
            "attack" => stats with { Attack = stats.Attack + 1 },
            "defense" => stats with { Defense = stats.Defense + 1 },
            "dexterity" => stats with { Dexterity = stats.Dexterity + 1 },
            "speed" => stats with { Speed = stats.Speed + 1 },
            "magic" => stats with { Magic = stats.Magic + 1 },
            "endurance" => stats with { Endurance = stats.Endurance + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown stat offer."),
        };
    }
}
