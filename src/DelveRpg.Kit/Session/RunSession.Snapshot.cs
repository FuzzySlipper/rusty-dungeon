using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

/// <summary>Serializable shape of one slot at a save boundary.</summary>
public sealed record SnapshotSlot(string? ArchetypeId, int Count, int Charges = 0)
{
    public int Condition { get; init; }

    public string? Prefix { get; init; }

    public string? Suffix { get; init; }

    public bool Unidentified { get; init; }

    public int Wear { get; init; }

    public int ItemLevel { get; init; }
}

/// <summary>Serializable shape of one monster at a save boundary.</summary>
public sealed record SnapshotMonster(string ArchetypeId, float X, float Y, int Hp, float Facing, int Level = 0);

/// <summary>Serializable shape of one floor item at a save boundary.</summary>
public sealed record SnapshotGroundItem(string ArchetypeId, int Count, int X, int Y, int Charges = 0)
{
    public int Condition { get; init; }

    public string? Prefix { get; init; }

    public string? Suffix { get; init; }

    public bool Unidentified { get; init; }

    public int Wear { get; init; }

    public int ItemLevel { get; init; }
}

/// <summary>Serializable shape of one floor at a save boundary.</summary>
public sealed record SnapshotFloor(
    int Width,
    int Height,
    int DungeonLevel,
    string Theme,
    byte[] Tiles,
    byte[] Explored,
    int StartX,
    int StartY,
    int StairsX,
    int StairsY);

/// <summary>
/// The complete state of one run at a save boundary. The donor snapshots the
/// player and the current level at every transition; this is that boundary
/// expressed as plain data the Host persists through Engine save primitives.
/// </summary>
public sealed record RunSnapshot(
    ulong RunSeed,
    int RunIndex,
    RunPhase Phase,
    int PlayerLevel,
    int Experience,
    int Gold,
    int Keys,
    bool HoldingOrb,
    int EscapePressureTicks,
    int Hp,
    int MaxHp,
    StatBlock Stats,
    float PlayerX,
    float PlayerY,
    float Facing,
    float PitchDegrees,
    int WieldedSlot,
    string? WeaponItemId,
    string? ArmorItemId,
    string? HelmetItemId,
    IReadOnlyList<SnapshotSlot> Slots,
    SnapshotFloor Floor,
    IReadOnlyList<SnapshotMonster> Monsters,
    IReadOnlyList<SnapshotGroundItem> GroundItems,
    IReadOnlyList<string> LevelUpOffers,
    int LevelUpCursor)
{
    /// <summary>The floor's traps, triggers and pots; null in saves from before they existed.</summary>
    public IReadOnlyList<SnapshotFeature>? Features { get; init; }

    public int ArmorSlot { get; init; } = -1;

    /// <summary>Slot counts including bags used this run; zero in older saves.</summary>
    public int HotbarSize { get; init; }

    public int BackpackSize { get; init; }

    public int HelmetSlot { get; init; } = -1;

    /// <summary>Uniques already found this run.</summary>
    public IReadOnlyList<string>? SpawnedUniques { get; init; }

    /// <summary>Potion effects identified this run.</summary>
    public IReadOnlyList<string>? KnownPotions { get; init; }
}

public sealed partial class RunSession
{
    /// <summary>Capture the run at a save boundary.</summary>
    public RunSnapshot Capture()
    {
        var tiles = new byte[Level.Width * Level.Height];
        var explored = new byte[Level.Width * Level.Height];
        for (int y = 0; y < Level.Height; y++)
        {
            for (int x = 0; x < Level.Width; x++)
            {
                tiles[(y * Level.Width) + x] = (byte)Level.At(x, y).Kind;
                explored[(y * Level.Width) + x] = Fog.IsExplored(x, y) ? (byte)1 : (byte)0;
            }
        }

        (int startX, int startY, int stairsX, int stairsY) = FindMarkers();

        var slots = new SnapshotSlot[Player.Inventory.Capacity];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = Player.Inventory.Slot(i) is ItemInstance item
                ? new SnapshotSlot(item.ArchetypeId, item.Count, item.Charges)
                {
                    Condition = (int)item.Condition,
                    Prefix = item.Prefix,
                    Suffix = item.Suffix,
                    Unidentified = item.Unidentified,
                    Wear = item.Wear,
                    ItemLevel = item.ItemLevel,
                }
                : new SnapshotSlot(null, 0);
        }

