using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;

namespace DelveRpg.Kit.Progression;

public enum CampOfferKind
{
    /// <summary>Start the run.</summary>
    Descend,

    /// <summary>A soulbound bag expansion: one more backpack slot for every run.</summary>
    BagExpansion,

    /// <summary>A soulbound belt expansion: one more hotbar slot for every run.</summary>
    BeltExpansion,

    /// <summary>An item from the camp stock, carried into the next run.</summary>
    Item,
}

/// <summary>One line of the camp menu.</summary>
public sealed record CampOffer(CampOfferKind Kind, string Label, int Cost, string? ItemId = null, int ItemLevel = 0);

/// <summary>
/// The camp between runs, where banked gold is spent. The donor's camp is a
/// walkable level with shop NPCs; its soulbound expansions are sold by a
/// merchant on a transition floor ([donor] entities/triggers/TriggeredShop.java,
/// overlays/ShopOverlay.java). This port folds both into one menu.
/// </summary>
public static class Camp
{
    /// <summary>[donor] entities/Player.java:279-280: the hotbar stops at ten.</summary>
    public const int MaxHotbar = 10;

    /// <summary>[donor] Player.java:262-263: the backpack stops at 36.</summary>
    public const int MaxBackpack = 36;

    /// <summary>The camp stock's item level ([donor] TriggeredShop.java:105-112: level 7).</summary>
    public const int StockItemLevel = 7;

    /// <summary>[donor] TriggeredShop.java:154-166: 30 + n² × (int)(30 × 0.75).</summary>
    public static int BagCost(int bought) => 30 + (bought * bought * 22);

    /// <summary>[donor] TriggeredShop.java:154-166: 60 + n² × 60.</summary>
    public static int BeltCost(int bought) => 60 + (bought * bought * 60);

    /// <summary>The menu for this visit: descend first, then the expansions while they can grow, then the stock.</summary>
    public static IReadOnlyList<CampOffer> Offers(MetaProgression meta, IReadOnlyList<string> stock, IRulesCatalog rules)
    {
        var offers = new List<CampOffer> { new(CampOfferKind.Descend, "Descend into the dungeon", 0) };
        if (meta.BackpackSize < MaxBackpack)
        {
            offers.Add(new(CampOfferKind.BagExpansion, "Soulbound bag expansion (+1 backpack slot)", BagCost(meta.InventoryUpgrades)));
        }

        if (meta.HotbarSize < MaxHotbar)
        {
            offers.Add(new(CampOfferKind.BeltExpansion, "Soulbound belt expansion (+1 hotbar slot)", BeltCost(meta.HotbarUpgrades)));
        }

        foreach (string itemId in stock)
        {
            if (rules.Item(itemId) is ItemArchetype item)
            {
                offers.Add(new(CampOfferKind.Item, item.DisplayName, Math.Max(1, item.Value), itemId, StockItemLevel));
            }
        }

        return offers;
    }

    /// <summary>Buy one offer; null when it is unaffordable or not for sale.</summary>
    public static MetaProgression? Buy(MetaProgression meta, CampOffer offer)
    {
        if (offer.Kind == CampOfferKind.Descend || offer.Cost > meta.Gold)
        {
            return null;
        }

        MetaProgression paid = meta with { Gold = meta.Gold - offer.Cost };
        return offer.Kind switch
        {
            CampOfferKind.BagExpansion when meta.BackpackSize < MaxBackpack => paid with { InventoryUpgrades = meta.InventoryUpgrades + 1 },
            CampOfferKind.BeltExpansion when meta.HotbarSize < MaxHotbar => paid with { HotbarUpgrades = meta.HotbarUpgrades + 1 },
            CampOfferKind.Item when offer.ItemId is string itemId =>
                paid with { Stash = [.. meta.Stash ?? [], new StashedItem(itemId, offer.ItemLevel)] },
            _ => null,
        };
    }

    /// <summary>
    /// The camp stock for one visit, in the shape of the donor's camp shop:
    /// two ranged weapons, a melee weapon, an armor piece, a wand and a potion,
    /// drawn from what the rules find at the stock's level (TriggeredShop.java:105-112).
    /// </summary>
    public static IReadOnlyList<string> RollStock(IRandomSource random, IRulesCatalog rules)
    {
        IReadOnlyList<string> pool = rules.ItemsForFloor(StockItemLevel);
        var stock = new List<string>();
        void Pick(Func<ItemKind, bool> wanted, int count)
        {
            List<string> candidates = pool.Where(id => rules.Item(id) is ItemArchetype item && wanted(item.Kind)).ToList();
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                stock.Add(candidates[random.Next(0, candidates.Count)]);
            }
        }

        Pick(kind => kind == ItemKind.RangedWeapon, 2);
        Pick(kind => kind == ItemKind.Weapon, 1);
        Pick(kind => kind is ItemKind.Armor or ItemKind.Helmet, 1);
        Pick(kind => kind == ItemKind.Wand, 1);
        Pick(kind => kind == ItemKind.Potion, 1);
        return stock;
    }
}
