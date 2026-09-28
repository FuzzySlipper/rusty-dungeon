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
    private readonly Dictionary<ulong, Appearance> _actorAppearances = new();
    private MeshResource? _levelMesh;
    private Appearance? _levelAppearance;
    private ulong _loadedLevelRevision;
    private bool _disposed;

    public DelveSceneRenderer(IEngineContext engine)
    {
        _engine = engine;
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
            _actorAppearances[stale].Dispose();
            _actorAppearances.Remove(stale);
        }

        _engine.Graphics.PublishSnapshot(facts.ToArray());
    }

    private void LoadLevel(DungeonLevel level, string theme)
    {
        if (_levelAppearance is not null)
        {
            _levelAppearance.Dispose();
            _levelAppearance = null;
        }

        _levelMesh?.Dispose();
        foreach (Appearance appearance in _actorAppearances.Values)
        {
            appearance.Dispose();
        }

        _actorAppearances.Clear();

        (Vector3[] positions, Vector3[] normals, Vector2[] uvs, Color[] colors, uint[] indices) = LevelMesh.Build(level, theme);
        _levelMesh = _engine.Graphics.CreateMeshResource(new MeshResourceCreateRequest(
            positions,
            normals,
            uvs,
            colors,
            indices,
            new[] { new MeshGroup(0, 0, (uint)indices.Length) },
            new[] { new MeshMaterialBinding(0, _material) }));
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (Appearance appearance in _actorAppearances.Values)
        {
            appearance.Dispose();
        }

        _actorAppearances.Clear();
        _levelAppearance?.Dispose();
        _levelMesh?.Dispose();
        _engine.CameraView.ClearActiveCamera(new ClearActiveCameraRequest(0));
        _camera.Dispose();
        _material.Dispose();
    }
}
