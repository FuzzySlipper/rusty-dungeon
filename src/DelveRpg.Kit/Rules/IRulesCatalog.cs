namespace DelveRpg.Kit.Rules;

/// <summary>
/// The ruleset's definition tables, read by the session for every
/// rules-dependent decision. One ruleset implementation owns the meaning of
/// every id the Kit sees.
/// </summary>
public interface IRulesCatalog
{
    MonsterArchetype? Monster(string id);

    ItemArchetype? Item(string id);

    /// <summary>Monster ids eligible to spawn on a floor of this difficulty.</summary>
    IReadOnlyList<string> MonstersForFloor(int dungeonLevel);

    /// <summary>Item ids eligible to drop on a floor of this difficulty.</summary>
    IReadOnlyList<string> ItemsForFloor(int dungeonLevel);

    /// <summary>
    /// One weighted loot draw for a given item level, honoring the loot bucket
    /// window; null when the table has nothing in range.
    /// </summary>
    string? RollLootId(Random.IRandomSource random, int itemLevel);

    /// <summary>Whether this item id stacks in one slot.</summary>
    bool IsStackable(string id);

    /// <summary>The item id whose pickup starts the escape arc.</summary>
    string ObjectiveItemId { get; }
}
