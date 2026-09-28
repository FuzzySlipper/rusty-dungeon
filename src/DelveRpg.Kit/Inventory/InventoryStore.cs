namespace DelveRpg.Kit.Inventory;

/// <summary>One item stack in a slot. Only stackable kinds carry counts above one.</summary>
public readonly record struct ItemInstance(string ArchetypeId, int Count)
{
    public static ItemInstance One(string archetypeId) => new(archetypeId, 1);
}

/// <summary>
/// The flat slot array the donor keeps on the HUD: hotbar slots first, then
/// backpack slots, all one grid. Capacity grows with meta-progression.
/// </summary>
public sealed class InventoryStore
{
    private readonly ItemInstance?[] _slots;

    public InventoryStore(int hotbarSize, int backpackSize)
    {
        if (hotbarSize <= 0) throw new ArgumentOutOfRangeException(nameof(hotbarSize));
        if (backpackSize < 0) throw new ArgumentOutOfRangeException(nameof(backpackSize));
        HotbarSize = hotbarSize;
        BackpackSize = backpackSize;
        _slots = new ItemInstance?[hotbarSize + backpackSize];
    }

    public int HotbarSize { get; }

    public int BackpackSize { get; }

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
