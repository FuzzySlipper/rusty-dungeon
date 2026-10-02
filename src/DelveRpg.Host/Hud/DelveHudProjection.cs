using DelveRpg.Kit.Session;
using Rusty.Engine;

namespace DelveRpg.Host.Hud;

/// <summary>
/// Builds the one HUD projection the DOM companion observes. The shape is the
/// product's <c>delve.ui.snapshot.v1</c> contract: phase and run facts, the
/// hotbar as the donor lays it out, and the explored map window.
/// </summary>
public static class DelveHudProjection
{
    public const string StreamId = "delve.hud";
    public const string Contract = "delve.ui.snapshot.v1";

    public static UiValue Build(string hostPhase, HudFacts? facts, CampFacts? camp = null)
    {
        UiDocument document = camp is not null
            ? UiDocument.Object(
                ("phase", new UiDocument.Str(hostPhase)),
                ("camp", CampDocument(camp)))
            : facts is null
            ? UiDocument.Object(
                ("phase", new UiDocument.Str(hostPhase)))
            : UiDocument.Object(
                ("phase", new UiDocument.Str(hostPhase)),
                ("runPhase", new UiDocument.Str(facts.Phase)),
                ("runIndex", new UiDocument.Num(facts.RunIndex)),
                ("section", new UiDocument.Str(facts.SectionName)),
                ("dungeonLevel", new UiDocument.Num(facts.DungeonLevel)),
                ("hp", new UiDocument.Num(facts.Hp)),
                ("maxHp", new UiDocument.Num(facts.MaxHp)),
                ("playerLevel", new UiDocument.Num(facts.PlayerLevel)),
                ("experience", new UiDocument.Num(facts.Experience)),
                ("experienceToNext", new UiDocument.Num(facts.ExperienceToNext)),
                ("gold", new UiDocument.Num(facts.Gold)),
                ("keys", new UiDocument.Num(facts.Keys)),
                ("holdingOrb", new UiDocument.Flag(facts.HoldingOrb)),
                ("escapePressure", new UiDocument.Num(facts.EscapePressure)),
                ("usePrompt", new UiDocument.Str(facts.UsePrompt)),
                ("inventoryOpen", new UiDocument.Flag(facts.InventoryOpen)),
                ("mapOpen", new UiDocument.Flag(facts.MapOpen)),
                ("messages", UiDocument.Array(facts.Messages.Select(message => (UiDocument)new UiDocument.Str(message)).ToArray())),
                ("hotbar", UiDocument.Array(facts.Hotbar
                    .Select(slot => SlotDocument(slot, HudLayout.HotbarSlot(slot.Index, facts.Hotbar.Count)))
                    .ToArray())),
                ("backpack", UiDocument.Array(facts.Backpack
                    .Select((slot, index) => SlotDocument(slot, HudLayout.BackpackSlot(index, facts.Hotbar.Count)))
                    .ToArray())),
                ("healthBar", RectDocument(HudLayout.HealthBar)),
                ("stats", UiDocument.Object(
                    ("attack", new UiDocument.Num(facts.Stats.Attack)),
                    ("defense", new UiDocument.Num(facts.Stats.Defense)),
                    ("dexterity", new UiDocument.Num(facts.Stats.Dexterity)),
                    ("speed", new UiDocument.Num(facts.Stats.Speed)),
                    ("magic", new UiDocument.Num(facts.Stats.Magic)),
                    ("endurance", new UiDocument.Num(facts.Stats.Endurance)))),
                ("levelUp", UiDocument.Object(
                    ("offers", UiDocument.Array(facts.LevelUpOffers.Select(offer => (UiDocument)new UiDocument.Str(offer)).ToArray())),
                    ("cursor", new UiDocument.Num(facts.LevelUpCursor)))),
                ("minimap", UiDocument.Object(
                    ("size", new UiDocument.Num(facts.Minimap.Size)),
                    ("cells", new UiDocument.Str(facts.Minimap.Cells)),
                    ("playerX", new UiDocument.Num(facts.Minimap.PlayerX)),
                    ("playerY", new UiDocument.Num(facts.Minimap.PlayerY)),
                    ("heading", new UiDocument.Num(facts.Minimap.HeadingDegrees)))));

        return UiDocumentEncoder.Encode(document);
    }

    private static UiDocument CampDocument(CampFacts camp) =>
        UiDocument.Object(
            ("gold", new UiDocument.Num(camp.Gold)),
            ("wins", new UiDocument.Num(camp.Wins)),
            ("deaths", new UiDocument.Num(camp.Deaths)),
            ("hotbarSize", new UiDocument.Num(camp.HotbarSize)),
            ("backpackSize", new UiDocument.Num(camp.BackpackSize)),
            ("offers", UiDocument.Array(camp.Offers.Select(offer => (UiDocument)UiDocument.Object(
                ("label", new UiDocument.Str(offer.Label)),
                ("cost", new UiDocument.Num(offer.Cost)),
                ("affordable", new UiDocument.Flag(offer.Affordable)))).ToArray())),
            ("cursor", new UiDocument.Num(camp.Cursor)),
            ("stash", UiDocument.Array(camp.Stash.Select(name => (UiDocument)new UiDocument.Str(name)).ToArray())),
            ("message", new UiDocument.Str(camp.Message)));

    /// <summary>A layout rectangle as [x, y, width, height] viewport fractions from the lower left.</summary>
    private static UiDocument RectDocument(HudRect rect) =>
        UiDocument.Array(
            new UiDocument.Num(rect.X),
            new UiDocument.Num(rect.Y),
            new UiDocument.Num(rect.Width),
            new UiDocument.Num(rect.Height));

    private static UiDocument SlotDocument(HudSlot slot, HudRect rect) =>
        UiDocument.Object(
            ("rect", RectDocument(rect)),
            ("index", new UiDocument.Num(slot.Index)),
            ("itemId", slot.ItemId is null ? new UiDocument.Nothing() : new UiDocument.Str(slot.ItemId)),
            ("name", slot.DisplayName is null ? new UiDocument.Nothing() : new UiDocument.Str(slot.DisplayName)),
            ("kind", new UiDocument.Str(slot.Kind)),
            ("count", new UiDocument.Num(slot.Count)),
            ("wielded", new UiDocument.Flag(slot.Wielded)),
            ("charges", new UiDocument.Num(slot.Charges)));
}

/// <summary>The camp menu between runs: the purse, the record, the offers and the cursor.</summary>
public sealed record CampFacts(
    int Gold,
    int Wins,
    int Deaths,
    int HotbarSize,
    int BackpackSize,
    IReadOnlyList<CampOfferFacts> Offers,
    int Cursor,
    IReadOnlyList<string> Stash,
    string Message);

public sealed record CampOfferFacts(string Label, int Cost, bool Affordable);
