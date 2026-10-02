using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Rules;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class CombatResolverTests
{
    private static readonly GameTuning Tuning = new();

    private static ActorState Attacker() => new(1, ActorKind.Player, 0f, 0f, 20, new StatBlock(6, 2, 4, 4, 1, 4));

    private static ActorState Defender() => new(2, ActorKind.Monster, 1f, 0f, 10, new StatBlock(2, 3, 4, 4, 0, 3));

    [Fact]
    public void A_low_draw_is_dodged_outright()
    {
        // Chance() draws 0..999999 against dodgeChance*1e6 = 150000: draw 0 dodges.
        var random = new ScriptedRandom(0);
        AttackOutcome outcome = CombatResolver.ResolveMelee(random, Attacker(), Defender(), 5, 0, Tuning);
        Assert.True(outcome.Dodged);
        Assert.Equal(0, outcome.Damage);
    }

    [Fact]
    public void Damage_is_attack_roll_minus_armor_and_never_below_one()
    {
        ActorState defender = Defender();
        // Draw 1: not a dodge (>= 150000). Draw 2: attack roll = 5 + 1 = 6.
        var random = new ScriptedRandom(999_999, 1);
        AttackOutcome outcome = CombatResolver.ResolveMelee(random, Attacker(), defender, 5, 3, Tuning);

        Assert.False(outcome.Dodged);
        Assert.Equal(3, outcome.Damage); // 6 rolled - armor class 3
        Assert.Equal(7, defender.Hp);

        // Heavy armor still chips one point: roll 6 vs armor class 50.
        ActorState wall = Defender();
        var chip = new ScriptedRandom(999_999, 1);
        AttackOutcome floored = CombatResolver.ResolveMelee(chip, Attacker(), wall, 5, 47, Tuning);
        Assert.Equal(1, floored.Damage);
    }

    [Fact]
    public void A_lethal_blow_marks_the_target_killed()
    {
        ActorState defender = new(2, ActorKind.Monster, 1f, 0f, 3, new StatBlock(2, 0, 4, 4, 0, 3));
        var random = new ScriptedRandom(999_999, 100);
        AttackOutcome outcome = CombatResolver.ResolveMelee(random, Attacker(), defender, 5, 0, Tuning);
        Assert.True(outcome.TargetKilled);
        Assert.False(defender.Alive);
    }

    [Fact]
    public void The_attack_roll_is_one_through_attack_stat_inclusive()
    {
        // Pin the upper bound: roll = weapon 5 + drawn(1..attack 6).
        var random = new ScriptedRandom(6);
        Assert.Equal(11, CombatResolver.RollAttack(random, weaponPower: 5, attackStat: 6));

        // The donor formula floors a roll of one: 5 + 1.
        var low = new ScriptedRandom(1);
        Assert.Equal(6, CombatResolver.RollAttack(low, weaponPower: 5, attackStat: 6));
    }

    [Fact]
    public void A_weapon_hit_scales_base_damage_by_charge_and_adds_the_random_part()
    {
        // Donor Weapon.doAttackRoll: (int)(base * power) + nextInt(rand + 1 + max(0, ATK - 4)).
        // Iron dagger 2+1 at full charge with attack 4: 2 + 0..1.
        Assert.Equal(2, CombatResolver.WeaponDamage(new ScriptedRandom(0), 2, 1, 4, 1f));
        Assert.Equal(3, CombatResolver.WeaponDamage(new ScriptedRandom(1), 2, 1, 4, 1f));

        // A quick tap: half charge drops the base part to (int)(2 * 0.4) = 0, floored at 1.
        Assert.Equal(1, CombatResolver.WeaponDamage(new ScriptedRandom(0), 2, 1, 4, 0.4f));

        // Attack above 4 widens the random part: attack 6 rolls 0..3.
        Assert.Equal(5, CombatResolver.WeaponDamage(new ScriptedRandom(3), 2, 1, 6, 1f));
    }

    [Fact]
    public void A_weapon_hit_lands_without_dodge_or_armor()
    {
        ActorState defender = Defender(); // defense 3 is not armor here
        AttackOutcome outcome = CombatResolver.ResolveWeaponHit(new ScriptedRandom(1), Attacker(), defender, 2, 1, 1f);
        Assert.False(outcome.Dodged);
        Assert.Equal(3, outcome.Damage);
        Assert.Equal(7, defender.Hp);
    }

    [Fact]
    public void The_player_armor_class_is_gear_plus_defense_above_four()
    {
        Assert.Equal(3, CombatResolver.ArmorClass(2, 3)); // leather armor, defense 2
        Assert.Equal(5, CombatResolver.ArmorClass(6, 3));
    }

    [Fact]
    public void Experience_for_a_kill_follows_the_donor_curve()
    {
        Assert.Equal(4, CombatResolver.ExperienceForKill(1));
        Assert.Equal(8, CombatResolver.ExperienceForKill(5));
    }
}
