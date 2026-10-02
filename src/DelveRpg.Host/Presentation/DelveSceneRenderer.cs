using System.Numerics;
using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Ai;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// The first-person scene: one retained mesh per floor (floor and ceiling
/// quads, wall boxes), one camera-facing sprite per monster and ground item,
/// and the wielded weapon in the Engine's viewmodel layer. Geometry is rebuilt
/// when the run moves to a new floor; actor facts are republished every
/// update through the Engine's one snapshot path.
///
/// Delver tesselates chunks and draws entities as light-mapped billboards
/// ([donor] gfx/GlRenderer.java); this port keeps that composition with
/// Y-locked Engine sprites over the staged donor sheets (unlit, no light map).
/// An id with no staged art falls back to an authored placeholder plate.
/// </summary>
public sealed class DelveSceneRenderer : IDisposable
{
    private const ulong LevelObjectId = 1;
    private const ulong MonsterObjectBase = 1_000;
    private const ulong ItemObjectBase = 2_000_000;
    private const ulong WeaponObjectId = 3_000_000_000;
    private const ulong AmbientLightId = 4_000_000_000;
    private const ulong WallTorchLightBase = 4_100_000_000;
    private const ulong HandLightId = 4_000_000_002;
    private const ulong WallTorchObjectBase = 5_000_000_000;
    private const ulong TorchLightId = 4_000_000_001;

    private readonly IEngineContext _engine;
    private readonly Camera _camera;
    private readonly Light _ambient;
    private readonly Light _torch;
    private Light? _handLight;
    private readonly List<Light> _wallTorchLights = new();
    private IReadOnlyList<WallTorch> _wallTorches = Array.Empty<WallTorch>();
    private readonly Material _material;
    private readonly DelveArtAssets _art;
    private readonly DelveSpriteAssets _sprites;
    private readonly IRulesCatalog _rules;
    private readonly Dictionary<ulong, Appearance> _actorAppearances = new();
    private readonly Dictionary<ulong, uint> _spriteFrames = new();
    private string? _weaponSpriteId;
    private readonly List<Appearance> _retiredAppearances = new();
    private readonly List<MeshResource> _retiredMeshes = new();
    private MeshResource? _levelMesh;
    private Appearance? _levelAppearance;
    private ulong _loadedLevelRevision;
    private bool _disposed;

    public DelveSceneRenderer(
        IEngineContext engine,
        IRulesCatalog rules,
        Func<string, string?> readText,
        Func<string, bool> contentExists)
    {
        _engine = engine;
        _rules = rules;
        _art = DelveArtAssets.Load(engine, readText, contentExists);
        _sprites = DelveSpriteAssets.Load(engine, readText, contentExists);
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

        // The Engine's neutral world rig is disabled (DelveRpg.Host.csproj):
        // the dungeon is lit by point lights over this faint, cool ambient,
        // which only keeps unlit corners from going pure black — the donor's
        // light maps fall to near-dark away from torches.
        _ambient = engine.Graphics.CreateLight(new LightRequest(
            AmbientLightId,
            false,
            0,
            new LightDescriptor(
                LightKind.Ambient,
                new Vector3(0.55f, 0.6f, 0.8f),
                AmbientIntensity,
                true,
                Vector3.Zero,
                -Vector3.UnitY,
                false,
                0f,
                0f,
                0f,
                0f,
                LightShadowIntent.Disabled)));

        _torch = engine.Graphics.CreateLight(TorchRequest(Vector3.Zero));

        engine.CameraView.SetBackgroundColor(new SetBackgroundColorRequest(new Color(0.02f, 0.02f, 0.05f, 1f)));
    }

