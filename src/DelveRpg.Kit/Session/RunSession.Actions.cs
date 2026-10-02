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
        AdvanceSwing(weapon);

        // The donor has no unarmed attack: an empty hand neither charges nor
        // swings ([donor] entities/Player.java:1597 Attack, gfx/GlRenderer.java:2014).
        if (weapon is null || weapon.Kind is not (ItemKind.Weapon or ItemKind.RangedWeapon or ItemKind.Wand))
        {
            Player.AttackCharge = 0;
            return;
        }

        int chargeTicks = weapon.ChargeTicks;

        // An auto-fire wand zaps for as long as the attack is held, a bolt
        // per its fire interval ([donor] entities/Player.java:1215, 1243-1249).
        if (weapon is { Kind: ItemKind.Wand, AutoFireTicks: > 0 })
        {
            Player.AttackCharge = 0;
            if (input.AttackHeld && body.AttackCooldownRemaining == 0)
            {
                _attackedThisTick = true;
                ZapWand(weapon);
                body.AttackCooldownRemaining = weapon.AutoFireTicks;
                Player.Swing = new SwingState(false, 1f, weapon.AutoFireTicks, 0, landed: true);
            }

            return;
        }

        // The donor's swing: holding winds the charge up to full and holds
        // it there; releasing swings at the charged fraction of full power
        // ([donor] entities/Player.java:1221-1258, 1588-1592).
        if (input.AttackHeld)
        {
            if (body.AttackCooldownRemaining == 0)
            {
                Player.AttackCharge = Math.Min(chargeTicks, Player.AttackCharge + 1);
            }

            return;
        }

        if (Player.AttackCharge == 0)
        {
            return;
        }

        _attackedThisTick = true;
        float attackPower = Player.AttackCharge / (float)Math.Max(1, chargeTicks);
        Player.AttackCharge = 0;
        StartSwing(weapon, attackPower);
        Wear(Player.WieldedSlot);
    }

    /// <summary>
    /// Release an attack. A bow or wand fires at once; a melee weapon starts
    /// its swing, whose blow lands partway through (<see cref="AdvanceSwing"/>).
    /// The quick swing plays below half charge and the full one from half up,
    /// at playback <c>speed × 0.25 + (DEX − 4) × 0.015</c>; the next attack waits
    /// three quarters of the swing ([donor] entities/Player.java:1566-1581,
    /// items/Sword.java:56-69). Bows set no wait, as in the donor.
    /// </summary>
    private void StartSwing(ItemArchetype weapon, float attackPower)
    {
        CueAtPlayer(CueKind.Swing, weapon.Id);
        ActorState body = Player.Body;
        bool strong = attackPower >= 0.5f && weapon.StrongSwing is not null;
        SwingTiming? timing = strong ? weapon.StrongSwing : weapon.WeakSwing;
        float playback = Math.Max(0.05f, ((weapon.Speed + WieldedNumbers(weapon).AttackSpeedBonus) * 0.25f) + ((body.Stats.Dexterity - 4) * 0.015f));
        int length = timing is null ? _tuning.AttackCooldownTicks : (int)MathF.Round(timing.Length / playback);
        int cooldown = timing is null ? _tuning.AttackCooldownTicks : (int)MathF.Round(length * 0.75f);

        switch (weapon.Kind)
        {
            case ItemKind.RangedWeapon:
                ShootBow(weapon, attackPower);
                Player.Swing = new SwingState(strong, attackPower, length, 0, landed: true);
                body.AttackCooldownRemaining = 0;
                return;
            case ItemKind.Wand:
                ZapWand(weapon);
                Player.Swing = new SwingState(strong, attackPower, length, 0, landed: true);
                body.AttackCooldownRemaining = cooldown;
                return;
        }

        if (timing is null)
        {
            // A weapon without swing timing strikes on release.
            SwingWeapon(weapon, attackPower);
            body.AttackCooldownRemaining = cooldown;
            return;
        }

        int hitAt = Math.Max(1, (int)MathF.Round(timing.ActionTime / playback * 0.5f));
        Player.Swing = new SwingState(strong, attackPower, length, hitAt, landed: false);
        body.AttackCooldownRemaining = cooldown;
    }

    /// <summary>Play the current swing on by a tick; its blow lands at the action time.</summary>
    private void AdvanceSwing(ItemArchetype? weapon)
    {
        if (Player.Swing is not SwingState swing)
        {
            return;
        }

        swing.ElapsedTicks++;
        if (!swing.Landed && swing.ElapsedTicks >= swing.HitAtTicks)
        {
            swing.Landed = true;
            SwingWeapon(weapon, swing.Power);
        }

        if (swing.ElapsedTicks >= swing.LengthTicks && swing.Landed)
        {
            Player.Swing = null;
        }
    }

    private void SwingWeapon(ItemArchetype? weapon, float attackPower)
    {
        MonsterState? target = NearestTargetInReach(ReachTiles);
        Pot? pot = NearestPotInReach(ReachTiles);
        if (pot is not null
            && (target is null || Distance(Player.Body.X, Player.Body.Y, pot.X, pot.Y) < Distance(Player.Body, target.Body)))
        {
            WeaponNumbers potNumbers = weapon is null ? default : WieldedNumbers(weapon);
            DamagePot(pot, Math.Max(1, CombatResolver.WeaponDamage(
                _random, potNumbers.BaseDamage, potNumbers.RandDamage, EffectiveStats().Attack, attackPower)));
            return;
        }

        if (target is null)
        {
            ShowMessage("Your swing finds only air.");
            return;
        }

        WeaponNumbers numbers = weapon is null ? default : WieldedNumbers(weapon);
        int damage = CombatResolver.WeaponDamage(
            _random, numbers.BaseDamage, numbers.RandDamage, EffectiveStats().Attack, attackPower) + numbers.ElementalDamage;

        // The donor shoves along the facing times the distance the blow
        // landed at, at most the weapon's reach ([donor] items/Sword.java:95-100).
        float facingX = MathF.Sin(Player.Body.Facing);
        float facingY = -MathF.Cos(Player.Body.Facing);
        HitMonster(
            target,
            damage,
            weapon is null ? DamageType.Physical : numbers.DamageType,
            facingX * WeaponPushReach,
            facingY * WeaponPushReach,
            attackPower * ((weapon?.Knockback ?? 0f) + numbers.KnockbackBonus));
        ShowMessage($"You hit the {target.Archetype.DisplayName} for {damage}.");
    }

    /// <summary>The donor weapon's default reach, the shove a swing's knockback scales.</summary>
    private const float WeaponPushReach = 0.5f;

    private void KillMonster(MonsterState monster)
    {
        _monsters.Remove(monster);
        _corpses.Add(new Corpse(_nextActorId++, monster.Archetype.SpriteId, monster.Body.X, monster.Body.Y, ElapsedTicks));
        // The donor awards 3 + its zero-based level field, which initLevel sets
        // to the spawn level minus one ([donor] DelverGameMode.java:123).
        int experience = CombatResolver.ExperienceForKill(monster.Level - 1);
        Player.Experience += experience;
        ShowMessage($"The {monster.Archetype.DisplayName} falls. (+{experience} xp)");
        Cue(CueKind.MonsterDie, monster.Body.X, monster.Body.Y, monster.Archetype.Id);
        foreach (ItemInstance carried in monster.Carried)
        {
            DropItem(carried, monster.Body.TileX, monster.Body.TileY);
        }

        if (RollUnique() is string uniqueId)
        {
            AddGroundItem(uniqueId, monster.Body.TileX, monster.Body.TileY);
            ShowMessage("Something rare glints where it fell.");
        }
        else if (_random.Chance(_tuning.EnchantChance) && _rules.RollLootId(_random, Level.DifficultyLevel) is string lootId)
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
            if (monster.IsDying)
            {
                continue;
            }

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
                Cue(CueKind.DoorOpen, tileX + 0.5f, tileY + 0.5f);
                return;
            case TileKind.DoorLocked:
                // A locked door takes one key and stays open
                // ([donor] entities/Door.java:212-236).
                if (Player.Keys <= 0)
                {
                    ShowMessage("The door is locked.");
                    Cue(CueKind.DoorLocked, tileX + 0.5f, tileY + 0.5f);
                    return;
                }

                Player.Keys--;
                Level.Set(tileX, tileY, Tile.DoorOpen);
                ShowMessage("You unlock the door.");
                Cue(CueKind.DoorOpen, tileX + 0.5f, tileY + 0.5f);
                return;
            case TileKind.StairsDown:
                if (RunIndex + 1 >= _plan.FloorCount)
                {
                    ShowMessage("There is nowhere deeper to go.");
                    return;
                }

                TravelTo(RunIndex + 1);
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
            TravelTo(RunIndex - 1);
            return;
        }

        if (Player.HoldingOrb)
        {
            Phase = RunPhase.Won;
            ShowMessage("You escape the dungeon with the orb!");
            CueAtPlayer(CueKind.Escape);
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
                Player.Inventory.TryConsumeOne(slot);
                DrinkPotion(archetype);
                return;
            case ItemKind.Food:
                Player.Body.Hp = Math.Min(Player.Body.MaxHp, Player.Body.Hp + archetype.HealAmount);
                Player.Inventory.TryConsumeOne(slot);
                ShowMessage($"You eat the {archetype.DisplayName}.");
                CueAtPlayer(CueKind.Eat);
                return;
            case ItemKind.Weapon:
            case ItemKind.RangedWeapon:
            case ItemKind.Wand:
                Player.Equipment.WeaponItemId = archetype.Id;
                Player.WieldedSlot = slot;
                ShowMessage($"You wield the {ItemName(item)}.");
                CueAtPlayer(CueKind.Equip, archetype.Id);
                IdentifyOnEquip(slot);
                return;
            case ItemKind.Armor:
                Player.Equipment.ArmorItemId = archetype.Id;
                Player.ArmorSlot = slot;
                ShowMessage($"You wear the {ItemName(item)}.");
                CueAtPlayer(CueKind.Equip, archetype.Id);
                IdentifyOnEquip(slot);
                return;
            case ItemKind.Helmet:
                Player.Equipment.HelmetItemId = archetype.Id;
                Player.HelmetSlot = slot;
                ShowMessage($"You wear the {ItemName(item)}.");
                CueAtPlayer(CueKind.Equip, archetype.Id);
                IdentifyOnEquip(slot);
                return;
            case ItemKind.BagUpgrade:
                UseBag(slot, archetype);
                return;
            default:
                ShowMessage($"You cannot use the {archetype.DisplayName} here.");
                return;
        }
    }

    /// <summary>
    /// A bag upgrade is used up for one more slot this run: a belt pouch
    /// grows the hotbar, a bag the backpack, up to the donor's caps
    /// ([donor] entities/items/BagUpgrade.java:35-63; Player.java:262-280).
    /// </summary>
    private void UseBag(int slot, ItemArchetype bag)
    {
        InventoryStore inventory = Player.Inventory;
        if (bag.GrowsHotbar ? inventory.HotbarSize >= Camp.MaxHotbar : inventory.BackpackSize >= Camp.MaxBackpack)
        {
            ShowMessage(bag.GrowsHotbar ? "Your belt cannot hold more." : "Your bag cannot grow larger.");
            return;
        }

        inventory.TryConsumeOne(slot);
        if (!bag.GrowsHotbar)
        {
            inventory.GrowBackpack();
            ShowMessage("Your bag size increased!");
            return;
        }

        inventory.GrowHotbar(out int insertedAt);
        Player.WieldedSlot = Shifted(Player.WieldedSlot, insertedAt);
        Player.ArmorSlot = Shifted(Player.ArmorSlot, insertedAt);
        Player.HelmetSlot = Shifted(Player.HelmetSlot, insertedAt);
        ShowMessage("Your belt size increased!");
    }

    private static int Shifted(int slot, int insertedAt) => slot >= insertedAt ? slot + 1 : slot;

    /// <summary>Hand over the gear bought in camp, plain and at the stock's level.</summary>
    public void GiveStash(IReadOnlyList<StashedItem>? stash)
    {
        foreach (StashedItem stashed in stash ?? [])
        {
            if (_rules.Item(stashed.ItemId) is ItemArchetype archetype)
            {
                var item = new ItemInstance(archetype.Id, Math.Max(1, archetype.StackSize), archetype.Charges) { ItemLevel = stashed.ItemLevel };
                if (!Player.Inventory.TryAdd(item, _rules.IsStackable(archetype.Id)))
                {
                    DropItem(item, Player.Body.TileX, Player.Body.TileY);
                }
            }
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
                CueAtPlayer(CueKind.PickupGold);
                return;
            case ItemKind.Key:
                Player.Keys++;
                _groundItems.Remove(item);
                ShowMessage("You found a key.");
                CueAtPlayer(CueKind.Pickup, archetype.Id);
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
                    ShowMessage($"You pick up the {ItemName(item.Item)}.");
                    CueAtPlayer(CueKind.Pickup, item.Item.ArchetypeId);
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
        foreach (MonsterState monster in _monsters.ToArray())
        {
            ActorState body = monster.Body;
            body.AttackCooldownRemaining = Math.Max(0, body.AttackCooldownRemaining - 1);
            monster.RangedCooldownRemaining = Math.Max(0, monster.RangedCooldownRemaining - 1);
            if (!TickMonsterBody(monster))
            {
                continue;
            }

            EffectSet effects = body.Effects;
            float speedMultiplier = effects.SpeedMultiplier();
            if (effects.IsParalyzed)
            {
                continue;
            }

            float distance = Distance(body, Player.Body);
            bool seesPlayer = LineOfSight.CanSee(Level, body.TileX, body.TileY, Player.Body.TileX, Player.Body.TileY);
            if (TickMeleeAttack(monster, distance, seesPlayer))
            {
                // Winding up: the monster stands and swings
                // ([donor] Monster.java:735-750 postAttackMoveWaitTimer).
                continue;
            }

            if (monster.Archetype.Ranged is MonsterRangedAttack ranged
                && monster.BrainState == MonsterBrainState.Chasing
                && seesPlayer
                && distance > ranged.MinDistance
                && distance < ranged.MaxDistance
                && monster.RangedCooldownRemaining == 0)
            {
                CastAtPlayer(monster, ranged);
                monster.RangedCooldownRemaining = ranged.CooldownTicks + _random.Next(0, 30);
            }

            MonsterBrainState before = monster.BrainState;
            if (monster.HurtTicksRemaining > 0)
            {
                monster.Body.Z = Level.StandHeight(monster.Body.TileX, monster.Body.TileY);
                // A flinching monster stands where the blow left it.
                continue;
            }

            MonsterBrain.Tick(monster, Level, Player, _tuning, speedMultiplier, NoticeRadius, _monsters, _random);
            monster.Body.Z = Level.StandHeight(monster.Body.TileX, monster.Body.TileY);
            if (before == MonsterBrainState.Idle && monster.BrainState == MonsterBrainState.Chasing)
            {
                Cue(CueKind.MonsterAlert, monster.Body.X, monster.Body.Y, monster.Archetype.Id);
                // Alerted monsters wait a beat before the first cast
                // ([donor] entities/Monster.java:555-565).
                monster.RangedCooldownRemaining = 40 + _random.Next(0, 20);
            }
        }
    }

    private void TickEffects()
    {
        int playerDamage = Player.Body.Effects.Tick(Player.Body.Hp);
        if (playerDamage < 0)
        {
            Player.Body.Hp = Math.Min(Player.Body.MaxHp, Player.Body.Hp - playerDamage);
        }
        else if (playerDamage > 0)
        {
            HurtPlayer(playerDamage);
            ShowMessage($"You suffer {playerDamage} damage.");
        }

        foreach (MonsterState monster in _monsters.ToArray())
        {
            int damage = monster.Body.Effects.Tick(monster.Body.Hp);
            if (damage > 0)
            {
                monster.Body.Hp = Math.Max(0, monster.Body.Hp - damage);
                if (!monster.Body.Alive && monster.DyingTicksRemaining == 0)
                {
                    monster.HurtTicksRemaining = monster.Archetype.HurtTicks;
                    monster.DyingTicksRemaining = DeathDelayTicks;
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
        if (_rules.Item(archetypeId) is not ItemArchetype archetype)
        {
            return;
        }

        DropItem(Fresh(archetype), tileX, tileY);
    }

    private (int X, int Y) TileInFront(float distance)
    {
        ActorState body = Player.Body;
        float facingX = MathF.Sin(body.Facing);
        float facingY = -MathF.Cos(body.Facing);
        return (TileAt(body.X + (facingX * distance)), TileAt(body.Y + (facingY * distance)));
    }

    private void ShowMessage(string text) => _messages.Add(new RunMessage(text, MessageDurationTicks));

    private static float Distance(float ax, float ay, float bx, float by) => MathF.Sqrt(DistanceSquared(ax, ay, bx, by));

    private static float Distance(ActorState left, ActorState right)
    {
        float dx = left.X - right.X;
        float dy = left.Y - right.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
