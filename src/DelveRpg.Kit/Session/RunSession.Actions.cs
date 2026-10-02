using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Ai;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

public sealed partial class RunSession
{
    private void TickCombat(RunInput input)
    {
        ActorState body = Player.Body;
        ItemArchetype? weapon = Player.Equipment.WeaponItemId is string weaponId ? _rules.Item(weaponId) : null;
        int chargeTicks = weapon?.ChargeTicks ?? _tuning.AttackChargeTicks;

        // The donor's swing: holding winds the charge up to full and holds
        // it there; releasing swings at the charged fraction of full power
        // ([donor] entities/Player.java:1221-1258, 1588-1592).
        if (input.AttackHeld)
        {
            if (body.AttackCooldownRemaining == 0)
            {
                Player.AttackCharge = Math.Min(chargeTicks, Player.AttackCharge + 1);
            }
        }
        else if (Player.AttackCharge > 0)
        {
            SwingWeapon(weapon, Player.AttackCharge / (float)Math.Max(1, chargeTicks));
            Player.AttackCharge = 0;
            body.AttackCooldownRemaining = _tuning.AttackCooldownTicks;
        }
    }

    private void SwingWeapon(ItemArchetype? weapon, float attackPower)
    {
        MonsterState? target = NearestTargetInReach(ReachTiles);
        if (target is null)
        {
            ShowMessage("Your swing finds only air.");
            return;
        }

        // Bare hands hit for a point; the donor has no unarmed swing at all.
        AttackOutcome outcome = CombatResolver.ResolveWeaponHit(
            _random, Player.Body, target.Body, weapon?.Power ?? 1, weapon?.RandDamage ?? 0, attackPower);
        ShowMessage($"You hit the {target.Archetype.DisplayName} for {outcome.Damage}.");
        target.BrainState = MonsterBrainState.Chasing;
        if (outcome.TargetKilled)
        {
            KillMonster(target);
        }
    }

    private void KillMonster(MonsterState monster)
    {
        _monsters.Remove(monster);
        // The donor awards 3 + its zero-based level field, which initLevel sets
        // to the spawn level minus one ([donor] DelverGameMode.java:123).
        int experience = CombatResolver.ExperienceForKill(monster.Level - 1);
        Player.Experience += experience;
        ShowMessage($"The {monster.Archetype.DisplayName} falls. (+{experience} xp)");

        if (_random.Chance(_tuning.EnchantChance) && _rules.RollLootId(_random, Level.DifficultyLevel) is string lootId)
        {
            AddGroundItem(lootId, monster.Body.TileX, monster.Body.TileY);
        }
    }

    private MonsterState? NearestTargetInReach(float reach)
    {
        ActorState body = Player.Body;
        MonsterState? best = null;
        float bestDistance = reach;
        foreach (MonsterState monster in _monsters)
        {
            float dx = monster.Body.X - body.X;
            float dy = monster.Body.Y - body.Y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance > bestDistance)
            {
                continue;
            }

            float facingX = MathF.Sin(body.Facing);
            float facingY = -MathF.Cos(body.Facing);
            float dot = ((dx * facingX) + (dy * facingY)) / Math.Max(0.0001f, distance);
            if (dot < 0.5f)
            {
                continue;
            }

            if (!LineOfSight.CanSee(Level, body.TileX, body.TileY, monster.Body.TileX, monster.Body.TileY))
            {
                continue;
            }

            best = monster;
            bestDistance = distance;
        }

