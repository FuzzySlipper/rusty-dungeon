using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

/// <summary>
/// The floor's traps, triggers and pots in play. A touch trigger fires its
/// id and every trap effect with that id answers — the donor's trigger chain,
/// <c>level.trigger(id)</c> ([donor] entities/triggers/Trigger.java:222-226,
/// game/Level.java:3073-3086), kept to the chains generated floors use:
/// a pressure plate setting off its trap, a tripwire setting off a wall bolt.
/// </summary>
public sealed partial class RunSession
{
    /// <summary>How far a trap burst reaches, in tiles.</summary>
    private const float BurstRadius = 1f;

    /// <summary>A bomb's reach.</summary>
    private const float BombRadius = 1.5f;

    /// <summary>A bomb's fuse, in ticks.</summary>
    private const int BombFuseTicks = 40;

    private const int BurstShownTicks = 30;
    private const float PotRadius = 0.2f;

    private readonly List<SpikeTrap> _spikes = new();
    private readonly List<TouchTrigger> _triggers = new();
    private readonly List<TrapEffect> _trapEffects = new();
    private readonly List<Pot> _pots = new();
    private readonly List<Burst> _bursts = new();
    private readonly List<(float X, float Y, int FuseRemaining)> _bombs = new();

    public IReadOnlyList<SpikeTrap> Spikes => _spikes;

    public IReadOnlyList<TouchTrigger> Triggers => _triggers;

    public IReadOnlyList<Pot> Pots => _pots;

    /// <summary>Bursts that went off in the last half second, for the Host to draw.</summary>
    public IReadOnlyList<Burst> Bursts => _bursts;

    private void LoadFeatures(FloorFeatures features)
    {
        _spikes.Clear();
        _triggers.Clear();
        _trapEffects.Clear();
        _pots.Clear();
        _bursts.Clear();
        _bombs.Clear();
        _spikes.AddRange(features.Spikes);
        _triggers.AddRange(features.Triggers);
        _trapEffects.AddRange(features.Effects);
        _pots.AddRange(features.Pots);
    }

    private void TickFeatures()
    {
        TickSpikes();
        TickTriggers();
        TickBombs();
        _bursts.RemoveAll(burst => ElapsedTicks - burst.AtTick > BurstShownTicks);
    }

    /// <summary>
    /// The donor's proximity spikes ([donor] entities/Spikes.java:47-161): a
    /// body moving onto the tile springs them; at full extension they strike
    /// each body on the tile once.
    /// </summary>
    private void TickSpikes()
    {
        foreach (SpikeTrap spikes in _spikes)
        {
            spikes.PhaseTicks++;
            switch (spikes.Phase)
            {
                case SpikePhase.Armed:
                    spikes.PhaseTicks = 0;
                    if (SomeoneMovesOn(spikes.TileX, spikes.TileY))
                    {
                        spikes.Phase = SpikePhase.Rising;
                    }

                    break;
                case SpikePhase.Rising when spikes.PhaseTicks >= SpikeTrap.RiseTicks:
                    spikes.Phase = SpikePhase.Up;
                    spikes.PhaseTicks = 0;
                    StrikeWithSpikes(spikes);
                    break;
                case SpikePhase.Up:
                    StrikeWithSpikes(spikes);
                    if (spikes.PhaseTicks >= SpikeTrap.UpTicks)
                    {
                        spikes.Phase = SpikePhase.Sinking;
                        spikes.PhaseTicks = 0;
                    }

                    break;
                case SpikePhase.Sinking when spikes.PhaseTicks >= SpikeTrap.RiseTicks:
                    spikes.Phase = SpikePhase.Resting;
                    spikes.PhaseTicks = 0;
                    spikes.Struck.Clear();
                    break;
                case SpikePhase.Resting when spikes.PhaseTicks >= SpikeTrap.RearmTicks:
                    spikes.Phase = SpikePhase.Armed;
                    spikes.PhaseTicks = 0;
                    break;
            }
        }
    }

    private bool SomeoneMovesOn(int tileX, int tileY) =>
        (Player.Body.TileX == tileX && Player.Body.TileY == tileY && PlayerSpeed > 0.01f)
        || _monsters.Any(monster => !monster.IsDying && monster.Body.TileX == tileX && monster.Body.TileY == tileY);

