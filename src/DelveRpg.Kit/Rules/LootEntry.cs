namespace DelveRpg.Kit.Rules;

/// <summary>
/// One weighted item drop candidate. Level bounds express the donor's loot
/// bucket idea: only entries whose window contains the floor's item level are
/// rolled, then a weighted pick decides the winner.
/// </summary>
public sealed record LootEntry(string ItemId, int Weight, int MinItemLevel, int MaxItemLevel);
