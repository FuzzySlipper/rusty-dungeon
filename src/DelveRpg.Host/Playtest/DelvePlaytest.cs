using System.Text;
using System.Text.Json;
using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Rusty.Engine.Debugging;

namespace DelveRpg.Host.Playtest;

/// <summary>
/// The playtest adapter the Engine's <see cref="PlaytestDebugModule"/> calls:
/// compact facts read from the live run, ordinary input bindings with their
/// current timing and eligibility, and look through the run's own rules.
/// Queries never advance time or act; the normal input path still decides
/// what an action does.
/// </summary>
public sealed class DelvePlaytest
{
    public static readonly IReadOnlyList<string> Actions =
    [
        "forward", "back", "left", "right", "use", "attack", "quick-attack", "confirm",
        "hotbar-1", "hotbar-2", "hotbar-3", "hotbar-4", "hotbar-5", "hotbar-6",
    ];

    private const double MoveMs = 200;
    private const double TapMs = 100;
    private const float NearbyTiles = 12f;

    private readonly Func<RunSession?> _session;
    private readonly Func<string> _hostPhase;
    private readonly IRulesCatalog _rules;

    public DelvePlaytest(Func<RunSession?> session, Func<string> hostPhase, IRulesCatalog rules)
    {
        _session = session;
        _hostPhase = hostPhase;
        _rules = rules;
    }

    public PlaytestAction InspectAction(string id)
    {
        if (!Actions.Contains(id))
        {
            return new PlaytestAction(id, "", 0, false, false, "unknown-action");
        }

        RunSession? session = _session();
        bool playing = session is { Phase: RunPhase.Playing };
        if (id == "confirm")
        {
            return new PlaytestAction(id, "Enter", TapMs, false, !playing || session!.Phase == RunPhase.LevelUp,
                playing ? "only on the title, level-up, death or victory screens" : null);
        }

        string? reason = session is null ? "no run: confirm on the title" : playing ? null : $"run phase {session.Phase}";
        ItemArchetype? weapon = session?.Player.Equipment.WeaponItemId is string weaponId ? _rules.Item(weaponId) : null;
        return id switch
        {
            "forward" => new PlaytestAction(id, "KeyW", MoveMs, true, reason is null, reason),
            "back" => new PlaytestAction(id, "KeyS", MoveMs, true, reason is null, reason),
            "left" => new PlaytestAction(id, "KeyA", MoveMs, true, reason is null, reason),
            "right" => new PlaytestAction(id, "KeyD", MoveMs, true, reason is null, reason),
            "use" => new PlaytestAction(id, "KeyE", TapMs, false, reason is null, reason),
            "attack" or "quick-attack" => AttackAction(id, session, weapon, reason),
            _ => new PlaytestAction(id, "Digit" + id["hotbar-".Length..], TapMs, false, reason is null, reason),
        };
    }

    /// <summary>
    /// A full-charge attack holds the button for the weapon's whole charge;
    /// a quick one taps. Either is refused with no weapon (there is no
    /// unarmed attack) or while the last swing's wait runs.
    /// </summary>
    private static PlaytestAction AttackAction(string id, RunSession? session, ItemArchetype? weapon, string? reason)
    {
        if (reason is null && weapon is null)
        {
            reason = "no weapon wielded";
        }
        else if (reason is null && session!.Player.Body.AttackCooldownRemaining > 0)
        {
            reason = $"recovering: {session.Player.Body.AttackCooldownRemaining} ticks";
        }

        double ms = id == "attack" && weapon is not null
            ? Math.Min(2000, Math.Ceiling((weapon.ChargeTicks + 2) * 1000.0 / 60.0))
            : TapMs;
        return new PlaytestAction(id, "Primary", ms, true, reason is null, reason, weapon?.Id);
    }

    public DebugCommandResult Look(double yawDegrees, double pitchDegrees)
    {
        if (!double.IsFinite(yawDegrees) || !double.IsFinite(pitchDegrees))
        {
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Look requires finite degrees.");
        }

        _session()?.Look((float)yawDegrees, (float)pitchDegrees);
        return Observe();
    }