        return best;
    }

    private void TickInteraction(RunInput input)
    {
        if (!input.UsePressed)
        {
            return;
        }

        (int tileX, int tileY) = TileInFront(1.2f);
        if (!Level.InBounds(tileX, tileY))
        {
            return;
        }

        Tile tile = Level.At(tileX, tileY);
        switch (tile.Kind)
        {
            case TileKind.DoorClosed:
                Level.TryOpenDoor(tileX, tileY);
                ShowMessage("You open the door.");
                return;
            case TileKind.StairsDown:
                if (RunIndex + 1 >= _plan.FloorCount)
                {
                    ShowMessage("There is nowhere deeper to go.");
                    return;
                }

                EnterFloor(RunIndex + 1);
                return;
            case TileKind.StairsUp:
                TryFinishRun();
                return;
        }

        if (GroundItemAt(tileX, tileY) is GroundItem item)
        {
            CollectItem(item);
            return;
        }

        ShowMessage("There is nothing to use here.");
    }

    private void TryFinishRun()
    {
        if (RunIndex > 0)
        {
            EnterFloor(RunIndex - 1);
            return;
        }

        if (Player.HoldingOrb)
        {
            Phase = RunPhase.Won;
            ShowMessage("You escape the dungeon with the orb!");
        }
        else
        {
            ShowMessage("You cannot leave without the orb.");
        }
    }

    private void TickHotbar(RunInput input)
    {
        if (input.HotbarPressed < 1 || input.HotbarPressed > Player.Inventory.HotbarSize)
        {
            return;
        }

        int slot = input.HotbarPressed - 1;
        ItemInstance? instance = Player.Inventory.Slot(slot);
        if (instance is not ItemInstance item)
        {
            return;
        }

        ItemArchetype? archetype = _rules.Item(item.ArchetypeId);
        if (archetype is null)
        {
            return;
        }

        switch (archetype.Kind)
        {
            case ItemKind.Potion:
            case ItemKind.Food:
                Player.Body.Hp = Math.Min(Player.Body.MaxHp, Player.Body.Hp + archetype.HealAmount);
                Player.Inventory.TryConsumeOne(slot);
                ShowMessage($"You consume the {archetype.DisplayName}.");
                return;
            case ItemKind.Weapon:
            case ItemKind.RangedWeapon:
            case ItemKind.Wand:
                Player.Equipment.WeaponItemId = archetype.Id;
                Player.WieldedSlot = slot;
                ShowMessage($"You wield the {archetype.DisplayName}.");
                return;
            case ItemKind.Armor:
                Player.Equipment.ArmorItemId = archetype.Id;
                ShowMessage($"You wear the {archetype.DisplayName}.");
                return;
            case ItemKind.Helmet:
                Player.Equipment.HelmetItemId = archetype.Id;
                ShowMessage($"You wear the {archetype.DisplayName}.");
                return;
            default:
                ShowMessage($"You cannot use the {archetype.DisplayName} here.");
                return;
        }
    }

    private void CollectItemAtPlayerTile()
    {
        int tileX = Player.Body.TileX;
        int tileY = Player.Body.TileY;
        if (tileX == _lastCollectTileX && tileY == _lastCollectTileY)
        {
            return;
        }

        _lastCollectTileX = tileX;
        _lastCollectTileY = tileY;
        GroundItem? item = GroundItemAt(tileX, tileY);
        if (item is not null)
        {
            CollectItem(item);
        }
    }

    private void CollectItem(GroundItem item)
    {
        ItemArchetype? archetype = _rules.Item(item.Item.ArchetypeId);
        if (archetype is null)
        {
            return;
        }

        switch (archetype.Kind)
        {
            case ItemKind.Gold:
                Player.Gold += archetype.Value;
                _groundItems.Remove(item);
                ShowMessage($"+{archetype.Value} gold.");
                return;
            case ItemKind.Key:
                Player.Keys++;
                _groundItems.Remove(item);
                ShowMessage("You found a key.");
                return;
            case ItemKind.QuestOrb:
                Player.HoldingOrb = true;
                _groundItems.Remove(item);
                ShowMessage("You take the orb. The dungeon awakens — get out!");
                return;
            default:
                if (Player.Inventory.TryAdd(item.Item, _rules.IsStackable(item.Item.ArchetypeId)))
                {
                    _groundItems.Remove(item);
                    ShowMessage($"You pick up the {archetype.DisplayName}.");
                }
                else
                {
                    ShowMessage("Your pack is full.");
                }

                return;
        }
    }

    private GroundItem? GroundItemAt(int x, int y)
    {
        foreach (GroundItem item in _groundItems)
        {
            if (item.X == x && item.Y == y)
            {
                return item;
            }
        }

        return null;
    }

    private void TickMonsters()
    {
        foreach (MonsterState monster in _monsters)
        {
            ActorState body = monster.Body;
            body.AttackCooldownRemaining = Math.Max(0, body.AttackCooldownRemaining - 1);
            EffectSet effects = body.Effects;
            float speedMultiplier = effects.SpeedMultiplier();
            if (effects.IsParalyzed)
            {
                continue;
            }

            float distance = Distance(body, Player.Body);
            if (distance < 1.2f
                && body.AttackCooldownRemaining == 0
                && LineOfSight.CanSee(Level, body.TileX, body.TileY, Player.Body.TileX, Player.Body.TileY))
            {
                int armorClass = CombatResolver.ArmorClass(Player.Body.Stats.Defense, GearArmorClass());
                AttackOutcome outcome = CombatResolver.ResolveMelee(
                    _random, body, Player.Body, monster.Archetype.AttackPower, armorClass, _tuning);
                body.AttackCooldownRemaining = monster.Archetype.AttackCooldownTicks;
                if (outcome.Dodged)
                {
                    ShowMessage($"The {monster.Archetype.DisplayName} misses you.");
                }
                else
                {
                    ShowMessage($"The {monster.Archetype.DisplayName} hits you for {outcome.Damage}.");
                }

                continue;
            }

            MonsterBrain.Tick(monster, Level, Player, _tuning, speedMultiplier);
        }
    }

    private void TickEffects()
    {
        int playerDamage = Player.Body.Effects.Tick();
        if (playerDamage > 0)
        {
            Player.Body.Hp = Math.Max(0, Player.Body.Hp - playerDamage);
            ShowMessage($"You suffer {playerDamage} damage.");
        }

        foreach (MonsterState monster in _monsters.ToArray())
        {
            int damage = monster.Body.Effects.Tick();
            if (damage > 0)
            {
                monster.Body.Hp = Math.Max(0, monster.Body.Hp - damage);
                if (!monster.Body.Alive)
                {
                    KillMonster(monster);
                }
            }
        }
    }

    /// <summary>
    /// While the objective is held, monsters press in on a cadence that
    /// tightens the longer the escape runs, like the donor's escape arc.
    /// </summary>
    private void TickEscapeArc()
    {
        if (!Player.HoldingOrb)
        {
            return;
        }

        EscapePressureTicks++;
        _escapeSpawnRemaining--;
        if (_escapeSpawnRemaining > 0)
        {
            return;
        }

        int elapsed = Math.Max(1, EscapePressureTicks);
        int cadence = Math.Max(
            _tuning.EscapeSpawnCadenceEndTicks,
            _tuning.EscapeSpawnCadenceStartTicks - (elapsed / 10));
        _escapeSpawnRemaining = cadence;

        int spread = Math.Max(1, _tuning.EscapeSpawnGroupEnd - _tuning.EscapeSpawnGroupStart);
        int groupSize = Math.Min(
            _tuning.EscapeSpawnGroupEnd,
            _tuning.EscapeSpawnGroupStart + (elapsed / (cadence * spread)));
        IReadOnlyList<string> eligible = _rules.MonstersForFloor(Level.DifficultyLevel + 1);
        if (eligible.Count == 0)
        {
            return;
        }

        for (int i = 0; i < groupSize; i++)
        {
            (int x, int y) = FarOpenSpot();
            AddMonster(eligible[_random.Next(0, eligible.Count)], x, y);
        }

        ShowMessage("You hear pursuit closing in.");
    }

    private (int X, int Y) FarOpenSpot()
    {
        ActorState body = Player.Body;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            int x = _random.Next(0, Level.Width);
            int y = _random.Next(0, Level.Height);
            if (!Level.IsWalkable(x, y))
            {
                continue;
            }

            float dx = x - body.TileX;
            float dy = y - body.TileY;
            if ((dx * dx) + (dy * dy) >= 64f)
            {
                return (x, y);
            }
        }

        return (body.TileX, body.TileY);
    }

    /// <summary>
    /// Spawn a monster levelled for this floor and the player, or at a saved
    /// level when restoring one.
    /// </summary>
    private void AddMonster(string archetypeId, int tileX, int tileY, int savedLevel = 0)
    {
        if (_rules.Monster(archetypeId) is not MonsterArchetype archetype)
        {
            return;
        }

        int level = savedLevel > 0
            ? savedLevel
            : MonsterScaling.SpawnLevel(Level.DifficultyLevel, Player.Level, archetype.MonsterLevel);
        var body = new ActorState(
            _nextActorId++,
            ActorKind.Monster,
            tileX + 0.5f,
            tileY + 0.5f,
            MonsterScaling.MaxHitPoints(archetype.BaseHp, level),
            MonsterScaling.Stats(archetype.Stats, level));
        _monsters.Add(new MonsterState(body, archetype, level));
    }

    private void AddGroundItem(string archetypeId, int tileX, int tileY)
    {
        if (_rules.Item(archetypeId) is not ItemArchetype)
        {
            return;
        }

        _groundItems.Add(new GroundItem(ItemInstance.One(archetypeId), tileX, tileY));
    }

    private (int X, int Y) TileInFront(float distance)
    {
        ActorState body = Player.Body;
        float facingX = MathF.Sin(body.Facing);
        float facingY = -MathF.Cos(body.Facing);
        return (TileAt(body.X + (facingX * distance)), TileAt(body.Y + (facingY * distance)));
    }

    /// <summary>Armor class from the player's gear only; the defense stat is added by the resolver.</summary>
    private int GearArmorClass()
    {
        int armor = 0;
        if (Player.Equipment.ArmorItemId is string armorId && _rules.Item(armorId) is ItemArchetype armorItem)
        {
            armor += armorItem.Power;
        }

        if (Player.Equipment.HelmetItemId is string helmetId && _rules.Item(helmetId) is ItemArchetype helmetItem)
        {
            armor += helmetItem.Power;
        }

        return armor;
    }

    private void ShowMessage(string text) => _messages.Add(new RunMessage(text, MessageDurationTicks));

    private static float Distance(ActorState left, ActorState right)
    {
        float dx = left.X - right.X;
        float dy = left.Y - right.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
