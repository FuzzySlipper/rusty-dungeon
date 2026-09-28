using DelveRpg.Kit.Inventory;
using Xunit;

namespace DelveRpg.Kit.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void Items_fill_the_first_free_slot()
    {
        var inventory = new InventoryStore(2, 2);
        Assert.True(inventory.TryAdd(ItemInstance.One("sword"), stackable: false));
        Assert.True(inventory.TryAdd(ItemInstance.One("bread"), stackable: true));
        Assert.Equal("sword", inventory.Slot(0)?.ArchetypeId);
        Assert.Equal("bread", inventory.Slot(1)?.ArchetypeId);
    }

    [Fact]
    public void Stackable_items_merge_into_their_stack()
    {
        var inventory = new InventoryStore(2, 2);
        Assert.True(inventory.TryAdd(new ItemInstance("gold", 3), stackable: true));
        Assert.True(inventory.TryAdd(new ItemInstance("gold", 2), stackable: true));
        Assert.Equal(5, inventory.Slot(0)?.Count);
    }

    [Fact]
    public void A_full_pack_rejects_additions()
    {
        var inventory = new InventoryStore(1, 0);
        Assert.True(inventory.TryAdd(ItemInstance.One("sword"), stackable: false));
        Assert.False(inventory.TryAdd(ItemInstance.One("bread"), stackable: true));
    }

    [Fact]
    public void Consuming_one_unit_empties_a_stack_at_zero()
    {
        var inventory = new InventoryStore(2, 0);
        inventory.TryAdd(new ItemInstance("potion", 1), stackable: true);
        Assert.True(inventory.TryConsumeOne(0));
        Assert.Null(inventory.Slot(0));
        Assert.False(inventory.TryConsumeOne(0));
    }

    [Fact]
    public void Slots_swap_and_find()
    {
        var inventory = new InventoryStore(3, 0);
        inventory.TryAdd(ItemInstance.One("sword"), stackable: false);
        inventory.TryAdd(ItemInstance.One("potion"), stackable: true);
        Assert.True(inventory.TrySwap(0, 1));
        Assert.Equal("potion", inventory.Slot(0)?.ArchetypeId);
        Assert.Equal(0, inventory.Find("potion"));
        Assert.Equal(-1, inventory.Find("absent"));
    }
}
