using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Combat;

/// <summary>What one attack did.</summary>
public sealed record AttackOutcome(bool Dodged, int Damage, bool TargetKilled);

/// <summary>
/// Physical attack resolution. The donor rolls a flat dodge first, then
/// <c>roll(1..attack) − armor class</c> with a floor of 1; this port keeps that
/// shape and folds weapon power and the attacker's attack stat into the rolled
/// attack magnitude.
/// </summary>
public static class CombatResolver
{
    /// <summary>Attack magnitude rolled for one swing: weapon power plus 1..attack stat.</summary>
    public static int RollAttack(IRandomSource random, int weaponPower, int attackStat) =>
        weaponPower + random.Next(1, Math.Max(1, attackStat) + 1);

    /// <summary>Armor class of a defender: defense stat plus worn gear.</summary>
    public static int ArmorClass(int defenseStat, int gearArmorClass) => defenseStat + gearArmorClass;

    public static AttackOutcome ResolveMelee(
        IRandomSource random,
        ActorState attacker,
        ActorState defender,
        int weaponPower,
        int defenderArmorClass,
        GameTuning tuning)
    {
        if (random.Chance(tuning.DodgeChance))
        {
            return new AttackOutcome(true, 0, false);
        }

        int attack = RollAttack(random, weaponPower, attacker.Stats.Attack);
        int damage = Math.Max(1, attack - ArmorClass(defender.Stats.Defense, defenderArmorClass));
        defender.Hp = Math.Max(0, defender.Hp - damage);
        return new AttackOutcome(false, damage, !defender.Alive);
    }

    /// <summary>Experience awarded for one kill.</summary>
    public static int ExperienceForKill(int monsterLevel) => 3 + monsterLevel;
}
