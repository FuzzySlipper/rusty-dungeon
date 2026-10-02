using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Ai;
using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

/// <summary>
/// The single mutable owner of one run: the current floor, the player, the
/// monsters, and the rules decisions between them. One tick advances one
/// admitted Engine step; the Host feeds semantic <see cref="RunInput"/> and
/// reads presentation facts back out.
/// </summary>
public sealed partial class RunSession
{
    private const int MessageDurationTicks = 180;
    private const float BodyRadius = 0.25f;
    private const float ReachTiles = 1.6f;

    private readonly IRulesCatalog _rules;
    private readonly GameTuning _tuning;
    private readonly RunPlan _plan;
    private readonly IRandomSource _random;
    private readonly ulong _runSeed;
    private readonly List<RunMessage> _messages = new();
    private readonly List<MonsterState> _monsters = new();
    private readonly List<GroundItem> _groundItems = new();
    private long _nextActorId = 1;

    private float _velocityX;
    private float _velocityY;
    private int _lastPlayerTileX = int.MinValue;
    private int _lastPlayerTileY = int.MinValue;
    private int _escapeSpawnRemaining;
    private int _fogRefreshRemaining;
    private int _lastCollectTileX = int.MinValue;
    private int _lastCollectTileY = int.MinValue;

    public RunSession(
        IRulesCatalog rules,
        GameTuning tuning,
        RunPlan plan,
        ulong runSeed,
        MetaProgression meta,
        IRandomSource runtimeDraws)
    {
        _rules = rules;
        _tuning = tuning;
        _plan = plan;
        _runSeed = runSeed;
        _random = runtimeDraws;
        Meta = meta;
        StatBlock startingStats = new(4, 2, 4, 4, 2, 5);
        Player = new PlayerState(
            new ActorState(
                _nextActorId++,
                ActorKind.Player,
                0f,
                0f,
                LevelProgression.MaxHitPoints(startingStats, 1),
                startingStats),
            new InventoryStore(meta.HotbarSize, meta.BackpackSize));
        EnterFloor(0);
    }

    /// <summary>Restores a previously captured run. See <see cref="Capture"/>.</summary>
    public RunSession(
        IRulesCatalog rules,
        GameTuning tuning,
        RunPlan plan,
        RunSnapshot snapshot,
        MetaProgression meta,
        IRandomSource runtimeDraws)
    {
        if (snapshot.RunIndex < 0 || snapshot.RunIndex >= plan.FloorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                $"Snapshot run index {snapshot.RunIndex} is outside the rebuilt plan ({plan.FloorCount} floors).");
        }