    private void StrikeWithSpikes(SpikeTrap spikes)
    {
        if (Player.Body.TileX == spikes.TileX && Player.Body.TileY == spikes.TileY && spikes.Struck.Add(Player.Body.Id))
        {
            HurtPlayer(SpikeTrap.Damage);
            ShowMessage($"Spikes stab you for {SpikeTrap.Damage}.");
        }

        foreach (MonsterState monster in _monsters.ToArray())
        {
            if (!monster.IsDying && monster.Body.TileX == spikes.TileX && monster.Body.TileY == spikes.TileY
                && spikes.Struck.Add(monster.Body.Id))
            {
                HitMonster(monster, SpikeTrap.Damage, DamageType.Physical, 0f, 0f, 0f);
            }
        }
    }

    private void TickTriggers()
    {
        foreach (TouchTrigger trigger in _triggers)
        {
            trigger.ResetRemaining = Math.Max(0, trigger.ResetRemaining - 1);
            bool touched = (Player.Body.TileX == trigger.TileX && Player.Body.TileY == trigger.TileY)
                || (trigger.IsPlate && _monsters.Any(monster => !monster.IsDying
                    && monster.Body.TileX == trigger.TileX && monster.Body.TileY == trigger.TileY));
            if (!touched)
            {
                trigger.Pressed = false;
                continue;
            }

            if (trigger.Pressed || trigger.ResetRemaining > 0)
            {
                continue;
            }

            trigger.Pressed = true;
            trigger.ResetRemaining = trigger.ResetTicks;
            if (trigger.IsPlate)
            {
                ShowMessage("Click.");
            }

            FireTrigger(trigger.TargetId);
        }
    }

    /// <summary>Set off every trap effect answering a trigger id.</summary>
    private void FireTrigger(string id)
    {
        foreach (TrapEffect effect in _trapEffects.Where(effect => effect.TriggerId == id).ToList())
        {
            switch (effect.Kind)
            {
                case TrapEffectKind.FireBurst:
                    Explode(effect.X, effect.Y, BurstRadius, 6 + (int)(Level.DifficultyLevel * 0.5f), DamageType.Fire);
                    break;
                case TrapEffectKind.PoisonBurst:
                    Explode(effect.X, effect.Y, BurstRadius, 2, DamageType.Poison);
                    break;
                case TrapEffectKind.Teleport:
                    if (Player.Body.TileX == TileAt(effect.X) && Player.Body.TileY == TileAt(effect.Y))
                    {
                        (int x, int y) = FarOpenSpot();
                        Player.Body.X = x + 0.5f;
                        Player.Body.Y = y + 0.5f;
                        _velocityX = 0f;
                        _velocityY = 0f;
                        _bursts.Add(new Burst(_nextActorId++, effect.X, effect.Y, DamageType.Magic, ElapsedTicks));
                        ShowMessage("The floor gives way to elsewhere.");
                    }

                    break;
                case TrapEffectKind.WallBolt:
                    ShootFromWall(effect.X, effect.Y);
                    break;
            }
        }
    }

    /// <summary>
    /// A wall bolt: the donor Magic Missile Trap's spawner casts the plain
    /// magic missile (2 + roll(0..2) magic) straight at the player.
    /// </summary>
    private void ShootFromWall(float x, float y)
    {
        float fromZ = _tuning.EyeHeight;
        float dx = Player.Body.X - x;
        float dy = Player.Body.Y - y;
        float dz = (_tuning.EyeHeight * 0.8f) - fromZ;
        float length = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        if (length <= 0.0001f)
        {
            return;
        }

        const float speed = 0.17f; // [donor] spells/MagicMissile.java speed
        _projectiles.Add(new Projectile(_nextActorId++, fromPlayer: false, x, y, fromZ, dx / length * speed, dy / length * speed, dz / length * speed)
        {
            Floating = true,
            Damage = 2 + _random.Next(0, 3),
            DamageType = DamageType.Magic,
            SpriteId = "projectile.eyebolt",
        });
        ShowMessage("A bolt flies from the wall!");
    }

