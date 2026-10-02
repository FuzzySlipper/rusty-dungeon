namespace DelveRpg.Kit.Inventory;

/// <summary>
/// One item stack in a slot. Only stackable kinds carry counts above one;
/// <see cref="Charges"/> is what a wand has left.
/// </summary>
public readonly record struct ItemInstance(string ArchetypeId, int Count, int Charges = 0)
{
    /// <summary>Wear and tear; a default instance is in normal condition.</summary>
    public Rules.ItemCondition Condition { get; init; }

    /// <summary>The prefix enchantment's id, if any.</summary>
    public string? Prefix { get; init; }

    /// <summary>The suffix enchantment's id, if any.</summary>
    public string? Suffix { get; init; }

    /// <summary>Its enchantments are not yet known; unknown mods do nothing ([donor] Item.java:748).</summary>
    public bool Unidentified { get; init; }

    /// <summary>Uses since its condition last dropped.</summary>
    public int Wear { get; init; }

    /// <summary>The level it was found at; enchantments and damage grow with it.</summary>
    public int ItemLevel { get; init; }

    public static ItemInstance One(string archetypeId) => new(archetypeId, 1);
}

/// <summary>
/// The flat slot array the donor keeps on the HUD: hotbar slots first, then
/// backpack slots, all one grid. Capacity grows with meta-progression.
/// </summary>
public sealed class InventoryStore
{
    private ItemInstance?[] _slots;

    public InventoryStore(int hotbarSize, int backpackSize)
    {
        if (hotbarSize <= 0) throw new ArgumentOutOfRangeException(nameof(hotbarSize));
        if (backpackSize < 0) throw new ArgumentOutOfRangeException(nameof(backpackSize));
        HotbarSize = hotbarSize;
        BackpackSize = backpackSize;
        _slots = new ItemInstance?[hotbarSize + backpackSize];
    }

    public int HotbarSize { get; private set; }

    public int BackpackSize { get; private set; }

    public int Capacity => _slots.Length;

    public ItemInstance? Slot(int index) => _slots[index];

    /// <summary>Find a slot holding this item id, preferring the hotbar.</summary>
    public int Find(string archetypeId)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] is ItemInstance item && item.ArchetypeId == archetypeId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Add to an existing stack or the first free slot; false when full.</summary>
    public bool TryAdd(ItemInstance incoming, bool stackable)
    {
        if (stackable)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is ItemInstance item && item.ArchetypeId == incoming.ArchetypeId)
                {
                    _slots[i] = item with { Count = item.Count + incoming.Count };
                    return true;
                }
            }
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] is null)
            {
                _slots[i] = incoming;
                return true;
            }
        }

        return false;
    }

    /// <summary>Take the whole stack out of one slot.</summary>
    public ItemInstance? Take(int index)
    {
        ItemInstance? taken = _slots[index];
        _slots[index] = null;
        return taken;
    }

    /// <summary>Consume one unit from a stack; the slot empties at zero.</summary>
    public bool TryConsumeOne(int index)
    {
        if (_slots[index] is not ItemInstance item)
        {
            return false;
        }

        _slots[index] = item.Count <= 1 ? null : item with { Count = item.Count - 1 };
        return true;
    }

    /// <summary>One more backpack slot, at the end ([donor] entities/items/BagUpgrade.java).</summary>
    public void GrowBackpack()
    {
        Array.Resize(ref _slots, _slots.Length + 1);
        BackpackSize++;
    }

    /// <summary>
    /// One more hotbar slot, inserted after the last; every backpack slot
    /// moves up one, so callers holding slot indices at or past
    /// <paramref name="insertedAt"/> must move them too.
    /// </summary>
    public void GrowHotbar(out int insertedAt)
    {
        insertedAt = HotbarSize;
        var grown = new ItemInstance?[_slots.Length + 1];
        Array.Copy(_slots, 0, grown, 0, insertedAt);
        Array.Copy(_slots, insertedAt, grown, insertedAt + 1, _slots.Length - insertedAt);
        _slots = grown;
        HotbarSize++;
    }

    /// <summary>Spend one of a wand's charges; false when the slot holds none.</summary>
    public bool TryUseCharge(int index)
    {
        if (_slots[index] is not ItemInstance item || item.Charges <= 0)
        {
            return false;
        }

        _slots[index] = item with { Charges = item.Charges - 1 };
        return true;
    }

    /// <summary>
    /// Restore one slot exactly as captured. Unlike <see cref="TryAdd"/> this
    /// never merges or compacts: save boundaries keep slot positions because
    /// the wielded slot and the hotbar layout are identity.
    /// </summary>
    public void RestoreSlot(int index, ItemInstance? item)
    {
        if (index < 0 || index >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _slots[index] = item;
    }

    public bool TrySwap(int left, int right)
    {
        if (left < 0 || right < 0 || left >= Capacity || right >= Capacity)
        {
            return false;
        }

        (_slots[left], _slots[right]) = (_slots[right], _slots[left]);
        return true;
    }
}
