using System.Numerics;
using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// The first-person scene: one retained mesh per floor (tiles as floor quads
/// and wall boxes) and one billboarded-plate primitive per actor. Geometry is
/// rebuilt when the run moves to a new floor; actor facts are republished every
/// update through the Engine's one snapshot path.
///
/// Delver tesselates chunks and draws entities as light-mapped billboard
/// decals; this port keeps the composition (static floor geometry plus
/// camera-facing actor plates) and leaves textured art and baked lighting to a
/// later content slice. Colors here are authored placeholders, never donor
/// rips.
/// </summary>
public sealed class DelveSceneRenderer : IDisposable
{
    private const ulong LevelObjectId = 1;
    private const ulong MonsterObjectBase = 1_000;
    private const ulong ItemObjectBase = 2_000_000;

    private readonly IEngineContext _engine;
    private readonly Camera _camera;
    private readonly Material _material;
    private readonly DelveArtAssets _art;
    private readonly Dictionary<ulong, Appearance> _actorAppearances = new();
    private readonly List<Appearance> _retiredAppearances = new();
    private readonly List<MeshResource> _retiredMeshes = new();
    private MeshResource? _levelMesh;
    private Appearance? _levelAppearance;
    private ulong _loadedLevelRevision;
    private bool _disposed;

    public DelveSceneRenderer(IEngineContext engine, Func<string, string?> readText, Func<string, bool> contentExists)
    {
        _engine = engine;
        _art = DelveArtAssets.Load(engine, readText, contentExists);
        _material = engine.Graphics.CreateMaterial(new MaterialRequest(
            new Color(1f, 1f, 1f, 1f),
            default,
            0.9f,
            new Color(1f, 1f, 1f, 1f),
            Vector3.Zero,
            0f,
            false));

        var projection = new CameraProjection(CameraProjectionKind.Perspective, 75.0, 0.0, 0.05, 120.0);
        _camera = engine.CameraView.CreateCamera(new CameraDescriptor(
            new CameraPose(Vector3.Zero, 0.0, 0.0),
            CameraBasisMode.Derived,
            default,
            projection,
            new CameraViewport(0, 0, 1, 1)));
        engine.CameraView.SetActiveCamera(_camera);
        engine.CameraView.SetBackgroundColor(new SetBackgroundColorRequest(new Color(0.02f, 0.02f, 0.05f, 1f)));
    }

    /// <summary>Publish camera and actor facts for one update.</summary>
    public void Render(RunSession session, float eyeHeight)
    {
        if (_disposed)
        {
            return;
        }

        if (_loadedLevelRevision != session.LevelRevision)
        {
            LoadLevel(session.Level, session.CurrentFloor.Theme);
            _loadedLevelRevision = session.LevelRevision;
        }

        ActorState player = session.Player.Body;
        _engine.CameraView.UpdateCamera(new CameraUpdateRequest(
            _camera,
            new CameraDescriptor(
                new CameraPose(
                    new Vector3(player.X, eyeHeight, player.Y),
                    player.PitchDegrees,
                    player.Facing * (180.0 / Math.PI)),
                CameraBasisMode.Derived,
                default,
                new CameraProjection(CameraProjectionKind.Perspective, 75.0, 0.0, 0.05, 120.0),
                new CameraViewport(0, 0, 1, 1))));

        var facts = new List<AppearanceFact>();
        var live = new HashSet<ulong>();

        if (_levelAppearance is not null)
        {
            facts.Add(new AppearanceFact(
                LevelObjectId,
                false,
                0,
                new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
                _levelAppearance,
                true,
                RenderLayer.Scene));
        }

        foreach (MonsterState monster in session.Monsters)
        {
            ulong objectId = MonsterObjectBase + (ulong)monster.Body.Id;
            live.Add(objectId);
            Appearance appearance = RequireAppearance(
                objectId,
                () => _engine.Graphics.CreatePrimitive(new PrimitiveAppearanceRequest(
                    PrimitiveGeometry.Cube, false, MonsterTint(monster.Archetype.Id))));
            facts.Add(new AppearanceFact(
                objectId,
                false,
                0,
                new Transform(
                    new Vector3(monster.Body.X, 0.45f, monster.Body.Y),
                    Quaternion.Identity,
                    new Vector3(0.55f, 0.9f, 0.55f)),
                appearance,
                true,
                RenderLayer.Scene));
        }

        foreach (GroundItem item in session.GroundItems)
        {
            ulong objectId = ItemObjectBase + (ulong)((item.Y * 10_000) + item.X);
            live.Add(objectId);
            Appearance appearance = RequireAppearance(
                objectId,
                () => _engine.Graphics.CreatePrimitive(new PrimitiveAppearanceRequest(
                    PrimitiveGeometry.Cube, false, ItemTint(item.Item.ArchetypeId))));
            facts.Add(new AppearanceFact(
                objectId,
                false,
                0,
                new Transform(
                    new Vector3(item.X + 0.5f, 0.12f, item.Y + 0.5f),
                    Quaternion.Identity,
                    new Vector3(0.22f, 0.22f, 0.22f)),
                appearance,
                true,
                RenderLayer.Scene));
        }

        foreach (ulong stale in _actorAppearances.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _retiredAppearances.Add(_actorAppearances[stale]);
            _actorAppearances.Remove(stale);
        }

        _engine.Graphics.PublishSnapshot(facts.ToArray());

        // A retained appearance must leave the published snapshot before
        // disposal; the publish above is the first that excludes these.
        RetireDrain();
    }