    /// <summary>
    /// A burst: every body and pot within the radius takes the damage
    /// (elemental effects follow), and it is shown for half a second.
    /// </summary>
    private void Explode(float x, float y, float radius, int damage, DamageType damageType)
    {
        _bursts.Add(new Burst(_nextActorId++, x, y, damageType, ElapsedTicks));
        if (damage <= 0)
        {
            return;
        }

        if (DistanceSquared(x, y, Player.Body.X, Player.Body.Y) <= radius * radius)
        {
            HurtPlayer(damage, damageType);
            ElementalEffects.ApplyOnHit(damageType, Player.Body.Effects, _random);
            ShowMessage($"The blast hits you for {damage}.");
        }

        foreach (MonsterState monster in _monsters.ToArray())
        {
            if (!monster.IsDying && DistanceSquared(x, y, monster.Body.X, monster.Body.Y) <= radius * radius)
            {
                HitMonster(monster, damage, damageType, 0f, 0f, 0f);
            }
        }

        foreach (Pot pot in _pots.ToArray())
        {
            if (DistanceSquared(x, y, pot.X, pot.Y) <= radius * radius)
            {
                DamagePot(pot, damage);
            }
        }
    }

    private void TickBombs()
    {
        for (int i = _bombs.Count - 1; i >= 0; i--)
        {
            (float x, float y, int fuse) = _bombs[i];
            if (fuse > 1)
            {
                _bombs[i] = (x, y, fuse - 1);
                continue;
            }

            _bombs.RemoveAt(i);
            Explode(x, y, BombRadius, 6, DamageType.Fire);
        }
    }

    /// <summary>
    /// Break a pot a little more ([donor] entities/Breakable.java:152, 176-234).
    /// A broken plain pot holds a surprise half the time — the donor draws a
    /// random surprise from its theme's list of bombs and monster spawners, so
    /// here it is a monster for this floor or a lit bomb, even odds. The
    /// exploding pot bursts and shoves what stands near.
    /// </summary>
    private void DamagePot(Pot pot, int damage)
    {
        pot.Hp -= Math.Max(1, damage);
        if (pot.Hp > 0)
        {
            return;
        }

        _pots.Remove(pot);
        if (pot.Kind == PotKind.Exploding)
        {
            Explode(pot.X, pot.Y, 3f, 0, DamageType.Magic);
            ShoveFrom(pot.X, pot.Y, 3f, 0.15f);
            ShowMessage("The pot bursts!");
            return;
        }

        ShowMessage("The pot shatters.");
        if (!_random.Chance(0.5))
        {
            return;
        }

        IReadOnlyList<string> eligible = _rules.MonstersForFloor(Level.DifficultyLevel);
        if (_random.Next(0, 2) == 0 && eligible.Count > 0)
        {
            AddMonster(eligible[_random.Next(0, eligible.Count)], pot.TileX, pot.TileY);
            ShowMessage("Something was hiding in the pot!");
        }
        else
        {
            _bombs.Add((pot.X, pot.Y, BombFuseTicks));
            ShowMessage("Something hisses in the shards!");
        }
    }

    /// <summary>The exploding pot's impulse: bodies within reach are thrown back, harder the closer.</summary>
    private void ShoveFrom(float x, float y, float radius, float strength)
    {
        float playerDistance = MathF.Sqrt(DistanceSquared(x, y, Player.Body.X, Player.Body.Y));
        if (playerDistance < radius && playerDistance > 0.0001f)
        {
            float push = strength * (1f - (playerDistance / radius));
            _velocityX += (Player.Body.X - x) / playerDistance * push;
            _velocityY += (Player.Body.Y - y) / playerDistance * push;
        }

        foreach (MonsterState monster in _monsters)
        {
            float distance = MathF.Sqrt(DistanceSquared(x, y, monster.Body.X, monster.Body.Y));
            if (distance < radius && distance > 0.0001f)
            {
                float push = strength * (1f - (distance / radius));
                monster.VelocityX += (monster.Body.X - x) / distance * push;
                monster.VelocityY += (monster.Body.Y - y) / distance * push;
            }
        }
    }