        return new RunSnapshot(
            RunSeed: _runSeed,
            RunIndex: RunIndex,
            Phase: Phase,
            PlayerLevel: Player.Level,
            Experience: Player.Experience,
            Gold: Player.Gold,
            Keys: Player.Keys,
            HoldingOrb: Player.HoldingOrb,
            EscapePressureTicks: EscapePressureTicks,
            Hp: Player.Body.Hp,
            MaxHp: Player.Body.MaxHp,
            Stats: Player.Body.Stats,
            PlayerX: Player.Body.X,
            PlayerY: Player.Body.Y,
            Facing: Player.Body.Facing,
            PitchDegrees: Player.Body.PitchDegrees,
            WieldedSlot: Player.WieldedSlot,
            WeaponItemId: Player.Equipment.WeaponItemId,
            ArmorItemId: Player.Equipment.ArmorItemId,
            HelmetItemId: Player.Equipment.HelmetItemId,
            Slots: slots,
            Floor: new SnapshotFloor(
                Level.Width,
                Level.Height,
                Level.DifficultyLevel,
                Level.Theme,
                tiles,
                explored,
                startX,
                startY,
                stairsX,
                stairsY),
            Monsters: _monsters.Select(monster => new SnapshotMonster(
                monster.Archetype.Id,
                monster.Body.X,
                monster.Body.Y,
                monster.Body.Hp,
                monster.Body.Facing,
                monster.Level)).ToList(),
            GroundItems: _groundItems.Select(item => new SnapshotGroundItem(
                item.Item.ArchetypeId, item.Item.Count, item.X, item.Y, item.Item.Charges)
            {
                Condition = (int)item.Item.Condition,
                Prefix = item.Item.Prefix,
                Suffix = item.Item.Suffix,
                Unidentified = item.Item.Unidentified,
                Wear = item.Item.Wear,
                ItemLevel = item.Item.ItemLevel,
            }).ToList(),
            LevelUpOffers: LevelUpOffers.ToList(),
            LevelUpCursor: LevelUpCursor)
        {
            Features = CaptureFeatures(),
            ArmorSlot = Player.ArmorSlot,
            HotbarSize = Player.Inventory.HotbarSize,
            BackpackSize = Player.Inventory.BackpackSize,
            HelmetSlot = Player.HelmetSlot,
            SpawnedUniques = _spawnedUniques.ToList(),
            KnownPotions = _knownPotions.Select(effect => effect.ToString()).ToList(),
        };
    }

    private (int StartX, int StartY, int StairsX, int StairsY) FindMarkers()
    {
        int startX = 0;
        int startY = 0;
        int stairsX = 0;
        int stairsY = 0;
        for (int y = 0; y < Level.Height; y++)
        {
            for (int x = 0; x < Level.Width; x++)
            {
                switch (Level.At(x, y).Kind)
                {
                    case TileKind.StairsUp:
                        startX = x;
                        startY = y;
                        break;
                    case TileKind.StairsDown:
                        stairsX = x;
                        stairsY = y;
                        break;
                }
            }
        }

        return (startX, startY, stairsX, stairsY);
    }

    private PlayerState RestorePlayer(RunSnapshot snapshot)
    {
        var player = new PlayerState(
            new ActorState(
                _nextActorId++,
                ActorKind.Player,
                snapshot.PlayerX,
                snapshot.PlayerY,
                snapshot.MaxHp,
                snapshot.Stats),
            new InventoryStore(Math.Max(Meta.HotbarSize, snapshot.HotbarSize), Math.Max(Meta.BackpackSize, snapshot.BackpackSize)))
        {
            Gold = snapshot.Gold,
            Keys = snapshot.Keys,
            Experience = snapshot.Experience,
            Level = snapshot.PlayerLevel,
            HoldingOrb = snapshot.HoldingOrb,
            WieldedSlot = snapshot.WieldedSlot,
        };
        player.Body.Hp = Math.Clamp(snapshot.Hp, 0, snapshot.MaxHp);
        player.Body.Facing = snapshot.Facing;
        player.Body.PitchDegrees = snapshot.PitchDegrees;
        player.Equipment.WeaponItemId = snapshot.WeaponItemId;
        player.Equipment.ArmorItemId = snapshot.ArmorItemId;
        player.Equipment.HelmetItemId = snapshot.HelmetItemId;
        for (int i = 0; i < snapshot.Slots.Count && i < player.Inventory.Capacity; i++)
        {
            SnapshotSlot slot = snapshot.Slots[i];
            player.Inventory.RestoreSlot(
                i,
                slot.ArchetypeId is null ? null : RestoreItem(slot.ArchetypeId, slot.Count, slot.Charges, slot.Condition, slot.Prefix, slot.Suffix, slot.Unidentified, slot.Wear, slot.ItemLevel));
        }

        if (player.WieldedSlot < -1 || player.WieldedSlot >= player.Inventory.Capacity
            || (player.WieldedSlot >= 0 && player.Inventory.Slot(player.WieldedSlot) is null))
        {
            player.WieldedSlot = -1;
        }

        player.ArmorSlot = ValidSlot(player, snapshot.ArmorSlot, snapshot.ArmorItemId);
        player.HelmetSlot = ValidSlot(player, snapshot.HelmetSlot, snapshot.HelmetItemId);
        return player;
    }

    /// <summary>A saved equipment slot that still holds its item; else the first slot that does.</summary>
    private static int ValidSlot(PlayerState player, int slot, string? itemId)
    {
        if (itemId is null)
        {
            return -1;
        }

        return slot >= 0 && slot < player.Inventory.Capacity && player.Inventory.Slot(slot)?.ArchetypeId == itemId
            ? slot
            : player.Inventory.Find(itemId);
    }

    private static ItemInstance RestoreItem(
        string archetypeId, int count, int charges, int condition, string? prefix, string? suffix, bool unidentified, int wear, int itemLevel) =>
        new(archetypeId, count, charges)
        {
            Condition = Enum.IsDefined((ItemCondition)condition) ? (ItemCondition)condition : ItemCondition.Normal,
            Prefix = prefix,
            Suffix = suffix,
            Unidentified = unidentified,
            Wear = Math.Max(0, wear),
            ItemLevel = Math.Max(0, itemLevel),
        };

    private void RestoreFloor(RunSnapshot snapshot)
    {
        SnapshotFloor floor = snapshot.Floor;
        RunIndex = snapshot.RunIndex;
        var level = new DungeonLevel(floor.Width, floor.Height, floor.DungeonLevel, floor.Theme);
        for (int y = 0; y < floor.Height; y++)
        {
            for (int x = 0; x < floor.Width; x++)
            {
                level.Set(x, y, new Tile((TileKind)floor.Tiles[(y * floor.Width) + x]));
            }
        }

        Level = level;
        Fog = new FogMap(level.Width, level.Height);
        Torches = TorchPlacement.Place(level);
        LoadFeatures(RestoreFeatures(snapshot.Features));
        for (int y = 0; y < floor.Height; y++)
        {
            for (int x = 0; x < floor.Width; x++)
            {
                if (floor.Explored[(y * floor.Width) + x] != 0)
                {
                    Fog.Reveal(level, x, y, 0);
                }
            }
        }

        _monsters.Clear();
        _groundItems.Clear();
        foreach (SnapshotMonster monster in snapshot.Monsters)
        {
            // A snapshot may name a monster the current catalog no longer
            // carries; skip it instead of writing its facts onto another one.
            int before = _monsters.Count;
            AddMonster(monster.ArchetypeId, (int)MathF.Floor(monster.X), (int)MathF.Floor(monster.Y), monster.Level);
            if (_monsters.Count == before)
            {
                continue;
            }

            MonsterState restored = _monsters[^1];
            restored.Body.X = monster.X;
            restored.Body.Y = monster.Y;
            restored.Body.Hp = Math.Clamp(monster.Hp, 1, restored.Body.MaxHp);
            restored.Body.Facing = monster.Facing;
        }

        foreach (SnapshotGroundItem item in snapshot.GroundItems)
        {
            if (_rules.Item(item.ArchetypeId) is not ItemArchetype)
            {
                continue;
            }

            _groundItems.Add(new GroundItem(
                _nextActorId++,
                RestoreItem(item.ArchetypeId, item.Count, item.Charges, item.Condition, item.Prefix, item.Suffix, item.Unidentified, item.Wear, item.ItemLevel),
                item.X,
                item.Y));
        }

        EscapePressureTicks = snapshot.EscapePressureTicks;
        LevelUpOffers = (snapshot.LevelUpOffers ?? (IReadOnlyList<string>)[]).ToList();
        LevelUpCursor = Math.Clamp(snapshot.LevelUpCursor, 0, Math.Max(0, LevelUpOffers.Count - 1));
        Phase = snapshot.Phase;
        _escapeSpawnRemaining = _tuning.EscapeSpawnCadenceStartTicks;
        _levelRevision++;
    }
}
