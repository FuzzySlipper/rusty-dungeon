using DelveRpg.Host.Hud;
using DelveRpg.Host.Input;
using DelveRpg.Host.Playtest;
using DelveRpg.Host.Presentation;
using DelveRpg.Host.Save;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver;
using DelveRpg.Rulesets.Delver.Content;
using Rusty.Engine;
using Rusty.Engine.Debugging;

namespace DelveRpg.Host;

/// <summary>
/// The product entry: explicit composition of the Delver ruleset, the Engine
/// services the run needs, and the one admitted update path. Lifecycle
/// callbacks apply run policy (save boundaries, permadeath, meta updates);
/// gameplay itself lives in the Kit session and the ruleset.
/// </summary>
public sealed class DelveProduct : IEngineProduct, IDebugCommandModuleSource
{
    private const uint MaxCatchUpSteps = 4;
    private const string PhaseTitle = "title";
    private const string PhaseRun = "run";
    private const string PhaseCamp = "camp";

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
    private IReadOnlyList<string> _campStock = Array.Empty<string>();
    private int _campCursor;
    private string _campMessage = "";
    private readonly Func<string, string?> _readText;

    public DelveProduct(ProductCreateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _engine = context.Engine;
        _readText = path =>
            context.Content.TryReadFile(path, out ProductContentFile file) ? file.ReadText() : null;
        DelverComposition composition = DelverComposition.Load(_readText);
        _ruleset = new DelverRuleset(composition);
        _saves = new DelveSaveStore(_engine);
        _renderer = new DelveSceneRenderer(
            _engine,
            _ruleset.Catalog,
            _readText,
            path => context.Content.TryReadFile(path, out ProductContentFile _));
        _uiStream = _engine.Ui.OpenStream(new UiStreamRequest(DelveHudProjection.StreamId, DelveHudProjection.Contract));
    }