    /// <summary>The nearest unbroken pot in the swing's reach and cone, as for monsters.</summary>
    private Pot? NearestPotInReach(float reach)
    {
        ActorState body = Player.Body;
        float facingX = MathF.Sin(body.Facing);
        float facingY = -MathF.Cos(body.Facing);
        Pot? best = null;
        float bestDistance = reach;
        foreach (Pot pot in _pots)
        {
            float dx = pot.X - body.X;
            float dy = pot.Y - body.Y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance > bestDistance || ((dx * facingX) + (dy * facingY)) / Math.Max(0.0001f, distance) < 0.5f)
            {
                continue;
            }

            best = pot;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>True when a step ends inside a pot and closer than it began; pots are solid.</summary>
    private bool CrowdsPot(float fromX, float fromY, float toX, float toY)
    {
        float reach = PotRadius + BodyRadius;
        foreach (Pot pot in _pots)
        {
            float after = DistanceSquared(pot.X, pot.Y, toX, toY);
            if (after < reach * reach && after < DistanceSquared(pot.X, pot.Y, fromX, fromY))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A projectile meeting a pot breaks it a little and is spent.</summary>
    private bool StrikePot(Projectile projectile)
    {
        if (projectile.Z > 0.5f)
        {
            return false;
        }

        float reach = PotRadius + ProjectileRadius;
        foreach (Pot pot in _pots)
        {
            if (DistanceSquared(projectile.X, projectile.Y, pot.X, pot.Y) <= reach * reach)
            {
                DamagePot(pot, projectile.Damage);
                if (projectile.AmmoItemId is string ammoId)
                {
                    DropItem(new Inventory.ItemInstance(ammoId, 1), pot.TileX, pot.TileY);
                }

                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<SnapshotFeature> CaptureFeatures() =>
        _spikes.Select(spikes => new SnapshotFeature("spikes", spikes.TileX + 0.5f, spikes.TileY + 0.5f))
            .Concat(_triggers.Select(trigger => new SnapshotFeature(
                "trigger", trigger.TileX + 0.5f, trigger.TileY + 0.5f, trigger.TargetId, IsPlate: trigger.IsPlate, ResetTicks: trigger.ResetTicks)))
            .Concat(_trapEffects.Select(effect => new SnapshotFeature("effect", effect.X, effect.Y, effect.TriggerId, (int)effect.Kind)))
            .Concat(_pots.Select(pot => new SnapshotFeature("pot", pot.X, pot.Y, Variant: (int)pot.Kind, Hp: pot.Hp)))
            .ToList();

    private FloorFeatures RestoreFeatures(IReadOnlyList<SnapshotFeature>? saved)
    {
        var spikes = new List<SpikeTrap>();
        var triggers = new List<TouchTrigger>();
        var effects = new List<TrapEffect>();
        var pots = new List<Pot>();
        foreach (SnapshotFeature feature in saved ?? [])
        {
            switch (feature.Kind)
            {
                case "spikes":
                    spikes.Add(new SpikeTrap(_nextActorId++, TileAt(feature.X), TileAt(feature.Y)));
                    break;
                case "trigger" when feature.TriggerId is string id:
                    triggers.Add(new TouchTrigger(_nextActorId++, TileAt(feature.X), TileAt(feature.Y), id, feature.IsPlate, feature.ResetTicks));
                    break;
                case "effect" when feature.TriggerId is string id && Enum.IsDefined((TrapEffectKind)feature.Variant):
                    effects.Add(new TrapEffect(id, (TrapEffectKind)feature.Variant, feature.X, feature.Y));
                    break;
                case "pot" when Enum.IsDefined((PotKind)feature.Variant):
                    pots.Add(new Pot(_nextActorId++, (PotKind)feature.Variant, feature.X, feature.Y) { Hp = Math.Max(1, feature.Hp) });
                    break;
            }
        }

        return new FloorFeatures(spikes, triggers, effects, pots);
    }
}

/// <summary>Serializable shape of one trap, trigger, effect or pot at a save boundary; timers are not kept.</summary>
public sealed record SnapshotFeature(
    string Kind,
    float X,
    float Y,
    string? TriggerId = null,
    int Variant = 0,
    int Hp = 0,
    bool IsPlate = false,
    int ResetTicks = 0);
