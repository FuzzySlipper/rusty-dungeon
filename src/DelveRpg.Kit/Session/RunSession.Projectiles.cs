using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Session;

/// <summary>
/// Arrows and bolts. A bow looses one arrow from the pack at the charged
/// fraction of its range; a wand spends a charge per bolt; a ranged monster
/// casts at the player. Flight is swept over the tile grid in short steps:
/// walls and closed doors stop a projectile, as do the floor and ceiling,
/// and the first body in its path takes the hit. Projectile hits skip dodge
/// and armor, as the donor's Projectile.encroached calls hit/takeDamage
/// directly ([donor] entities/projectiles/Projectile.java:188-213,
/// entities/Player.java:2066-2070).
/// </summary>
public sealed partial class RunSession
{
    /// <summary>Longest stride of one sweep step, in tiles (the donor sweeps by its collision box).</summary>
    private const float ProjectileStep = 0.1f;
    private const float ProjectileRadius = 0.05f;
    private const float MonsterRadius = 0.3f;
    private const float BodyHeight = 0.8f;
    private const float CeilingHeight = 1f;
    private const int ProjectileLifetimeTicks = 600;
    private const float LaunchOffsetTiles = 0.3f;

    /// <summary>[donor] entities/projectiles/Missile.java breakChance.</summary>
    private const double ArrowBreakChance = 0.1;

    private readonly List<Projectile> _projectiles = new();

    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    /// <summary>
    /// Loose an arrow: the pack's first arrows are spent, the damage is the
    /// bow's weapon roll, and the arrow leaves at <c>attackPower × range / 8</c>
    /// tiles per tick along the look direction ([donor] entities/items/Bow.java:45-68).
    /// </summary>
    private void ShootBow(ItemArchetype bow, float attackPower)
    {
        int ammoSlot = FindAmmoSlot();
        if (ammoSlot < 0)
        {
            ShowMessage("You have no arrows.");
            return;
        }

        string ammoId = Player.Inventory.Slot(ammoSlot)!.Value.ArchetypeId;
        ItemArchetype ammo = _rules.Item(ammoId)!;
        float knockback = bow.Knockback * attackPower;
        Player.Inventory.TryConsumeOne(ammoSlot);
        int damage = Math.Max(1, CombatResolver.WeaponDamage(
            _random, bow.Power, bow.RandDamage, Player.Body.Stats.Attack, attackPower));
        LaunchFromPlayer(
            attackPower * bow.Range / 8f,
            0f,
            floating: false,
            damage,
            bow.DamageType,
            ammo.ProjectileSpriteId.Length > 0 ? ammo.ProjectileSpriteId : ammo.SpriteId,
            ammoId,
            knockback);
    }

    /// <summary>
    /// Zap a wand: one charge per bolt; the bolt rolls
    /// <c>power + roll(0..randDamage + max(0, MAG − 4))</c> whatever the charge
    /// and flies straight, scattered by the wand's accuracy
    /// ([donor] entities/items/Wand.java:82-125, entities/spells/MagicMissile.java:49-61,
    /// spells/Spell.java:115-122).
    /// </summary>
    private void ZapWand(ItemArchetype wand)
    {
        int slot = Player.WieldedSlot >= 0 && Player.Inventory.Slot(Player.WieldedSlot)?.ArchetypeId == wand.Id
            ? Player.WieldedSlot
            : Player.Inventory.Find(wand.Id);
        if (slot < 0 || !Player.Inventory.TryUseCharge(slot))
        {
            ShowMessage($"The {wand.DisplayName} fizzles; it has no charges left.");
            return;
        }

        int randDamage = wand.RandDamage + Math.Max(0, Player.Body.Stats.Magic - 4);
        int damage = wand.Power + _random.Next(0, randDamage + 1);
        float scatter = (1f - Math.Clamp(wand.Accuracy, 0f, 1f)) * 45f;
        float spread = scatter <= 0f ? 0f : _random.Next(0, 1000) / 1000f * scatter;
        LaunchFromPlayer(
            wand.ProjectileSpeed,
            spread,
            floating: true,
            Math.Max(1, damage),
            wand.DamageType,
            wand.ProjectileSpriteId,
            ammoItemId: null,
            wand.Knockback);
    }