    /// <summary>
    /// The live-debug playtest adapter (crew-services playtest assist ops).
    /// Every command reads the current run, so a new run or a resumed save
    /// never leaves a stale delegate.
    /// </summary>
    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar)
    {
        var playtest = new DelvePlaytest(() => _session, () => _hostPhase, _ruleset.Catalog);
        registrar.Register(new PlaytestDebugModule(
            playtest.Observe,
            playtest.InspectAction,
            DelvePlaytest.Actions,
            playtest.Look));
        registrar.Register(new DelveGridDebugModule(() => _session));
    }

    public void Start()
    {
        if (_shutdown)
        {
            return;
        }

        MetaProgression meta = LoadMeta();
        RunSnapshot? saved = _saves.LoadRun();
        if (saved is not null)
        {
            _session = _ruleset.ResumeSession(saved, meta, DrawSource(saved.RunSeed));
            if (_session is null)
            {
                // A stale or finished save cannot continue; permadeath applies.
                _saves.DeleteRun();
            }
            else
            {
                _savedLevelRevision = _session.LevelRevision;
                _hostPhase = PhaseRun;
            }
        }

        PublishHud();
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (_shutdown)
        {
            return ProductUpdateResult.None;
        }

        if (update.Facts.Mode != ProductUpdateMode.Realtime
            || update.Facts.LifecycleState != ProductLifecycleState.Running)
        {
            return ProductUpdateResult.None;
        }

        _router.Route(update.Input);
        uint steps = Math.Min(update.Facts.AdmittedStepCount, MaxCatchUpSteps);
        for (uint step = 0; step < steps; step++)
        {
            Step(_router.TakeTickInput());
        }

        if (_session is not null)
        {
            _renderer.Render(_session, _ruleset.Tuning);
        }

        PublishHud();
        return ProductUpdateResult.None;
    }

    private void Step(RunInput input)
    {
        if (_hostPhase == PhaseCamp)
        {
            StepCamp(input);
            return;
        }

        if (_session is null || _hostPhase == PhaseTitle)
        {
            if (input.MenuConfirm)
            {
                EnterCamp();
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

    /// <summary>
    /// The camp between runs: a fresh stock each visit (the donor's camp shop
    /// restocks per visit), the soulbound expansions, and the way down.
    /// </summary>
    private void EnterCamp()
    {
        long draw = _engine.Random.DrawKeyed(new KeyedRngRequest(
            0xD00D_F00D_D1CE_0002UL, "camp-stock", $"camp:{_runSeedCounter}", long.MinValue, long.MaxValue)).Value;
        _campStock = Camp.RollStock(new Kit.Random.SplitMixRandom((ulong)draw), _ruleset.Catalog);
        _campCursor = 0;
        _campMessage = "";
        _hostPhase = PhaseCamp;
    }

    private void StepCamp(RunInput input)
    {
        MetaProgression meta = LoadMeta();
        IReadOnlyList<CampOffer> offers = Camp.Offers(meta, _campStock, _ruleset.Catalog);
        if (input.MenuUp)
        {
            _campCursor = Math.Max(0, _campCursor - 1);
        }

        if (input.MenuDown)
        {
            _campCursor = Math.Min(offers.Count - 1, _campCursor + 1);
        }

        if (input.MenuCancel)
        {
            _hostPhase = PhaseTitle;
            return;
        }

        if (!input.MenuConfirm)
        {
            return;
        }

        CampOffer offer = offers[Math.Clamp(_campCursor, 0, offers.Count - 1)];
        if (offer.Kind == CampOfferKind.Descend)
        {
            StartNewRun();
            return;
        }

        if (Camp.Buy(meta, offer) is not MetaProgression bought)
        {
            _campMessage = $"You cannot afford the {offer.Label}.";
            return;
        }

        _saves.SaveMeta(bought);
        if (offer.Kind == CampOfferKind.Item)
        {
            // A stock item is sold once.
            var stock = _campStock.ToList();
            stock.Remove(offer.ItemId!);
            _campStock = stock;
        }

        _campMessage = offer.Kind == CampOfferKind.Item
            ? $"You buy the {offer.Label}; it will go down with you."
            : "A warm feeling touches your soul.";
        _campCursor = Math.Min(_campCursor, Camp.Offers(bought, _campStock, _ruleset.Catalog).Count - 1);
    }

    private CampFacts BuildCampFacts()
    {
        MetaProgression meta = LoadMeta();
        IReadOnlyList<CampOffer> offers = Camp.Offers(meta, _campStock, _ruleset.Catalog);
        return new CampFacts(
            meta.Gold,
            meta.Wins,
            meta.Deaths,
            meta.HotbarSize,
            meta.BackpackSize,
            offers.Select(offer => new CampOfferFacts(offer.Label, offer.Cost, offer.Kind == CampOfferKind.Descend || offer.Cost <= meta.Gold)).ToList(),
            Math.Clamp(_campCursor, 0, offers.Count - 1),
            (meta.Stash ?? []).Select(stashed => _ruleset.Catalog.Item(stashed.ItemId)?.DisplayName ?? stashed.ItemId).ToList(),
            _campMessage);
    }

    private MetaProgression LoadMeta() => _saves.LoadMeta(_ruleset.NewPlayer());

    private void StartNewRun()
    {
        long draw = _engine.Random.DrawKeyed(new KeyedRngRequest(
            0xD00D_F00D_D1CE_0001UL, "run-seeds", $"run:{_runSeedCounter++}", long.MinValue, long.MaxValue)).Value;
        ulong runSeed = (ulong)draw;
        MetaProgression meta = LoadMeta();
        _session = _ruleset.CreateSession(runSeed, meta, DrawSource(runSeed));

        // The stash went down with the run.
        _saves.SaveMeta(meta with { Stash = [] });
        _savedLevelRevision = _session.LevelRevision;
        _hostPhase = PhaseRun;
        _saves.SaveRun(_session.Capture());
    }

    private void FinishRun()
    {
        if (_session is not null)
        {
            MetaProgression meta = LoadMeta();
            bool won = _session.Phase == Kit.Session.RunPhase.Won;
            // Whatever the run ends with goes back in the purse, death or not.
            meta = won
                ? meta with { Wins = meta.Wins + 1, Gold = _session.Player.Gold }
                : meta with { Deaths = meta.Deaths + 1, Gold = _session.Player.Gold };
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

    /// <summary>Live run draws come from the Engine's keyed random service.</summary>
    private KeyedRandomSource DrawSource(ulong runSeed) =>
        new(_engine.Random, runSeed, $"run-draws:{runSeed}");

    private void PublishHud()
    {
        HudFacts? facts = _hostPhase == PhaseCamp ? null : _session?.BuildHudFacts();
        CampFacts? camp = _hostPhase == PhaseCamp ? BuildCampFacts() : null;
        string phase = _hostPhase == PhaseCamp ? PhaseCamp : _session is null ? PhaseTitle : _session.Phase switch
        {
            Kit.Session.RunPhase.Dead => "dead",
            Kit.Session.RunPhase.Won => "won",
            Kit.Session.RunPhase.LevelUp => "levelup",
            _ => PhaseRun,
        };
        _engine.Ui.PublishProjection(new UiProjection(
            _uiStream,
            ++_uiSequence,
            DelveHudProjection.Build(phase, facts, camp)));
    }
}
