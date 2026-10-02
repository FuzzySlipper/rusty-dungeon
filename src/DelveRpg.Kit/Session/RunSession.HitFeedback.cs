using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Effects;

namespace DelveRpg.Kit.Session;

/// <summary>
/// What a hit does besides the numbers. A struck monster is shoved and
/// briefly stunned, may flinch through its hurt animation, and at zero hit
/// points staggers for the donor's death delay before it falls and leaves a
/// corpse. A struck player is shoved by melee blows and the screen flashes
/// red ([donor] entities/Monster.java:417-436, 939-1001, 1224-1259,
/// game/Game.java:849-854).
/// </summary>
public sealed partial class RunSession
{
    /// <summary>[donor] entities/Monster.java:213 deathDelay.</summary>
    public const int DeathDelayTicks = 22;

    /// <summary>[donor] Game.flash(Colors.HURT_FLASH, 20) — ticks, despite the name.</summary>
    public const int HurtFlashTicks = 20;

    /// <summary>Most a single blow can knock back ([donor] Monster.java:948).</summary>
    private const float MaxKnockback = 0.6f;

    /// <summary>Velocity a body keeps each tick on the floor ([donor] entities/Entity.java:465-493).</summary>
    private const float FloorFriction = 0.8f;

    private readonly List<Corpse> _corpses = new();

    /// <summary>The fallen of this floor, in the order they fell.</summary>
    public IReadOnlyList<Corpse> Corpses => _corpses;

    /// <summary>
    /// Land one blow on a monster. <paramref name="pushX"/>/<paramref name="pushY"/>
    /// is the shove before knockback scaling: the swing's facing times the
    /// weapon's reach, or a projectile's velocity, as the donor passes them.
    /// </summary>
    private void HitMonster(MonsterState monster, int damage, DamageType damageType, float pushX, float pushY, float knockback)
    {
        ActorState body = monster.Body;
        body.Hp = Math.Max(0, body.Hp - damage);
        Cue(CueKind.MonsterHurt, body.X, body.Y, monster.Archetype.Id);
        ElementalEffects.ApplyOnHit(damageType, body.Effects, _random);
        monster.BrainState = MonsterBrainState.Chasing;
        monster.WasHit = true;

        // Knockback replaces the velocity and stuns for five ticks per unit
        // ([donor] Monster.java:948-952).
        float kick = Math.Min(knockback * 1.1f, MaxKnockback);
        if (kick > 0f)
        {
            monster.VelocityX = pushX * kick;
            monster.VelocityY = pushY * kick;
            monster.StunTicksRemaining = Math.Max(monster.StunTicksRemaining, (int)MathF.Ceiling(kick * 5f));
        }

        // A pain roll flinches the monster through its hurt animation and
        // spoils a blow it was winding up; a killing blow always hurts
        // ([donor] Monster.java:417-436 doPainRoll).
        float painChance = monster.Archetype.PainChance + (damage / (float)Math.Max(1, body.MaxHp) * 0.5f);
        if (!body.Alive || _random.Chance(painChance))
        {
            monster.HurtTicksRemaining = monster.Archetype.HurtTicks;
            monster.AttackWindupRemaining = 0;
        }

        if (!body.Alive && monster.DyingTicksRemaining == 0)
        {
            monster.DyingTicksRemaining = DeathDelayTicks;
        }
    }

    /// <summary>
    /// Damage the player from any source: Iron Skin halves physical damage
    /// and Resist Magic the rest (the donor's damageMod / magicDamageMod,
    /// [donor] entities/Actor.java:184-199); the hit points drop and the
    /// screen flashes.
    /// </summary>
    private void HurtPlayer(int damage, DamageType damageType = DamageType.Physical)
    {
        if (damage <= 0)
        {
            return;
        }

        CueAtPlayer(CueKind.PlayerHurt);

        EffectKind ward = damageType == DamageType.Physical ? EffectKind.IronSkin : EffectKind.MagicResist;
        if (Player.Body.Effects.IsActive(ward))
        {
            damage = Math.Max(1, damage / 2);
        }

        Player.Body.Hp = Math.Max(0, Player.Body.Hp - damage);
        Player.HurtFlashRemaining = HurtFlashTicks;
    }

    /// <summary>A melee blow that lands shoves the player away from the attacker.</summary>
    private void ShovePlayer(ActorState from, float knockback)
    {
        float dx = Player.Body.X - from.X;
        float dy = Player.Body.Y - from.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0.0001f || knockback <= 0f)
        {
            return;
        }

        _velocityX = dx / length * knockback;
        _velocityY = dy / length * knockback;
    }

    /// <summary>
    /// A monster's body this tick: knockback slides it under floor friction,
    /// stun and hurt count down, and a dying monster falls when its delay is
    /// out. Returns true when the monster may act this tick.
    /// </summary>
    private bool TickMonsterBody(MonsterState monster)
    {
        ActorState body = monster.Body;
        if (MathF.Abs(monster.VelocityX) + MathF.Abs(monster.VelocityY) > 0.0001f)
        {
            float nextX = body.X + monster.VelocityX;
            if (MonsterFits(monster, nextX, body.Y) && !CrowdsPlayer(body.X, body.Y, nextX, body.Y))
            {
                body.X = nextX;
            }
            else
            {
                monster.VelocityX = 0f;
            }

            float nextY = body.Y + monster.VelocityY;
            if (MonsterFits(monster, body.X, nextY) && !CrowdsPlayer(body.X, body.Y, body.X, nextY))
            {
                body.Y = nextY;
            }
            else
            {
                monster.VelocityY = 0f;
            }

            monster.VelocityX *= FloorFriction;
            monster.VelocityY *= FloorFriction;
        }

        monster.StunTicksRemaining = Math.Max(0, monster.StunTicksRemaining - 1);
        monster.HurtTicksRemaining = Math.Max(0, monster.HurtTicksRemaining - 1);
        if (monster.DyingTicksRemaining > 0)
        {
            monster.DyingTicksRemaining--;
            if (monster.DyingTicksRemaining == 0)
            {
                KillMonster(monster);
            }

            return false;
        }

        return monster.StunTicksRemaining == 0;
    }

    /// <summary>True when a slide ends inside the player's separation and closer than it began.</summary>
    private bool CrowdsPlayer(float fromX, float fromY, float toX, float toY)
    {
        float separation = _tuning.ActorSeparationTiles;
        float after = DistanceSquared(Player.Body.X, Player.Body.Y, toX, toY);
        return after < separation * separation
            && after < DistanceSquared(Player.Body.X, Player.Body.Y, fromX, fromY);
    }

    /// <summary>A monster body fits where its centre's tile is open; a shove never carries it into a wall.</summary>
    private bool MonsterFits(MonsterState monster, float x, float y) =>
        Level.IsWalkable(TileAt(x - MonsterRadius), TileAt(y - MonsterRadius))
        && Level.IsWalkable(TileAt(x + MonsterRadius), TileAt(y - MonsterRadius))
        && Level.IsWalkable(TileAt(x - MonsterRadius), TileAt(y + MonsterRadius))
        && Level.IsWalkable(TileAt(x + MonsterRadius), TileAt(y + MonsterRadius));
}

/// <summary>A fallen monster left on the floor; it lies until the floor is left.</summary>
public sealed record Corpse(long Id, string SpriteId, float X, float Y, long FellAtTick);
