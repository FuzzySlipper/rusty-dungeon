namespace DelveRpg.Kit.Rules;

/// <summary>One actor's six core attributes. Rulesets decide the numbers.</summary>
public readonly record struct StatBlock(int Attack, int Defense, int Dexterity, int Speed, int Magic, int Endurance);