    private void LaunchFromPlayer(
        float speed,
        float scatterDegrees,
        bool floating,
        int damage,
        DamageType damageType,
        string spriteId,
        string? ammoItemId,
        float knockback)
    {
        ActorState body = Player.Body;
        float yaw = body.Facing;
        float pitch = DegreesToRadians(body.PitchDegrees);
        if (scatterDegrees > 0f)
        {
            // Tip the aim off-line by the scatter, in a random direction around it.
            float around = DegreesToRadians(_random.Next(0, 360));
            yaw += DegreesToRadians(scatterDegrees) * MathF.Cos(around);
            pitch += DegreesToRadians(scatterDegrees) * MathF.Sin(around);
        }

        // Shots leave from the hand, a little ahead of the body, unless that
        // is already inside a wall.
        float startX = body.X + (MathF.Sin(yaw) * LaunchOffsetTiles);
        float startY = body.Y - (MathF.Cos(yaw) * LaunchOffsetTiles);
        if (Level.BlocksSight(TileAt(startX), TileAt(startY)))
        {
            (startX, startY) = (body.X, body.Y);
        }

        float flat = MathF.Cos(pitch) * speed;
        _projectiles.Add(new Projectile(
            _nextActorId++,
            fromPlayer: true,
            startX,
            startY,
            _tuning.EyeHeight - 0.1f,
            MathF.Sin(yaw) * flat,
            -MathF.Cos(yaw) * flat,
            MathF.Sin(pitch) * speed)
        {
            Floating = floating,
            Damage = damage,
            DamageType = damageType,
            SpriteId = spriteId,
            AmmoItemId = ammoItemId,
            Knockback = knockback,
        });
    }

    /// <summary>
    /// A ranged monster's cast: a straight bolt from its middle at the
    /// player's ([donor] entities/Monster.java:820-850 spell cast,
    /// entities/spells/MagicMissile.java:49-61 doCast).
    /// </summary>
    private void CastAtPlayer(MonsterState monster, MonsterRangedAttack ranged)
    {
        ActorState from = monster.Body;
        ActorState to = Player.Body;
        float fromZ = BodyHeight * 0.6f;
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float dz = (_tuning.EyeHeight * 0.8f) - fromZ;
        float length = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        if (length <= 0.0001f)
        {
            return;
        }

        from.Facing = MathF.Atan2(dx, -dy);
        int damage = ranged.BaseDamage + _random.Next(0, ranged.RandDamage + 1);
        float scale = ranged.Speed / length;
        _projectiles.Add(new Projectile(
            _nextActorId++,
            fromPlayer: false,
            from.X,
            from.Y,
            fromZ,
            dx * scale,
            dy * scale,
            dz * scale)
        {
            Floating = true,
            Damage = Math.Max(1, damage),
            DamageType = ranged.DamageType,
            SpriteId = ranged.SpriteId,
        });
    }

    private int FindAmmoSlot()
    {
        for (int i = 0; i < Player.Inventory.Capacity; i++)
        {
            if (Player.Inventory.Slot(i) is ItemInstance item && _rules.Item(item.ArchetypeId)?.Kind == ItemKind.Ammo)
            {
                return i;
            }
        }

        return -1;
    }