    /// <summary>Publish camera and actor facts for one update.</summary>
    public void Render(RunSession session, GameTuning tuning)
    {
        float eyeHeight = tuning.EyeHeight;
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

        _engine.Graphics.UpdateLight(new LightUpdateRequest(
            _torch, TorchRequest(new Vector3(player.X, eyeHeight, player.Y))));

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
            if (_sprites.SpriteFor(monster.Archetype.SpriteId) is DelveSprite sprite)
            {
                Appearance appearance = RequireAppearance(objectId, () => CreateWorldSprite(sprite));
                ShowFrame(objectId, appearance, sprite, MonsterFrame(sprite.Definition, monster, session.ElapsedTicks));
                facts.Add(new AppearanceFact(
                    objectId,
                    false,
                    0,
                    new Transform(new Vector3(monster.Body.X, sprite.Definition.Lift, monster.Body.Y), Quaternion.Identity, Vector3.One),
                    appearance,
                    true,
                    RenderLayer.Scene));
                continue;
            }

            Appearance plate = RequireAppearance(
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
                plate,
                true,
                RenderLayer.Scene));
        }

        foreach (GroundItem item in session.GroundItems)
        {
            ulong objectId = ItemObjectBase + (ulong)((item.Y * 10_000) + item.X);
            live.Add(objectId);
            string? spriteId = _rules.Item(item.Item.ArchetypeId)?.SpriteId;
            if (spriteId is not null && _sprites.SpriteFor(spriteId) is DelveSprite sprite)
            {
                Appearance appearance = RequireAppearance(objectId, () => CreateWorldSprite(sprite));
                ShowFrame(objectId, appearance, sprite, (uint)sprite.Definition.Frame);
                facts.Add(new AppearanceFact(
                    objectId,
                    false,
                    0,
                    new Transform(new Vector3(item.X + 0.5f, sprite.Definition.Lift, item.Y + 0.5f), Quaternion.Identity, Vector3.One),
                    appearance,
                    true,
                    RenderLayer.Scene));
                continue;
            }

            Appearance plate = RequireAppearance(
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
                plate,
                true,
                RenderLayer.Scene));
        }

        AddWallTorches(session.ElapsedTicks, facts, live);
        AddHeldWeapon(session, tuning, facts, live);

        foreach (ulong stale in _actorAppearances.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _retiredAppearances.Add(_actorAppearances[stale]);
            _actorAppearances.Remove(stale);
            _spriteFrames.Remove(stale);
        }

        // The hand light hangs off the weapon object, which must be in the
        // published scene before the light is made and while it lives.
        if (_handLight is not null && !live.Contains(WeaponObjectId))
        {
            _handLight.Dispose();
            _handLight = null;
        }

        _engine.Graphics.PublishSnapshot(facts.ToArray());
        if (_handLight is null && live.Contains(WeaponObjectId))
        {
            _handLight = _engine.Graphics.CreateLight(HandLightRequest());
        }

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
        _spriteFrames.Clear();

        foreach (Light light in _wallTorchLights)
        {
            light.Dispose();
        }

        _wallTorchLights.Clear();
        _wallTorches = TorchPlacement.Place(level);
        for (int i = 0; i < _wallTorches.Count; i++)
        {
            _wallTorchLights.Add(_engine.Graphics.CreateLight(PointLight(
                WallTorchLightBase + (ulong)i,
                _wallTorches[i].LightPosition,
                WallTorchColor,
                WallTorchIntensity,
                WallTorchRange)));
        }

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

    /// <summary>
    /// The wielded weapon, drawn camera-local in the viewmodel layer (the
    /// donor's drawHeldItem, [donor] gfx/GlRenderer.java): it sits low and to
    /// the right, rises and pulls back while a swing charges, and sweeps
    /// across through the attack cooldown. Without a weapon or its art the
    /// hand is empty.
    /// </summary>
    private void AddHeldWeapon(RunSession session, GameTuning tuning, List<AppearanceFact> facts, HashSet<ulong> live)
    {
        PlayerState player = session.Player;
        ItemArchetype? weapon = player.Equipment.WeaponItemId is string weaponId ? _rules.Item(weaponId) : null;
        DelveSprite? sprite = weapon is null ? null : _sprites.SpriteFor(weapon.SpriteId);
        if (weapon is null || sprite is null)
        {
            return;
        }

        if (_weaponSpriteId != weapon.SpriteId && _actorAppearances.Remove(WeaponObjectId, out Appearance? previous))
        {
            // A new weapon's sprite replaces the old one after this publish.
            _retiredAppearances.Add(previous);
            _spriteFrames.Remove(WeaponObjectId);
        }

        _weaponSpriteId = weapon.SpriteId;
        live.Add(WeaponObjectId);
        Appearance appearance = RequireAppearance(WeaponObjectId, () => _engine.Graphics.CreateSpriteFromAtlas(new SpriteFromAtlasRequest(
            sprite.Atlas,
            (uint)sprite.Definition.Frame,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.42f, 0.42f),
            BillboardMode.None,
            SpriteSizeMode.World,
            0,
            SpriteDepthPolicy.Default,
            new Color(1f, 1f, 1f, 1f),
            sprite.Material)));
        ShowFrame(WeaponObjectId, appearance, sprite, (uint)sprite.Definition.Frame);

        HeldPose pose = HeldWeaponPose(
            player.AttackCharge / (float)Math.Max(1, weapon.ChargeTicks),
            player.Body.AttackCooldownRemaining / (float)Math.Max(1, tuning.AttackCooldownTicks));
        float roll = (sprite.Definition.HeldRoll + pose.RollDegrees) * (MathF.PI / 180f);
        facts.Add(new AppearanceFact(
            WeaponObjectId,
            false,
            0,
            new Transform(pose.Position, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll), Vector3.One),
            appearance,
            true,
            RenderLayer.Viewmodel));
    }

    /// <summary>
    /// Wall torches: a fullbright looping flame sprite and a point light each
    /// ([donor] entities/Torch.java: sprite atlas cells 32-39 over 30 ticks,
    /// lightColor (1, 0.8, 0.2), range 3.2). Without the torch sprite the
    /// lights still burn.
    /// </summary>
    private void AddWallTorches(long elapsedTicks, List<AppearanceFact> facts, HashSet<ulong> live)
    {
        if (_sprites.SpriteFor(WallTorchSpriteId) is not DelveSprite sprite || sprite.Definition.Loop is not DelveSpriteAnimation loop)
        {
            return;
        }

        for (int i = 0; i < _wallTorches.Count; i++)
        {
            ulong objectId = WallTorchObjectBase + (ulong)i;
            live.Add(objectId);
            Appearance appearance = RequireAppearance(objectId, () => CreateWorldSprite(sprite));
            ShowFrame(objectId, appearance, sprite, (uint)loop.FrameAt(elapsedTicks + (i * 7)));
            facts.Add(new AppearanceFact(
                objectId,
                false,
                0,
                new Transform(_wallTorches[i].SpritePosition, Quaternion.Identity, Vector3.One),
                appearance,
                true,
                RenderLayer.Scene));
        }
    }

    /// <summary>
    /// The player's torch: a warm point light carried at eye height, the
    /// donor's player light ([donor] entities/Player.java:173-176 torchColor
    /// (1, 0.8, 0.4) and torchRange 3, updatePlayerLight).
    /// </summary>
    private static LightRequest TorchRequest(Vector3 position) =>
        PointLight(TorchLightId, position, new Vector3(1f, 0.8f, 0.4f), TorchIntensity, TorchRange);

    /// <summary>
    /// The viewmodel rig is disabled too (DelveRpg.Host.csproj): the held
    /// weapon is lit by the torch in the hand, a light parented to the weapon
    /// so it rides in the camera-local viewmodel layer, as the donor tints the
    /// held item by the light where the player stands.
    /// </summary>
    private static LightRequest HandLightRequest() => new(
        HandLightId,
        true,
        WeaponObjectId,
        new LightDescriptor(
            LightKind.Point,
            new Vector3(1f, 0.8f, 0.4f),
            HandLightIntensity,
            true,
            new Vector3(-0.35f, 0.35f, 0.45f),
            -Vector3.UnitY,
            true,
            3f,
            LightDecay,
            0f,
            0f,
            LightShadowIntent.Disabled));

    private static LightRequest PointLight(ulong id, Vector3 position, Vector3 color, float intensity, float range) => new(
        id,
        false,
        0,
        new LightDescriptor(
            LightKind.Point,
            color,
            intensity,
            true,
            position,
            -Vector3.UnitY,
            true,
            range,
            LightDecay,
            0f,
            0f,
            LightShadowIntent.Disabled));

    private const string WallTorchSpriteId = "decor.torch";
    private const float AmbientIntensity = 0.06f;
    private const float TorchIntensity = 8f;
    private const float TorchRange = 3.5f;
    private static readonly Vector3 WallTorchColor = new(1f, 0.8f, 0.2f);
    private const float WallTorchIntensity = 8f;
    private const float WallTorchRange = 4f;
    private const float LightDecay = 1f;
    private const float HandLightIntensity = 1.5f;

    /// <summary>Camera-local weapon pose; the viewmodel camera looks down -Z.</summary>
    public readonly record struct HeldPose(Vector3 Position, float RollDegrees);

    /// <summary>
    /// Pose for a charge fraction (0..1, wind-up) and the remaining fraction
    /// of the post-swing cooldown (1 just after the blow, 0 at rest).
    /// </summary>
    public static HeldPose HeldWeaponPose(float charge, float cooldownRemaining)
    {
        charge = Math.Clamp(charge, 0f, 1f);
        float swing = MathF.Sin(Math.Clamp(cooldownRemaining, 0f, 1f) * MathF.PI);
        var position = new Vector3(
            0.30f - (0.28f * swing),
            -0.28f + (0.10f * charge) - (0.04f * swing),
            -0.62f + (0.10f * charge) - (0.12f * swing));
        return new HeldPose(position, (25f * charge) - (75f * swing));
    }

    private Appearance CreateWorldSprite(DelveSprite sprite) =>
        _engine.Graphics.CreateSpriteFromAtlas(new SpriteFromAtlasRequest(
            sprite.Atlas,
            (uint)sprite.Definition.Frame,
            new Vector2(0.5f, 0f),
            new Vector2(sprite.Definition.Size, sprite.Definition.Size),
            BillboardMode.Cylindrical,
            SpriteSizeMode.World,
            0,
            SpriteDepthPolicy.Default,
            new Color(1f, 1f, 1f, 1f),
            sprite.Material));

    /// <summary>
    /// Switch a sprite's cell only when it changes; the update applies in
    /// place. A sprite is created on its resting cell.
    /// </summary>
    private void ShowFrame(ulong objectId, Appearance appearance, DelveSprite sprite, uint frame)
    {
        uint shown = _spriteFrames.TryGetValue(objectId, out uint current) ? current : (uint)sprite.Definition.Frame;
        if (shown != frame)
        {
            _engine.Graphics.SetSpriteFrame(new SpriteFrameUpdateRequest(appearance, frame));
        }

        _spriteFrames[objectId] = frame;
    }

    /// <summary>
    /// The donor's frame choice, attack over walk ([donor] entities/Monster.java):
    /// the attack cells play out right after a blow, the walk cycle loops while
    /// the monster is on the move, and an idle monster rests on its first cell.
    /// </summary>
    public static uint MonsterFrame(DelveSpriteDefinition definition, MonsterState monster, long elapsedTicks)
    {
        int cooldown = monster.Archetype.AttackCooldownTicks;
        int sinceBlow = cooldown - monster.Body.AttackCooldownRemaining;
        if (definition.Attack is DelveSpriteAnimation attack
            && monster.Body.AttackCooldownRemaining > 0
            && sinceBlow < Math.Min(attack.Speed, cooldown))
        {
            return (uint)attack.FrameAt(sinceBlow);
        }

        if (definition.Walk is DelveSpriteAnimation walk && monster.BrainState != MonsterBrainState.Idle)
        {
            return (uint)walk.FrameAt(elapsedTicks + monster.Body.Id);
        }

        return (uint)definition.Frame;
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

        // The hand light goes before its parent weapon leaves the scene.
        if (_handLight is not null)
        {
            TryDispose(_handLight.Dispose, failures);
            _handLight = null;
        }

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
        _spriteFrames.Clear();
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

        foreach (Light light in _wallTorchLights)
        {
            TryDispose(light.Dispose, failures);
        }

        _wallTorchLights.Clear();

        TryDispose(_torch.Dispose, failures);
        TryDispose(_ambient.Dispose, failures);
        TryDispose(() => _engine.CameraView.ClearActiveCamera(new ClearActiveCameraRequest(0)), failures);
        TryDispose(_camera.Dispose, failures);
        // Sprite atlases go after every sprite appearance made from them.
        TryDispose(_sprites.Dispose, failures);
        TryDispose(_art.Dispose, failures);
        TryDispose(_material.Dispose, failures);
        if (failures.Count > 0)
        {
            throw new AggregateException("Scene teardown reported failures.", failures);
        }
    }
}