    private void RetireDrain()
    {
        List<Exception> failures = new();
        foreach (Appearance appearance in _retiredAppearances)
        {
            TryDispose(appearance.Dispose, failures);
        }

        foreach (MeshResource mesh in _retiredMeshes)
        {
            TryDispose(mesh.Dispose, failures);
        }

        _retiredAppearances.Clear();
        _retiredMeshes.Clear();
        if (failures.Count > 0)
        {
            throw new AggregateException("Retired scene resource disposal failed.", failures);
        }
    }

    /// <summary>
    /// Swap in a new floor's geometry. The replaced resources are retired and
    /// only disposed after the next publish drops them (the publish at the end
    /// of this render).
    /// </summary>
    private void LoadLevel(DungeonLevel level, string theme)
    {
        if (_levelAppearance is not null)
        {
            _retiredAppearances.Add(_levelAppearance);
            _levelAppearance = null;
        }

        if (_levelMesh is not null)
        {
            _retiredMeshes.Add(_levelMesh);
            _levelMesh = null;
        }

        foreach (Appearance appearance in _actorAppearances.Values)
        {
            _retiredAppearances.Add(appearance);
        }

        _actorAppearances.Clear();

        LevelMesh.LevelGeometry geometry = LevelMesh.Build(level, theme, _art.RectFor);
        var bindings = new List<MeshMaterialBinding>();
        foreach ((string role, MeshGroup _) in geometry.Groups)
        {
            bindings.Add(new MeshMaterialBinding(
                (uint)LevelMesh.SlotFor(role),
                _art.MaterialFor(role) ?? _material));
        }

        _levelMesh = _engine.Graphics.CreateMeshResource(new MeshResourceCreateRequest(
            geometry.Positions,
            geometry.Normals,
            geometry.Uvs,
            geometry.Colors,
            geometry.Indices,
            geometry.Groups.Select(entry => entry.Group).ToArray(),
            bindings.ToArray()));
        _levelAppearance = _engine.Graphics.CreateMeshAppearance(_levelMesh);
    }

    private Appearance RequireAppearance(ulong objectId, Func<Appearance> create) =>
        _actorAppearances.TryGetValue(objectId, out Appearance? appearance) ? appearance : _actorAppearances[objectId] = create();

    private static Color MonsterTint(string archetypeId) => Tint(archetypeId, 0.65f, 0.22f, 0.22f);

    private static Color ItemTint(string archetypeId) => Tint(archetypeId, 0.85f, 0.72f, 0.25f);

    private static Color Tint(string id, float r, float g, float b)
    {
        int hash = StringComparer.Ordinal.GetHashCode(id);
        float jitter = ((hash % 40) + 40) % 40 / 200f;
        return new Color(Math.Clamp(r + jitter, 0f, 1f), Math.Clamp(g + jitter, 0f, 1f), Math.Clamp(b + jitter, 0f, 1f), 1f);
    }

    private static void TryDispose(Action dispose, List<Exception> failures)
    {
        try
        {
            dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<Exception> failures = new();

        // A retained appearance must leave the published snapshot before
        // disposal; publish the empty scene first.
        TryDispose(
            () => _engine.Graphics.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty),
            failures);

        foreach (Appearance appearance in _actorAppearances.Values)
        {
            TryDispose(appearance.Dispose, failures);
        }

        _actorAppearances.Clear();
        foreach (Appearance appearance in _retiredAppearances)
        {
            TryDispose(appearance.Dispose, failures);
        }

        foreach (MeshResource mesh in _retiredMeshes)
        {
            TryDispose(mesh.Dispose, failures);
        }

        _retiredAppearances.Clear();
        _retiredMeshes.Clear();
        if (_levelAppearance is not null)
        {
            TryDispose(_levelAppearance.Dispose, failures);
        }

        if (_levelMesh is not null)
        {
            TryDispose(_levelMesh.Dispose, failures);
        }

        TryDispose(() => _engine.CameraView.ClearActiveCamera(new ClearActiveCameraRequest(0)), failures);
        TryDispose(_camera.Dispose, failures);
        TryDispose(_art.Dispose, failures);
        TryDispose(_material.Dispose, failures);
        if (failures.Count > 0)
        {
            throw new AggregateException("Scene teardown reported failures.", failures);
        }
    }
}
