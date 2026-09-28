using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver.Content;

namespace DelveRpg.Rulesets.Delver;

/// <summary>
/// The compiled Delver ruleset: it turns the donor's depth model — ordered
/// sections of themed floors with a difficulty level per section — into the
/// Kit's run plan, and creates run sessions over its own catalog.
/// </summary>
public sealed class DelverRuleset
{
    public const string RulesetId = "delver";

    private readonly DelverComposition _composition;
    private readonly DelverCatalog _catalog;

    public DelverRuleset(DelverComposition composition)
    {
        _composition = composition;
        _catalog = new DelverCatalog(composition.Pack);
    }

    public GameTuning Tuning => _composition.Tuning;

    public DelverCatalog Catalog => _catalog;

    /// <summary>
    /// Expand the section definitions into the ordered floors of one run.
    /// Donor rule: each section contributes <c>floors</c> templates drawn from
    /// its weighted list, then an optional transition floor; a floor's
    /// difficulty is <c>section.DifficultyLevel + floor</c>.
    /// </summary>
    public RunPlan BuildRunPlan(ulong runSeed)
    {
        var random = new SplitMixRandom(runSeed ^ 0x5EC7104A15UL);
        var floors = new List<FloorSpec>();
        List<DelverSectionDefinition> sections = _composition.Pack.Sections
            .OrderBy(section => section.SortOrder)
            .ToList();

        foreach (DelverSectionDefinition section in sections)
        {
            if (section.LevelTemplates.Count == 0)
            {
                throw new InvalidOperationException($"Section '{section.Name}' defines no level templates.");
            }

            for (int floor = 0; floor < section.Floors; floor++)
            {
                DelverLevelTemplate template = section.LevelTemplates[random.Next(0, section.LevelTemplates.Count)];
                floors.Add(new FloorSpec(
                    RunIndex: floors.Count,
                    DungeonLevel: section.DifficultyLevel + floor,
                    SectionName: section.Name,
                    Theme: template.Theme,
                    IsTransition: false));
            }

            if (section.TransitionLevel is DelverLevelTemplate transition)
            {
                floors.Add(new FloorSpec(
                    RunIndex: floors.Count,
                    DungeonLevel: section.DifficultyLevel + section.Floors,
                    SectionName: section.Name,
                    Theme: transition.Theme,
                    IsTransition: true));
            }
        }

        return new RunPlan(floors);
    }

    /// <summary>Start a fresh run.</summary>
    public RunSession CreateSession(ulong runSeed, MetaProgression meta)
    {
        RunPlan plan = BuildRunPlan(runSeed);
        var session = new RunSession(_catalog, Tuning, plan, runSeed, meta);
        ApplyStartingGold(session);
        return session;
    }

    /// <summary>Resume a captured run over the same plan.</summary>
    public RunSession ResumeSession(RunSnapshot snapshot, MetaProgression meta)
    {
        RunPlan plan = BuildRunPlan(snapshot.RunSeed);
        return new RunSession(_catalog, Tuning, plan, snapshot, meta);
    }

    private void ApplyStartingGold(RunSession session)
    {
        if (session.Player.Gold == 0)
        {
            session.Player.Gold = Tuning.StartingGold;
        }
    }
}
