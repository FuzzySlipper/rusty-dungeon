using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

/// <summary>
/// Telegraphed melee. An alerted monster close enough starts its attack:
/// it stands still and winds up for the ticks its attack animation takes to
/// reach the donor's damage frame, may lunge partway through, and the blow
/// lands only if the player is still within reach when that frame comes, so
/// stepping back dodges it ([donor] entities/Monster.java:735-762,
/// 1093-1132 attack, 1224-1259 tryDamageHit, gfx/animation/DamageAction.java).
/// </summary>
public sealed partial class RunSession
{
    /// <summary>[donor] Monster.java:56 reach.</summary>
    private const float MonsterReach = 0.6f;

    /// <summary>The donor monster's collision half-width, added to reach in both checks.</summary>
    private const float MonsterCollision = 0.3f;

    /// <summary>[donor] Monster.java attack(): attacktimer = attackTime + rand(10).</summary>
    private const int AttackTimeJitter = 10;

    /// <summary>
    /// Advance a monster's melee for one tick. True while the attack keeps
    /// the monster busy (winding up), so it neither moves nor casts.
    /// </summary>
    private bool TickMeleeAttack(MonsterState monster, float distance, bool seesPlayer)
    {
        if (monster.AttackElapsedTicks < int.MaxValue)
        {
            monster.AttackElapsedTicks++;
        }

        MonsterArchetype archetype = monster.Archetype;
        if (monster.AttackWindupRemaining > 0)
        {
            if (archetype.LungeSpeed > 0f && monster.AttackElapsedTicks == archetype.LungeAtTicks)
            {
                Lunge(monster, archetype.LungeSpeed);
            }

            monster.AttackWindupRemaining--;
            if (monster.AttackWindupRemaining == 0)
            {
                LandMonsterBlow(monster);
            }

            return true;
        }

        float startDistance = MonsterCollision + Math.Max(MonsterReach, archetype.AttackStartDistance);
        if (monster.BrainState == MonsterBrainState.Idle
            || monster.Body.AttackCooldownRemaining > 0
            || !seesPlayer
            || distance >= startDistance)
        {
            return false;
        }

        ActorState body = monster.Body;
        body.Facing = MathF.Atan2(Player.Body.X - body.X, -(Player.Body.Y - body.Y));
        body.AttackCooldownRemaining = archetype.AttackCooldownTicks + _random.Next(0, AttackTimeJitter);
        monster.AttackElapsedTicks = 0;
        if (archetype.AttackWindupTicks <= 0)
        {
            // No attack animation: the blow is immediate (the donor GHOST).
            LandMonsterBlow(monster);
            return false;
        }

        monster.AttackWindupRemaining = archetype.AttackWindupTicks;
        return true;
    }

    /// <summary>The damage frame: the blow lands if the player is still in reach and in sight.</summary>
    private void LandMonsterBlow(MonsterState monster)
    {
        ActorState body = monster.Body;
        if (Distance(body, Player.Body) > MonsterReach + MonsterCollision
            || !LineOfSight.CanSee(Level, body.TileX, body.TileY, Player.Body.TileX, Player.Body.TileY))
        {
            ShowMessage($"The {monster.Archetype.DisplayName}'s blow falls short.");
            return;
        }

        int armorClass = CombatResolver.ArmorClass(Player.Body.Stats.Defense, GearArmorClass());
        int hpBefore = Player.Body.Hp;
        AttackOutcome outcome = CombatResolver.ResolveMelee(
            _random, body, Player.Body, monster.Archetype.AttackPower, armorClass, _tuning);
        if (outcome.Dodged)
        {
            ShowMessage($"The {monster.Archetype.DisplayName} misses you.");
            return;
        }

        Player.Body.Hp = hpBefore;
        HurtPlayer(outcome.Damage);
        ShovePlayer(body, monster.Archetype.AttackKnockback);
        ShowMessage($"The {monster.Archetype.DisplayName} hits you for {outcome.Damage}.");
    }

    /// <summary>A lunge adds a burst of velocity toward the player ([donor] gfx/animation/ImpulseAction.java).</summary>
    private void Lunge(MonsterState monster, float speed)
    {
        float dx = Player.Body.X - monster.Body.X;
        float dy = Player.Body.Y - monster.Body.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0.0001f)
        {
            return;
        }

        monster.VelocityX += dx / length * speed;
        monster.VelocityY += dy / length * speed;
    }
}