    private void TickProjectiles()
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = _projectiles[i];
            projectile.AgeTicks++;
            if (projectile.AgeTicks > ProjectileLifetimeTicks || FlyOneTick(projectile))
            {
                _projectiles.RemoveAt(i);
            }
        }
    }

    /// <summary>Move one projectile a tick; true when it has struck something and is gone.</summary>
    private bool FlyOneTick(Projectile projectile)
    {
        if (!projectile.Floating)
        {
            projectile.VelocityZ -= Projectile.Gravity;
        }

        float stride = MathF.Max(
            MathF.Abs(projectile.VelocityZ),
            MathF.Max(MathF.Abs(projectile.VelocityX), MathF.Abs(projectile.VelocityY)));
        int steps = Math.Max(1, (int)MathF.Ceiling(stride / ProjectileStep));
        for (int step = 0; step < steps; step++)
        {
            float nextX = projectile.X + (projectile.VelocityX / steps);
            float nextY = projectile.Y + (projectile.VelocityY / steps);
            float nextZ = projectile.Z + (projectile.VelocityZ / steps);

            if (Level.BlocksSight(TileAt(nextX), TileAt(nextY))
                || nextZ <= 0f
                || nextZ >= CeilingHeight)
            {
                LandProjectile(projectile);
                return true;
            }

            projectile.X = nextX;
            projectile.Y = nextY;
            projectile.Z = nextZ;
            if (StrikeBody(projectile) || StrikePot(projectile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hit the first body the projectile overlaps; true when it struck one.</summary>
    private bool StrikeBody(Projectile projectile)
    {
        if (projectile.Z > BodyHeight)
        {
            return false;
        }

        if (!projectile.FromPlayer)
        {
            ActorState player = Player.Body;
            float reach = BodyRadius + ProjectileRadius;
            if (DistanceSquared(projectile.X, projectile.Y, player.X, player.Y) > reach * reach)
            {
                return false;
            }

            HurtPlayer(projectile.Damage);
            ElementalEffects.ApplyOnHit(projectile.DamageType, player.Effects, _random);
            ShowMessage($"A bolt hits you for {projectile.Damage}.");
            return true;
        }

        foreach (MonsterState monster in _monsters)
        {
            if (monster.IsDying)
            {
                continue;
            }

            float reach = MonsterRadius + ProjectileRadius;
            if (DistanceSquared(projectile.X, projectile.Y, monster.Body.X, monster.Body.Y) > reach * reach)
            {
                continue;
            }

            // The donor shoves with the projectile's own velocity
            // ([donor] projectiles/Projectile.java:201-213 hit(xa, ya, ...)).
            HitMonster(
                monster,
                projectile.Damage,
                projectile.DamageType,
                projectile.VelocityX,
                projectile.VelocityY,
                projectile.Knockback);
            ShowMessage($"You hit the {monster.Archetype.DisplayName} for {projectile.Damage}.");

            // An arrow that strikes home is carried until the monster falls
            // ([donor] entities/projectiles/Missile.java addArrowLootToMonster).
            if (projectile.AmmoItemId is string ammoId)
            {
                monster.Carried.Add(new ItemInstance(ammoId, 1));
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// A projectile struck the world. A bolt is spent; an arrow breaks one
    /// time in ten and otherwise lies where it stopped, to be picked up again
    /// ([donor] entities/projectiles/Missile.java maybeBreak, pickup).
    /// </summary>
    private void LandProjectile(Projectile projectile)
    {
        if (projectile.AmmoItemId is not string ammoId || _random.Chance(ArrowBreakChance))
        {
            return;
        }

        DropItem(new ItemInstance(ammoId, 1), TileAt(projectile.X), TileAt(projectile.Y));
    }

    /// <summary>
    /// Put an item on the floor, joining a like stack already on that tile.
    /// A tile the player stands on is collected on the next step onto it,
    /// so a drop there is free to pick up with use.
    /// </summary>
    private void DropItem(ItemInstance item, int tileX, int tileY)
    {
        if (_rules.IsStackable(item.ArchetypeId))
        {
            for (int i = 0; i < _groundItems.Count; i++)
            {
                GroundItem lying = _groundItems[i];
                if (lying.X == tileX && lying.Y == tileY && lying.Item.ArchetypeId == item.ArchetypeId)
                {
                    _groundItems[i] = lying with { Item = lying.Item with { Count = lying.Item.Count + item.Count } };
                    return;
                }
            }
        }

        _groundItems.Add(new GroundItem(_nextActorId++, item, tileX, tileY));
    }
}
