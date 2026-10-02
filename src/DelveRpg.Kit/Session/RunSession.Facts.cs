using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Session;

/// <summary>One hotbar/backpack slot as the HUD shows it.</summary>
/// <summary>One hotbar slot; <see cref="Charges"/> is a wand's charges left and −1 for anything else.</summary>
public sealed record HudSlot(int Index, string? ItemId, string? DisplayName, string Kind, int Count, bool Wielded, int Charges = -1);

/// <summary>The explored map around the player, as rows of cells.</summary>
public sealed record MinimapFacts(int Size, string Cells, int PlayerX, int PlayerY, float HeadingDegrees);

/// <summary>
/// Everything the DOM companion shows. The session owns what these mean; the
/// Host encodes them into the Engine UI projection and the DOM renders them.
/// </summary>
public sealed record HudFacts(
    string Phase,
    int RunIndex,
    string SectionName,
    int DungeonLevel,
    int Hp,
    int MaxHp,
    int PlayerLevel,
    int Experience,
    int ExperienceToNext,
    int Gold,
    int Keys,
    bool HoldingOrb,
    float EscapePressure,
    IReadOnlyList<string> Messages,
    string UsePrompt,
    IReadOnlyList<HudSlot> Hotbar,
    bool InventoryOpen,
    bool MapOpen,
    StatBlock Stats,
    IReadOnlyList<string> LevelUpOffers,
    int LevelUpCursor,
    MinimapFacts Minimap);

public sealed partial class RunSession
{
    /// <summary>Build the HUD read model from the current run state.</summary>
    public HudFacts BuildHudFacts()
    {
        var messages = _messages
            .TakeLast(3)
            .Select(message => message.Text)
            .Reverse()
            .ToList();

        return new HudFacts(
            Phase: Phase.ToString(),
            RunIndex: RunIndex,
            SectionName: CurrentFloor.SectionName,
            DungeonLevel: Level.DifficultyLevel,
            Hp: Player.Body.Hp,
            MaxHp: Player.Body.MaxHp,
            PlayerLevel: Player.Level,
            Experience: Player.Experience,
            ExperienceToNext: LevelProgression.ExperienceToNext(Player.Level),
            Gold: Player.Gold,
            Keys: Player.Keys,
            HoldingOrb: Player.HoldingOrb,
            EscapePressure: Math.Clamp(
                EscapePressureTicks / (float)Math.Max(1, _tuning.EscapeSpawnCadenceStartTicks * 10),
                0f,
                1f),
            Messages: messages,
            UsePrompt: UsePromptText(),
            Hotbar: BuildHotbar(),
            InventoryOpen: InventoryOpen,
            MapOpen: MapOpen,
            Stats: Player.Body.Stats,
            LevelUpOffers: LevelUpOffers,
            LevelUpCursor: LevelUpCursor,
            Minimap: BuildMinimap());
    }

    private string UsePromptText()
    {
        (int tileX, int tileY) = TileInFront(1.2f);
        if (!Level.InBounds(tileX, tileY))
        {
            return string.Empty;
        }

        GroundItem? item = GroundItemAt(tileX, tileY);
        if (item is not null && _rules.Item(item.Item.ArchetypeId) is ItemArchetype archetype)
        {
            return $"E — take {archetype.DisplayName}";
        }

        return Level.At(tileX, tileY).Kind switch
        {
            TileKind.DoorClosed => "E — open door",
            TileKind.DoorLocked => Player.Keys > 0 ? "E — unlock door (uses a key)" : "Locked — it needs a key",
            TileKind.StairsDown => "E — descend",
            TileKind.StairsUp => "E — climb up",
            _ => string.Empty,
        };
    }

    private IReadOnlyList<HudSlot> BuildHotbar()
    {
        var slots = new List<HudSlot>();
        for (int i = 0; i < Player.Inventory.HotbarSize; i++)
        {
            ItemInstance? instance = Player.Inventory.Slot(i);
            ItemArchetype? archetype = instance is ItemInstance item ? _rules.Item(item.ArchetypeId) : null;
            slots.Add(new HudSlot(
                i,
                instance?.ArchetypeId,
                archetype?.DisplayName,
                archetype?.Kind.ToString() ?? "Empty",
                instance?.Count ?? 0,
                Player.WieldedSlot == i,
                archetype?.Kind == ItemKind.Wand ? instance?.Charges ?? 0 : -1));
        }

        return slots;
    }

    /// <summary>
    /// A window of the remembered map centered on the player. Unknown tiles
    /// render blank, explored walls '#', floors '.', and the markers stand out.
    /// </summary>
    private MinimapFacts BuildMinimap()
    {
        const int size = 21;
        int centerX = Player.Body.TileX;
        int centerY = Player.Body.TileY;
        char[] cells = new char[size * size];
        Array.Fill(cells, ' ');

        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                int x = centerX - (size / 2) + column;
                int y = centerY - (size / 2) + row;
                if (!Level.InBounds(x, y) || !Fog.IsExplored(x, y))
                {
                    continue;
                }

                cells[(row * size) + column] = Level.At(x, y).Kind switch
                {
                    TileKind.StairsDown => '>',
                    TileKind.StairsUp => '<',
                    TileKind.DoorClosed or TileKind.DoorOpen or TileKind.DoorLocked => '+',
                    TileKind.Water => '~',
                    _ => Level.At(x, y).BlocksMovement ? '#' : '.',
                };
            }
        }

        cells[((size / 2) * size) + (size / 2)] = '@';
        return new MinimapFacts(size, new string(cells), centerX, centerY, Player.Body.Facing * (180f / MathF.PI));
    }
}
