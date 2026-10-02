using System.Numerics;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>
/// The donor's fire flicker ([donor] entities/DynamicLight.java:95-102
/// <c>LightType.fire</c>): three slow sines each take up to a tenth off the
/// light's colour and up to a twentieth off its range, in ticks.
/// </summary>
public static class TorchFlicker
{
    /// <summary>Intensity and range multipliers <paramref name="ticks"/> into a torch's life.</summary>
    public static (float Intensity, float Range) At(double ticks)
    {
        float intensity = Dip(ticks, 0.11, 0.1f) * Dip(ticks, 0.147, 0.1f) * Dip(ticks, 0.263, 0.1f);
        float range = Dip(ticks, 0.111, 0.05f) * Dip(ticks, 0.1477, 0.05f) * Dip(ticks, 0.2631, 0.05f);
        return (intensity, range);
    }

    /// <summary>Each torch runs its own phase so neighbours do not pulse together.</summary>
    public static double Phase(int torchIndex) => torchIndex * 97.0;

    private static float Dip(double ticks, double frequency, float depth) =>
        1f - ((float)Math.Sin(ticks * frequency) * depth);
}

/// <summary>
/// The wall torches' presentation beyond their sprites: one retained warm
/// light per torch that flickers like the donor's fire lights, and one
/// retained Engine particle emitter per torch that lifts embers off the flame
/// like the donor torch's emitter ([data] entities.dat Torch emitter). Both
/// are rebuilt with each floor and owned here.
/// </summary>
public sealed class TorchEffects : IDisposable
{
    private const ulong LightBase = 4_100_000_000;
    private const ulong EmberBase = 4_400_000_000;
    private static readonly Vector3 LightColor = new(1f, 0.8f, 0.2f);
    private const float LightIntensity = 8f;
    private const float LightRange = 4f;
    private const float LightDecay = 1f;

    /// <summary>
    /// The donor spawns one or two embers every 90 ticks beside the player,
    /// slowing with distance and stopping at its spawnDistance of 15 tiles
    /// ([donor] entities/ParticleEmitter.java:199-201).
    /// </summary>
    private const float EmbersPerSecond = 1f;
    private const float EmberSpawnDistance = 15f;

    /// <summary>How often, in ticks, the ember rates follow the player.</summary>
    private const int EmberRateRefreshTicks = 15;

    /// <summary>Lifetime 25 + up to 10 ticks.</summary>
    private const float EmberLifeMin = 25f / 60f;
    private const float EmberLifeMax = 35f / 60f;

    /// <summary>Rising 0.01 ± 0.004 a tick, floating (no gravity); a little sideways drift stands in for the 0.2 spawn spread.</summary>
    private static readonly Vector3 EmberVelocityMin = new(-0.06f, 0.36f, -0.06f);
    private static readonly Vector3 EmberVelocityMax = new(0.06f, 0.84f, 0.06f);

    /// <summary>The particle colour (1, 0.957, 0.957), unfaded.</summary>
    private static readonly Color EmberColor = new(1f, 0.957f, 0.957f, 1f);

    /// <summary>
    /// Engine billboard particles keep one screen size at any distance (24
    /// points per unit of size); this size matches the donor's half-tile
    /// cell a couple of tiles away.
    /// </summary>
    private const float EmberSize = 1.2f;

    private readonly IEngineContext _engine;
    private readonly DelveParticleStrip? _ember;
    private readonly List<(Light Light, Vector3 Position)> _lights = new();
    private readonly List<(PresentationEmitter Emitter, Vector3 Flame, int Index)> _emitters = new();
    private bool _disposed;

    public TorchEffects(IEngineContext engine, DelveSpriteAssets sprites)
    {
        _engine = engine;
        _ember = sprites.ParticleFor("ember");
    }

    public int LightCount => _lights.Count;

    public int EmitterCount => _emitters.Count;

    /// <summary>Replace the torches: a light at each light position and embers at each flame.</summary>
    public void Load(IReadOnlyList<(Vector3 Light, Vector3 Flame)> torches)
    {
        Clear();
        for (int i = 0; i < torches.Count; i++)
        {
            _lights.Add((_engine.Graphics.CreateLight(Request(i, torches[i].Light, 1f, 1f)), torches[i].Light));
            if (_ember is not null)
            {
                _emitters.Add((_engine.Presentation.CreateEmitter(Embers(i, torches[i].Flame, _ember, EmbersPerSecond)), torches[i].Flame, i));
            }
        }
    }

    /// <summary>The ember rate a torch's emitter runs at with the player <paramref name="distance"/> tiles away.</summary>
    public static float EmberRate(float distance) =>
        EmbersPerSecond * Math.Max(0f, 1f - (distance / EmberSpawnDistance));

    /// <summary>Flicker every torch light for this tick, and let the embers follow the player.</summary>
    public void Tick(long elapsedTicks, Vector3 player)
    {
        if (_ember is not null && elapsedTicks % EmberRateRefreshTicks == 0)
        {
            foreach ((PresentationEmitter emitter, Vector3 flame, int index) in _emitters)
            {
                _engine.Presentation.UpdateEmitter(emitter, Embers(index, flame, _ember, EmberRate(Vector3.Distance(player, flame))));
            }
        }

        for (int i = 0; i < _lights.Count; i++)
        {
            (float intensity, float range) = TorchFlicker.At(elapsedTicks + TorchFlicker.Phase(i));
            _engine.Graphics.UpdateLight(new LightUpdateRequest(
                _lights[i].Light, Request(i, _lights[i].Position, intensity, range)));
        }
    }

    public void Clear()
    {
        foreach ((Light light, Vector3 _) in _lights)
        {
            light.Dispose();
        }

        foreach ((PresentationEmitter emitter, Vector3 _, int _) in _emitters)
        {
            emitter.Dispose();
        }

        _lights.Clear();
        _emitters.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }

    private static LightRequest Request(int index, Vector3 position, float intensityScale, float rangeScale) => new(
        LightBase + (ulong)index,
        false,
        0,
        new LightDescriptor(
            LightKind.Point,
            LightColor,
            LightIntensity * intensityScale,
            true,
            position,
            -Vector3.UnitY,
            true,
            LightRange * rangeScale,
            LightDecay,
            0f,
            0f,
            LightShadowIntent.Disabled));

    /// <summary>
    /// A retained emitter refuses a zero rate (reported as an exhausted
    /// budget), so a torch out of range keeps a trickle and is hidden.
    /// </summary>
    private const float IdleEmberRate = 0.01f;

    private static PresentationParticleDescriptor Embers(int index, Vector3 flame, DelveParticleStrip strip, float rate) => new(
        EmberBase + (ulong)index,
        "torch.embers",
        new PresentationAnchor(PresentationAnchorKind.World, flame, 0, Vector3.Zero),
        PresentationParticleVisual.Billboard,
        strip.Texture,
        (ushort)strip.Frames,
        Math.Max(rate, IdleEmberRate),
        0,
        EmberLifeMin,
        EmberLifeMax,
        EmberVelocityMin,
        EmberVelocityMax,
        Vector3.Zero,
        new PresentationParticleScalarKey[] { new(0f, EmberSize), new(1f, EmberSize) },
        new PresentationParticleColorKey[] { new(0f, EmberColor), new(1f, EmberColor) },
        // The donor plays the cells once over each particle's life.
        strip.Frames / ((EmberLifeMin + EmberLifeMax) / 2f),
        (ulong)index + 1,
        4,
        rate > 0f,
        false,
        default,
        ReadOnlyMemory<PresentationParticleCollisionVolume>.Empty);
}
