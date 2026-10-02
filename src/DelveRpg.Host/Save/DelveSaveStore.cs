using System.Text.Json.Serialization;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Session;
using Rusty.Engine;
using Rusty.Engine.Persistence;

namespace DelveRpg.Host.Save;

/// <summary>
/// Run and meta persistence through the Engine's managed state store. The run
/// blob is the donor's save boundary (player plus current floor); it is
/// deleted when the run ends, because death and victory are permanent.
/// </summary>
public sealed class DelveSaveStore : IDisposable
{
    public const string RunScope = "delverpg.runs";
    public const string RunKey = "run/current";
    public const string MetaScope = "delverpg.meta";
    public const string MetaKey = "meta/progression";

    private readonly ProductStateStore<RunSnapshot> _runs;
    private readonly ProductStateStore<MetaProgression> _meta;

    public DelveSaveStore(IEngineContext engine)
    {
        _runs = new ProductStateStore<RunSnapshot>(
            engine, RunScope, new JsonProductStateCodec<RunSnapshot>(DelveSaveJsonContext.Default.RunSnapshot));
        _meta = new ProductStateStore<MetaProgression>(
            engine, MetaScope, new JsonProductStateCodec<MetaProgression>(DelveSaveJsonContext.Default.MetaProgression));
    }

    public void SaveRun(RunSnapshot snapshot) =>
        _runs.Save(RunKey, snapshot, PersistenceRevisionGuard.Any, 0);

    public RunSnapshot? LoadRun()
    {
        ProductStateLoad<RunSnapshot> loaded = _runs.Load(RunKey);
        return loaded.Present ? loaded.State : null;
    }

    public void DeleteRun() => _runs.Delete(RunKey, PersistenceRevisionGuard.Any, 0);

    /// <summary>The saved record, or <paramref name="newPlayer"/> when there is none yet.</summary>
    public MetaProgression LoadMeta(MetaProgression newPlayer)
    {
        ProductStateLoad<MetaProgression> loaded = _meta.Load(MetaKey);
        return loaded.Present && loaded.State is not null ? loaded.State : newPlayer;
    }

    public void SaveMeta(MetaProgression meta) =>
        _meta.Save(MetaKey, meta, PersistenceRevisionGuard.Any, 0);

    public void Dispose()
    {
        _runs.Dispose();
        _meta.Dispose();
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RunSnapshot))]
[JsonSerializable(typeof(MetaProgression))]
public sealed partial class DelveSaveJsonContext : JsonSerializerContext;