        _rules = rules;
        _tuning = tuning;
        _plan = plan;
        _runSeed = snapshot.RunSeed;
        _random = runtimeDraws;
        Meta = meta;
        Player = RestorePlayer(snapshot);
        RestoreFloor(snapshot);
    }

    public RunPhase Phase { get; private set; } = RunPhase.Playing;

    public int RunIndex { get; private set; }

    public DungeonLevel Level { get; private set; } = null!;

    public FogMap Fog { get; private set; } = null!;

    /// <summary>The current floor's wall torches; their light decides how far monsters see the player.</summary>
    public IReadOnlyList<WallTorch> Torches { get; private set; } = Array.Empty<WallTorch>();

    public PlayerState Player { get; }

    public MetaProgression Meta { get; }

    public IReadOnlyList<MonsterState> Monsters => _monsters;

    public IReadOnlyList<GroundItem> GroundItems => _groundItems;

    public IReadOnlyList<RunMessage> Messages => _messages;

    /// <summary>Bumped every time a floor is generated or restored.</summary>
    /// <summary>
    /// Monotonic scene-staleness marker: floor changes plus every grid change
    /// on the current floor (a door opening, wherever it was opened from).
    /// </summary>
    public ulong LevelRevision => _levelRevision + Level.Revision;

    private ulong _levelRevision;

    public bool InventoryOpen { get; private set; }

    public bool MapOpen { get; private set; }

    public IReadOnlyList<string> LevelUpOffers { get; private set; } = Array.Empty<string>();

    public int LevelUpCursor { get; private set; }

    /// <summary>Ticks the objective has been held; drives the escape arc.</summary>
    public int EscapePressureTicks { get; private set; }

    public FloorSpec CurrentFloor => _plan.FloorAt(RunIndex);

    /// <summary>
    /// Admitted ticks this session has run. Presentation reads it for sprite
    /// animation; it is not saved, so a resumed run counts from zero.
    /// </summary>
    public long ElapsedTicks { get; private set; }

    /// <summary>
    /// Hand a fresh character its starting kit: each item goes to the first
    /// free slot, and the first weapon, body armor, and helmet are put on,
    /// like the donor's starting inventory.
    /// </summary>
    public void GiveStartingKit(IReadOnlyList<string> itemIds)
    {
        foreach (string itemId in itemIds)
        {
            ItemArchetype archetype = _rules.Item(itemId)
                ?? throw new InvalidOperationException($"Starting kit item '{itemId}' is not in the catalog.");
            if (!Player.Inventory.TryAdd(Fresh(archetype), _rules.IsStackable(itemId)))
            {
                throw new InvalidOperationException($"Starting kit item '{itemId}' does not fit the inventory.");
            }

            switch (archetype.Kind)
            {
                case ItemKind.Weapon or ItemKind.RangedWeapon or ItemKind.Wand when Player.Equipment.WeaponItemId is null:
                    Player.Equipment.WeaponItemId = itemId;
                    Player.WieldedSlot = Player.Inventory.Find(itemId);
                    break;
                case ItemKind.Armor when Player.Equipment.ArmorItemId is null:
                    Player.Equipment.ArmorItemId = itemId;
                    break;
                case ItemKind.Helmet when Player.Equipment.HelmetItemId is null:
                    Player.Equipment.HelmetItemId = itemId;
                    break;
            }
        }
    }

    /// <summary>Advance one tick of the run.</summary>
    public void Tick(RunInput input)
    {
        ElapsedTicks++;
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            RunMessage message = _messages[i];
            if (message.RemainingTicks <= 1)
            {
                _messages.RemoveAt(i);
            }
            else
            {
                _messages[i] = message with { RemainingTicks = message.RemainingTicks - 1 };
            }
        }

        switch (Phase)
        {
            case RunPhase.LevelUp:
                TickLevelUp(input);
                return;
            case RunPhase.Dead:
            case RunPhase.Won:
                return;
        }

        Player.Body.AttackCooldownRemaining = Math.Max(0, Player.Body.AttackCooldownRemaining - 1);
        Player.HurtFlashRemaining = Math.Max(0, Player.HurtFlashRemaining - 1);

        TickMovement(input);
        TickCombat(input);
        TickInteraction(input);
        TickHotbar(input);

        if (input.InventoryToggled)
        {
            InventoryOpen = !InventoryOpen;
        }

        if (input.MapToggled)
        {
            MapOpen = !MapOpen;
        }

        TickStealth();
        TickMonsters();
        TickProjectiles();
        TickEffects();
        TickEscapeArc();
        TickFog();

        if (!Player.Body.Alive)
        {
            Phase = RunPhase.Dead;
            ShowMessage("You have died.");
            return;
        }

        TickExperience();
    }

    private void TickMovement(RunInput input)
    {
        ActorState body = Player.Body;
        body.Facing += DegreesToRadians(input.LookYawDegrees);
        body.PitchDegrees = Math.Clamp(body.PitchDegrees + input.LookPitchDegrees, -80f, 80f);

        // Charging a swing slows the walk, like the donor's attack wind-up.
        float speedMultiplier = Player.Body.Effects.SpeedMultiplier();
        if (Player.AttackCharge > 0)
        {
            int chargeTicks = Math.Max(1, _tuning.AttackChargeTicks);
            speedMultiplier *= 1f - (0.5f * Math.Min(1f, Player.AttackCharge / (float)chargeTicks));
        }

        bool paralyzed = Player.Body.Effects.IsParalyzed;
        float moveX = input.MoveX;
        float moveY = input.MoveY;
        float length = MathF.Sqrt((moveX * moveX) + (moveY * moveY));
        if (length > 1f)
        {
            moveX /= length;
            moveY /= length;
        }

        // Camera-relative planar movement: forward follows the look yaw and
        // strafe is its perpendicular (the donor's FPS walk over the tile
        // grid; the same facing basis the reach slice uses).
        float facingSin = MathF.Sin(body.Facing);
        float facingCos = MathF.Cos(body.Facing);
        float wishX = (moveY * facingSin) + (moveX * facingCos);
        float wishY = (-moveY * facingCos) + (moveX * facingSin);

        _velocityX = (_velocityX * _tuning.WalkFriction) + (wishX * _tuning.WalkAcceleration);
        _velocityY = (_velocityY * _tuning.WalkFriction) + (wishY * _tuning.WalkAcceleration);
        float speed = MathF.Sqrt((_velocityX * _velocityX) + (_velocityY * _velocityY));
        float maxSpeed = _tuning.MaxWalkSpeed * speedMultiplier;
        if (speed > maxSpeed)
        {
            _velocityX *= maxSpeed / speed;
            _velocityY *= maxSpeed / speed;
        }

        if (paralyzed)
        {
            _velocityX = 0f;
            _velocityY = 0f;
        }

        MovePlayer(_velocityX, _velocityY);
    }

    private void MovePlayer(float deltaX, float deltaY)
    {
        ActorState body = Player.Body;
        float newX = body.X + deltaX;
        if (IsFree(newX, body.Y) && !CrowdsMonster(body.X, body.Y, newX, body.Y))
        {
            body.X = newX;
        }
        else
        {
            _velocityX = 0f;
        }

        float newY = body.Y + deltaY;
        if (IsFree(body.X, newY) && !CrowdsMonster(body.X, body.Y, body.X, newY))
        {
            body.Y = newY;
        }
        else
        {
            _velocityY = 0f;
        }

        CollectItemAtPlayerTile();
    }

    private bool IsFree(float x, float y) =>
        Level.IsWalkable(TileAt(x - BodyRadius), TileAt(y - BodyRadius))
        && Level.IsWalkable(TileAt(x + BodyRadius), TileAt(y - BodyRadius))
        && Level.IsWalkable(TileAt(x - BodyRadius), TileAt(y + BodyRadius))
        && Level.IsWalkable(TileAt(x + BodyRadius), TileAt(y + BodyRadius));

    /// <summary>
    /// True when a step ends inside a monster's separation and closer than it
    /// started; stepping away from an overlap stays free so nothing sticks.
    /// </summary>
    private bool CrowdsMonster(float fromX, float fromY, float toX, float toY)
    {
        float separation = _tuning.ActorSeparationTiles;
        foreach (MonsterState monster in _monsters)
        {
            if (monster.IsDying)
            {
                continue;
            }

            float after = DistanceSquared(monster.Body.X, monster.Body.Y, toX, toY);
            if (after < separation * separation
                && after < DistanceSquared(monster.Body.X, monster.Body.Y, fromX, fromY))
            {
                return true;
            }
        }

        return false;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by) =>
        ((ax - bx) * (ax - bx)) + ((ay - by) * (ay - by));

    private static int TileAt(float coordinate) => (int)MathF.Floor(coordinate);

    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);

    private void TickFog()
    {
        ActorState body = Player.Body;
        _fogRefreshRemaining--;
        bool tileChanged = body.TileX != _lastPlayerTileX || body.TileY != _lastPlayerTileY;
        if (tileChanged || _fogRefreshRemaining <= 0)
        {
            Fog.Reveal(Level, body.TileX, body.TileY, _tuning.SeenRadius);
            _lastPlayerTileX = body.TileX;
            _lastPlayerTileY = body.TileY;
            _fogRefreshRemaining = 15;
        }
    }

    private void TickLevelUp(RunInput input)
    {
        if (LevelUpOffers.Count == 0)
        {
            Phase = RunPhase.Playing;
            return;
        }

        if (input.MenuUp)
        {
            LevelUpCursor = Math.Max(0, LevelUpCursor - 1);
        }

        if (input.MenuDown)
        {
            LevelUpCursor = Math.Min(LevelUpOffers.Count - 1, LevelUpCursor + 1);
        }

        if (input.MenuCancel)
        {
            LevelUpCursor = 0;
        }

        if (input.MenuConfirm && LevelUpOffers.Count > 0)
        {
            LevelProgression.ApplyStatChoice(Player, LevelUpOffers[LevelUpCursor]);
            ShowMessage($"Your {LevelUpOffers[LevelUpCursor]} rises.");
            LevelUpOffers = Array.Empty<string>();
            LevelUpCursor = 0;
            Phase = RunPhase.Playing;
        }
    }

    private void TickExperience()
    {
        bool leveled = false;
        while (Player.Experience >= LevelProgression.ExperienceToNext(Player.Level))
        {
            LevelProgression.ApplyLevelUp(Player);
            leveled = true;
        }

        if (leveled)
        {
            LevelUpOffers = LevelProgression.RollOffers(_random);
            LevelUpCursor = 0;
            Phase = RunPhase.LevelUp;
            ShowMessage("You have grown stronger. Choose your fate.");
        }
    }

    /// <summary>Build (or rebuild) the floor at one run index and place the run on it.</summary>
    private void EnterFloor(int runIndex)
    {
        RunIndex = runIndex;
        FloorSpec floor = _plan.FloorAt(runIndex);
        SplitMixRandom floorRandom = new(SplitMixRandom.FloorSeed(_runSeed, runIndex));
        GeneratedLevel generated = DungeonGenerator.Generate(
            floorRandom,
            GenerationDefaults.For(floor),
            _tuning,
            floor,
            _rules.MonstersForFloor(floor.DungeonLevel),
            _rules.ItemsForFloor(floor.DungeonLevel));

        Level = generated.Level;
        Fog = new FogMap(Level.Width, Level.Height);
        Torches = TorchPlacement.Place(Level);
        _monsters.Clear();
        _groundItems.Clear();
        _projectiles.Clear();
        _corpses.Clear();
        Player.Body.X = generated.StartX + 0.5f;
        Player.Body.Y = generated.StartY + 0.5f;
        _velocityX = 0f;
        _velocityY = 0f;
        _lastPlayerTileX = int.MinValue;
        _lastPlayerTileY = int.MinValue;
        _lastCollectTileX = int.MinValue;
        _lastCollectTileY = int.MinValue;

        foreach (MonsterSpawn spawn in generated.Monsters)
        {
            AddMonster(spawn.ArchetypeId, spawn.X, spawn.Y);
        }

        foreach (ItemSpawn spawn in generated.Items.Concat(generated.BonusLoot))
        {
            AddGroundItem(spawn.ArchetypeId, spawn.X, spawn.Y);
        }

        if (runIndex == _plan.FloorCount - 1)
        {
            AddGroundItem(_rules.ObjectiveItemId, generated.StairsX, generated.StairsY);
        }

        _escapeSpawnRemaining = _tuning.EscapeSpawnCadenceStartTicks;
        _levelRevision++;
        ShowMessage($"{floor.SectionName} — dungeon level {floor.DungeonLevel}.");
    }
}
