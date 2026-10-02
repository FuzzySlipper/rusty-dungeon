using DelveRpg.Kit.Inventory;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Session;
using Xunit;
using static DelveRpg.Kit.Tests.RangedCombatTests;

namespace DelveRpg.Kit.Tests;

/// <summary>Spending banked gold in camp, and bags that grow the inventory in a run.</summary>
public sealed class CampAndBagTests
{
    private static readonly ScriptedCatalog Catalog = new();

    [Fact]
    public void Soulbound_expansions_cost_the_donor_prices_and_stop_at_the_caps()
    {
        Assert.Equal([30, 52, 118, 228], Enumerable.Range(0, 4).Select(Camp.BagCost));
        Assert.Equal([60, 120, 300, 600], Enumerable.Range(0, 4).Select(Camp.BeltCost));

        var rich = MetaProgression.Fresh with { Gold = 10_000 };
        CampOffer belt = Camp.Offers(rich, [], Catalog).Single(offer => offer.Kind == CampOfferKind.BeltExpansion);
        MetaProgression bought = Camp.Buy(rich, belt)!;
        Assert.Equal(1, bought.HotbarUpgrades);
        Assert.Equal(10_000 - 60, bought.Gold);

        MetaProgression fullBelt = rich with { HotbarUpgrades = Camp.MaxHotbar - 6 };
        Assert.DoesNotContain(Camp.Offers(fullBelt, [], Catalog), offer => offer.Kind == CampOfferKind.BeltExpansion);
        MetaProgression fullBag = rich with { InventoryUpgrades = Camp.MaxBackpack - 18 };
        Assert.DoesNotContain(Camp.Offers(fullBag, [], Catalog), offer => offer.Kind == CampOfferKind.BagExpansion);
    }

    [Fact]
    public void Camp_stock_goes_into_the_stash_and_an_empty_purse_buys_nothing()
    {
        var meta = MetaProgression.Fresh with { Gold = 10 };
        IReadOnlyList<CampOffer> offers = Camp.Offers(meta, ["test.item.sword"], Catalog);
        Assert.Equal(CampOfferKind.Descend, offers[0].Kind);
        CampOffer sword = offers.Single(offer => offer.Kind == CampOfferKind.Item);

        MetaProgression bought = Camp.Buy(meta, sword)!;
        Assert.Equal(new StashedItem("test.item.sword", Camp.StockItemLevel), Assert.Single(bought.Stash!));
        Assert.Equal(5, bought.Gold);

        CampOffer bag = offers.Single(offer => offer.Kind == CampOfferKind.BagExpansion);
        Assert.Null(Camp.Buy(meta, bag)); // 30 > 10
    }

    [Fact]
    public void A_bag_adds_a_backpack_slot_and_a_belt_pouch_a_hotbar_slot_that_moves_equipment_along()
    {
        SnapshotSlot[] slots =
        [
            new SnapshotSlot("test.item.bag", 1),
            new SnapshotSlot("test.item.belt", 1),
            .. Enumerable.Repeat(new SnapshotSlot(null, 0), 4),
            new SnapshotSlot("test.item.sword", 1), // first backpack slot
        ];
        RunSession session = Hall(ScriptedRandom.Always(0), slots, null);
        session.Tick(RunInput.Idle with { HotbarPressed = 7 }); // no such hotbar key yet: nothing happens
        session.Player.WieldedSlot = 6;
        session.Player.Equipment.WeaponItemId = "test.item.sword";
        int capacity = session.Player.Inventory.Capacity;

        session.Tick(RunInput.Idle with { HotbarPressed = 1 });
        Assert.Equal(capacity + 1, session.Player.Inventory.Capacity);
        Assert.Contains(session.Messages, message => message.Text.Contains("bag size"));

        session.Tick(RunInput.Idle with { HotbarPressed = 2 });
        Assert.Equal(7, session.Player.Inventory.HotbarSize);
        Assert.Equal(7, session.Player.WieldedSlot); // the sword moved one along
        Assert.Equal("test.item.sword", session.Player.Inventory.Slot(7)?.ArchetypeId);

        RunSession restored = new(new ScriptedCatalog(), new Rules.GameTuning(), new Rules.RunPlan([new Rules.FloorSpec(0, 1, "One", "Test", false)]),
            session.Capture(), MetaProgression.Fresh, ScriptedRandom.Always(0));
        Assert.Equal(7, restored.Player.Inventory.HotbarSize);
        Assert.Equal(session.Player.Inventory.Capacity, restored.Player.Inventory.Capacity);
    }

    [Fact]
    public void Stashed_gear_arrives_plain_at_the_stock_level()
    {
        RunSession session = Hall(ScriptedRandom.Always(0), [], null);
        session.GiveStash([new StashedItem("test.item.sword", 7)]);

        ItemInstance sword = session.Player.Inventory.Slot(session.Player.Inventory.Find("test.item.sword"))!.Value;
        Assert.Equal(7, sword.ItemLevel);
        Assert.Equal(Rules.ItemCondition.Normal, sword.Condition);
    }
}
