using DelveRpg.Host.Hud;
using DelveRpg.Host.Input;
using DelveRpg.Host.Presentation;
using DelveRpg.Host.Save;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver;
using DelveRpg.Rulesets.Delver.Content;
using Rusty.Engine;

namespace DelveRpg.Host;

/// <summary>
/// The product entry: explicit composition of the Delver ruleset, the Engine
/// services the run needs, and the one admitted update path. Lifecycle
/// callbacks apply run policy (save boundaries, permadeath, meta updates);
/// gameplay itself lives in the Kit session and the ruleset.
/// </summary>
public sealed class DelveProduct : IEngineProduct
{
    private const uint MaxCatchUpSteps = 4;
    private const string PhaseTitle = "title";
    private const string PhaseRun = "run";

    private readonly IEngineContext _engine;
    private readonly DelverRuleset _ruleset;
    private readonly DelveSaveStore _saves;
    private readonly DelveInputRouter _router = new();
    private readonly DelveSceneRenderer _renderer;
    private readonly UiStream _uiStream;
    private ulong _uiSequence;
    private long _runSeedCounter;
    private RunSession? _session;
    private ulong _savedLevelRevision;
    private string _hostPhase = PhaseTitle;
    private bool _shutdown;

    public DelveProduct(ProductCreateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _engine = context.Engine;
        DelverComposition composition = DelverComposition.Load(path =>
            context.Content.TryReadFile(path, out ProductContentFile file) ? file.ReadText() : null);
        _ruleset = new DelverRuleset(composition);
        _saves = new DelveSaveStore(_engine);
        _renderer = new DelveSceneRenderer(_engine);
        _uiStream = _engine.Ui.OpenStream(new UiStreamRequest(DelveHudProjection.StreamId, DelveHudProjection.Contract));
    }

    public void Start()
    {
        if (_shutdown)
        {
            return;
        }

        MetaProgression meta = _saves.LoadMeta();
        RunSnapshot? saved = _saves.LoadRun();
        if (saved is not null)
        {
            _session = _ruleset.ResumeSession(saved, meta);
            _savedLevelRevision = _session.LevelRevision;
            _hostPhase = PhaseRun;
        }

        PublishHud();
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (_shutdown)
        {
            return ProductUpdateResult.None;
        }

        _router.Route(update.Input);
        uint steps = Math.Max(1u, Math.Min(update.Facts.AdmittedStepCount, MaxCatchUpSteps));
        for (uint step = 0; step < steps; step++)
        {
            Step(_router.TakeTickInput());
        }

        if (_session is not null)
        {
            _renderer.Render(_session, _ruleset.Tuning.EyeHeight);
        }

        PublishHud();
        return ProductUpdateResult.None;
    }

    private void Step(RunInput input)
    {
        if (_session is null || _hostPhase == PhaseTitle)
        {
            if (input.MenuConfirm)
            {
                StartNewRun();
            }

            return;
        }

        if (_session.Phase is Kit.Session.RunPhase.Dead or Kit.Session.RunPhase.Won)
        {
            if (input.MenuConfirm)
            {
                FinishRun();
            }

            return;
        }

        _session.Tick(input);
        if (_session.LevelRevision != _savedLevelRevision)
        {
            // The donor saves at every level change; that is this run's save boundary.
            _savedLevelRevision = _session.LevelRevision;
            _saves.SaveRun(_session.Capture());
        }
    }

    private void StartNewRun()
    {
        long draw = _engine.Random.DrawKeyed(new KeyedRngRequest(
            0xD00D_F00D_D1CE_0001UL, "run-seeds", $"run:{_runSeedCounter++}", long.MinValue, long.MaxValue)).Value;
        ulong runSeed = (ulong)draw;
        _session = _ruleset.CreateSession(runSeed, _saves.LoadMeta());
        _savedLevelRevision = _session.LevelRevision;
        _hostPhase = PhaseRun;
        _saves.SaveRun(_session.Capture());
    }

    private void FinishRun()
    {
        if (_session is not null)
        {
            MetaProgression meta = _saves.LoadMeta();
            bool won = _session.Phase == Kit.Session.RunPhase.Won;
            meta = won
                ? meta with { Wins = meta.Wins + 1, Gold = meta.Gold + _session.Player.Gold }
                : meta with { Deaths = meta.Deaths + 1, Gold = meta.Gold + _session.Player.Gold };
            _saves.SaveMeta(meta);
        }

        // Permadeath: the run save dies with the run.
        _saves.DeleteRun();
        _session = null;
        _hostPhase = PhaseTitle;
    }

    public void Pause()
    {
        if (_session is not null && !_shutdown)
        {
            _saves.SaveRun(_session.Capture());
            _savedLevelRevision = _session.LevelRevision;
        }
    }

    public void Resume()
    {
    }

    public void Restart()
    {
        if (_shutdown)
        {
            return;
        }

        // Restart abandons the current run the way closing the game does.
        _saves.DeleteRun();
        _session = null;
        _hostPhase = PhaseTitle;
        PublishHud();
    }

    public void Shutdown()
    {
        if (_session is not null && !_shutdown)
        {
            _saves.SaveRun(_session.Capture());
        }

        _shutdown = true;
    }

    public void Dispose()
    {
        _shutdown = true;
        _uiStream.Dispose();
        _renderer.Dispose();
        _saves.Dispose();
    }

    private void PublishHud()
    {
        HudFacts? facts = _session?.BuildHudFacts();
        string phase = _session is null ? PhaseTitle : _session.Phase switch
        {
            Kit.Session.RunPhase.Dead => "dead",
            Kit.Session.RunPhase.Won => "won",
            Kit.Session.RunPhase.LevelUp => "levelup",
            _ => PhaseRun,
        };
        _engine.Ui.PublishProjection(new UiProjection(
            _uiStream,
            ++_uiSequence,
            DelveHudProjection.Build(phase, facts)));
    }
}
