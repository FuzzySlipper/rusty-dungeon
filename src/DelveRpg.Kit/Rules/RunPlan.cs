namespace DelveRpg.Kit.Rules;

/// <summary>The single floor a run is currently on.</summary>
public sealed record FloorSpec(int RunIndex, int DungeonLevel, string SectionName, string Theme, bool IsTransition);

/// <summary>
/// The ordered floors of one run, built by the ruleset from its depth
/// sections. A run walks <see cref="Floors"/> by <see cref="FloorSpec.RunIndex"/>.
/// </summary>
public sealed record RunPlan(IReadOnlyList<FloorSpec> Floors)
{
    public int FloorCount => Floors.Count;

    public FloorSpec FloorAt(int runIndex) =>
        runIndex >= 0 && runIndex < Floors.Count
            ? Floors[runIndex]
            : throw new ArgumentOutOfRangeException(nameof(runIndex), runIndex, "Run index is outside this plan.");
}
