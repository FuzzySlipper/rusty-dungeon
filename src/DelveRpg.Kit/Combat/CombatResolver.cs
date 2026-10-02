using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Combat;

/// <summary>What one attack did.</summary>
public sealed record AttackOutcome(bool Dodged, int Damage, bool TargetKilled);

/// <summary>
/// Physical attack resolution, in the donor's two shapes. A monster's blow
/// is <c>Actor.damageRoll</c>: a flat dodge, then <c>roll(1..attack)</c> minus
/// the defender's armor class, never below 1 ([donor] entities/Actor.java:146-168).
/// A player's weapon hit is <c>Weapon.doAttackRoll</c>, scaled by how far the
/// swing was charged and landed straight on the monster with no dodge or
/// armor ([donor] entities/items/Weapon.java:93-104, entities/Monster.java:939-957).
/// </summary>
public static class CombatResolver
{
    /// <summary>Attack magnitude rolled for one blow: weapon power plus 1..attack stat.</summary>
    public static int RollAttack(IRandomSource random, int weaponPower, int attackStat) =>
        weaponPower + random.Next(1, Math.Max(1, attackStat) + 1);

    /// <summary>
    /// The player's armor class: worn gear plus a point per defense stat above
    /// 4 ([donor] entities/Player.java:2179-2208 GetArmorClass).
    /// </summary>
    public static int ArmorClass(int defenseStat, int gearArmorClass) =>
        gearArmorClass + Math.Max(0, defenseStat - 4);

    /// <summary>A monster's blow against an armored defender.</summary>
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
        int damage = Math.Max(1, attack - defenderArmorClass);
        defender.Hp = Math.Max(0, defender.Hp - damage);
        return new AttackOutcome(false, damage, !defender.Alive);
    }

    /// <summary>
    /// Damage of one weapon swing: <c>(int)(baseDamage * attackPower)</c> plus
    /// <c>0..randDamage + max(0, attack - 4)</c>, at least 1. Attack power is
    /// the charged fraction of the swing, 0..1.
    /// </summary>
    public static int WeaponDamage(IRandomSource random, int baseDamage, int randDamage, int attackStat, float attackPower)
    {
        int damage = (int)(baseDamage * Math.Clamp(attackPower, 0f, 1f));
        damage += random.Next(0, randDamage + Math.Max(0, attackStat - 4) + 1);
        return Math.Max(damage, 1);
    }

    /// <summary>A player's weapon hit: the rolled damage lands in full.</summary>
    public static AttackOutcome ResolveWeaponHit(
        IRandomSource random,
        ActorState attacker,
        ActorState defender,
        int baseDamage,
        int randDamage,
        float attackPower)
    {
        int damage = WeaponDamage(random, baseDamage, randDamage, attacker.Stats.Attack, attackPower);
        defender.Hp = Math.Max(0, defender.Hp - damage);
        return new AttackOutcome(false, damage, !defender.Alive);
    }

    /// <summary>Experience awarded for one kill.</summary>
    public static int ExperienceForKill(int monsterLevel) => 3 + monsterLevel;
}