    public DebugCommandResult Observe()
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("hostPhase", _hostPhase());
            RunSession? session = _session();
            if (session is null)
            {
                w.WriteString("note", "Title screen: the confirm action starts a run.");
                w.WriteEndObject();
            }
            else
            {
                WriteRun(w, session);
                w.WriteEndObject();
            }
        }

        return DebugCommandResult.Success(Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private void WriteRun(Utf8JsonWriter w, RunSession session)
    {
        ActorState body = session.Player.Body;
        w.WriteString("runPhase", session.Phase.ToString());
        w.WriteNumber("elapsedTicks", session.ElapsedTicks);
        w.WriteStartObject("axes");
        w.WriteString("units", "tiles; tile (x, y) covers [x, x+1) x [y, y+1); the world's Z is the map's Y");
        w.WriteString("yaw", "degrees clockwise from north (-Y); 90 faces +X");
        w.WriteString("position", "body centre on the floor; eye height 0.5");
        w.WriteEndObject();

        w.WriteStartObject("player");
        w.WriteNumber("x", Math.Round(body.X, 3));
        w.WriteNumber("y", Math.Round(body.Y, 3));
        w.WriteNumber("yawDegrees", Math.Round(body.Facing * 180 / Math.PI % 360, 1));
        w.WriteNumber("pitchDegrees", Math.Round(body.PitchDegrees, 1));
        w.WriteNumber("hp", body.Hp);
        w.WriteNumber("maxHp", body.MaxHp);
        w.WriteNumber("level", session.Player.Level);
        w.WriteNumber("speed", Math.Round(session.PlayerSpeed, 4));
        w.WriteNumber("visibility", Math.Round(session.Player.Visibility, 3));
        w.WriteNumber("noticeRadius", Math.Round(session.NoticeRadius, 2));
        w.WriteString("weapon", session.Player.Equipment.WeaponItemId);
        w.WriteNumber("attackCharge", session.Player.AttackCharge);
        w.WriteNumber("attackCooldown", body.AttackCooldownRemaining);
        w.WriteBoolean("swinging", session.Player.Swing is not null);
        w.WriteNumber("hurtFlash", session.Player.HurtFlashRemaining);
        w.WriteStartArray("effects");
        foreach (var effect in body.Effects.Active)
        {
            w.WriteStringValue($"{effect.Kind}:{effect.RemainingTicks}");
        }

        w.WriteEndArray();
        w.WriteEndObject();

        w.WriteStartObject("floor");
        w.WriteNumber("runIndex", session.RunIndex);
        w.WriteNumber("dungeonLevel", session.CurrentFloor.DungeonLevel);
        w.WriteString("section", session.CurrentFloor.SectionName);
        w.WriteNumber("revision", session.LevelRevision);
        w.WriteEndObject();

        w.WriteStartArray("monsters");
        foreach (MonsterState monster in session.Monsters.OrderBy(m => Distance(body, m.Body.X, m.Body.Y)))
        {
            float distance = Distance(body, monster.Body.X, monster.Body.Y);
            if (distance > NearbyTiles)
            {
                continue;
            }

            w.WriteStartObject();
            w.WriteNumber("id", monster.Body.Id);
            w.WriteString("kind", monster.Archetype.Id);
            w.WriteString("state", monster.IsDying ? "dying" : monster.BrainState.ToString());
            w.WriteNumber("hp", monster.Body.Hp);
            w.WriteNumber("distance", Math.Round(distance, 2));
            w.WriteNumber("bearingDegrees", Math.Round(Bearing(body, monster.Body.X, monster.Body.Y), 1));
            w.WriteBoolean("windingUp", monster.AttackWindupRemaining > 0);
            w.WriteBoolean("flinching", monster.HurtTicksRemaining > 0);
            w.WriteEndObject();
        }

        w.WriteEndArray();

        w.WriteStartArray("groundItems");
        foreach (GroundItem item in session.GroundItems.OrderBy(i => Distance(body, i.X + 0.5f, i.Y + 0.5f)).Take(8))
        {
            w.WriteStartObject();
            w.WriteString("item", item.Item.ArchetypeId);
            w.WriteString("name", session.ItemName(item.Item));
            w.WriteNumber("count", item.Item.Count);
            w.WriteNumber("tileX", item.X);
            w.WriteNumber("tileY", item.Y);
            w.WriteNumber("distance", Math.Round(Distance(body, item.X + 0.5f, item.Y + 0.5f), 2));
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteNumber("keys", session.Player.Keys);
        w.WriteStartArray("features");
        foreach (SpikeTrap spikes in session.Spikes.Where(t => Distance(body, t.TileX + 0.5f, t.TileY + 0.5f) <= NearbyTiles))
        {
            WriteFeature(w, body, "spikes", spikes.TileX + 0.5f, spikes.TileY + 0.5f, spikes.Phase.ToString());
        }

        foreach (TouchTrigger plate in session.Triggers.Where(t => t.IsPlate && Distance(body, t.TileX + 0.5f, t.TileY + 0.5f) <= NearbyTiles))
        {
            WriteFeature(w, body, "pressure-plate", plate.TileX + 0.5f, plate.TileY + 0.5f, plate.Pressed ? "pressed" : "up");
        }

        foreach (Pot pot in session.Pots.Where(p => Distance(body, p.X, p.Y) <= NearbyTiles))
        {
            WriteFeature(w, body, "pot", pot.X, pot.Y, pot.Kind.ToString());
        }

        for (int y = Math.Max(0, body.TileY - 12); y <= Math.Min(session.Level.Height - 1, body.TileY + 12); y++)
        {
            for (int x = Math.Max(0, body.TileX - 12); x <= Math.Min(session.Level.Width - 1, body.TileX + 12); x++)
            {
                if (session.Level.At(x, y).Kind == TileKind.DoorLocked)
                {
                    WriteFeature(w, body, "locked-door", x + 0.5f, y + 0.5f, "locked");
                }
            }
        }

        w.WriteEndArray();
        w.WriteString("featureNote", "Hidden tripwires are not listed (a player would not see them).");
        w.WriteNumber("projectiles", session.Projectiles.Count);
        w.WriteNumber("corpses", session.Corpses.Count);
        w.WriteStartArray("messages");
        foreach (RunMessage message in session.Messages.Reverse().Take(5))
        {
            w.WriteStringValue(message.Text);
        }

        w.WriteEndArray();
        w.WriteString("bearingMeaning", "relative to the player's facing: 0 ahead, positive to the right");
        w.WriteString("attackSemantics", "Holding charges; the release swings, shoots or zaps. A swing's blow lands partway through it (about 10-20 ticks), so advance a little after an attack action to see the hit. Monsters wind up before their blows (windingUp); stepping back dodges.");
    }

    private static void WriteFeature(Utf8JsonWriter w, ActorState body, string kind, float x, float y, string state)
    {
        w.WriteStartObject();
        w.WriteString("kind", kind);
        w.WriteString("state", state);
        w.WriteNumber("tileX", (int)MathF.Floor(x));
        w.WriteNumber("tileY", (int)MathF.Floor(y));
        w.WriteNumber("distance", Math.Round(Distance(body, x, y), 2));
        w.WriteNumber("bearingDegrees", Math.Round(Bearing(body, x, y), 1));
        w.WriteEndObject();
    }

    private static float Distance(ActorState body, float x, float y) =>
        MathF.Sqrt(((x - body.X) * (x - body.X)) + ((y - body.Y) * (y - body.Y)));

    private static double Bearing(ActorState body, float x, float y)
    {
        double absolute = Math.Atan2(x - body.X, -(y - body.Y));
        double relative = (absolute - body.Facing) * 180 / Math.PI;
        relative = ((relative % 360) + 540) % 360 - 180;
        return relative;
    }
}
