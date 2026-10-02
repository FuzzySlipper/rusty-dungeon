using System.Text.Json;
using DelveRpg.Host.Playtest;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Host.Tests;

/// <summary>The playtest adapter reads the live run and reports bindings with their eligibility.</summary>
public sealed class DelvePlaytestTests
{
    private static readonly DelverRuleset Ruleset = new(DelverComposition.Load(ShippedContent.Read));

    private static RunSession NewRun() =>
        Ruleset.CreateSession(99UL, MetaProgression.Fresh, new Kit.Random.SplitMixRandom(7UL));

    [Fact]
    public void On_the_title_only_confirm_is_available()
    {
        var playtest = new DelvePlaytest(() => null, () => "title", Ruleset.Catalog);

        Assert.True(playtest.InspectAction("confirm").Available);
        Assert.Equal("Enter", playtest.InspectAction("confirm").Key);
        Assert.False(playtest.InspectAction("forward").Available);
        Assert.False(playtest.InspectAction("nonsense").Available);
        Assert.Contains("title", playtest.Observe().Message);
    }

    [Fact]
    public void In_a_run_attack_holds_for_the_weapons_full_charge_and_look_turns_without_ticking()
    {
        RunSession session = NewRun();
        var playtest = new DelvePlaytest(() => session, () => "run", Ruleset.Catalog);

        var attack = playtest.InspectAction("attack");
        Assert.True(attack.Available);
        Assert.Equal("Primary", attack.Key);
        Assert.True(attack.Hold);
        Assert.Equal(Math.Ceiling(42 * 1000.0 / 60.0), attack.DurationMs); // dagger charge 40 + 2
        Assert.Equal("KeyW", playtest.InspectAction("forward").Key);
        Assert.Equal("Digit3", playtest.InspectAction("hotbar-3").Key);

        long ticks = session.ElapsedTicks;
        using JsonDocument looked = JsonDocument.Parse(playtest.Look(90, 0).Message);
        Assert.Equal(90, looked.RootElement.GetProperty("player").GetProperty("yawDegrees").GetDouble(), 1);
        Assert.Equal(ticks, session.ElapsedTicks);
        Assert.Equal("Playing", looked.RootElement.GetProperty("runPhase").GetString());
    }

    [Fact]
    public void A_shipped_first_floor_is_decorated_from_its_theme()
    {
        RunSession session = NewRun();
        Assert.NotEmpty(session.Decorations);
        Assert.All(session.Decorations, decor => Assert.StartsWith("decor.", decor.SpriteId));
        Assert.Contains(session.Decorations, decor => decor.SpriteId.StartsWith("decor.sewer.", StringComparison.Ordinal));
    }
}
