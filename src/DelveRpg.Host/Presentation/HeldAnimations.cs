using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DelveRpg.Kit.Actors;

namespace DelveRpg.Host.Presentation;

/// <summary>The authored held-weapon animation manifest (<c>delve/art/held.json</c>).</summary>
public sealed class HeldAnimationManifest
{
    public string Schema { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Dictionary<string, HeldAnimationStyle> Styles { get; set; } = new();
}

/// <summary>One style: the charge pose clip and the quick and full swings.</summary>
public sealed class HeldAnimationStyle
{
    public List<HeldKeyframe> Charge { get; set; } = new();

    public List<HeldKeyframe> Weak { get; set; } = new();

    public List<HeldKeyframe> Strong { get; set; } = new();
}

/// <summary>An offset from the rest pose at clip time <see cref="T"/> (0..1).</summary>
public sealed class HeldKeyframe
{
    public float T { get; set; }

    public float Right { get; set; }

    public float Up { get; set; }

    public float Forward { get; set; }

    public float Roll { get; set; }

    public float Pitch { get; set; }

    public static HeldKeyframe Rest(float t) => new() { T = t };
}

/// <summary>Camera-local weapon pose; the viewmodel camera looks down −Z.</summary>
public readonly record struct HeldPose(Vector3 Position, float RollDegrees, float PitchDegrees);

/// <summary>
/// Samples the held weapon's pose. Clips interpolate linearly between
/// keyframes, the donor's default LerpedAnimation interpolation
/// ([donor] gfx/animation/lerp3d/LerpedAnimation.java). The charge clip
/// follows the charge fraction and holds at full; a swing plays over the
/// Kit's swing length and starts from the pose it was released from, as the
/// donor blends a new clip from the previous pose. The walk bob is the
/// donor's head bob, which the held item follows at 0.55
/// ([donor] entities/Player.java:582, gfx/GlRenderer.java:2034, :452).
/// </summary>
public sealed class HeldAnimations
{
    public const string ManifestPath = "delve/art/held.json";

    /// <summary>The weapon's resting place: low and to the right.</summary>
    public static readonly Vector3 RestPosition = new(0.30f, -0.28f, -0.62f);

    private readonly HeldAnimationManifest _manifest;

    public HeldAnimations(HeldAnimationManifest manifest)
    {
        _manifest = manifest;
    }

    public static HeldAnimations Load(Func<string, string?> readText)
    {
        string? text = readText(ManifestPath);
        HeldAnimationManifest manifest = text is null
            ? new HeldAnimationManifest()
            : JsonSerializer.Deserialize(text, HeldAnimationJsonContext.Default.HeldAnimationManifest)
                ?? new HeldAnimationManifest();
        return new HeldAnimations(manifest);
    }

    /// <summary>
    /// The pose for a weapon of <paramref name="style"/>: mid-swing when
    /// <paramref name="swing"/> is playing, else charging by
    /// <paramref name="charge"/> (0..1). An unknown style rests.
    /// </summary>
    public HeldPose Sample(string style, float charge, SwingState? swing)
    {
        if (!_manifest.Styles.TryGetValue(style, out HeldAnimationStyle? clips))
        {
            return Pose(HeldKeyframe.Rest(0f));
        }

        if (swing is not null)
        {
            List<HeldKeyframe> frames = swing.Strong && clips.Strong.Count > 0 ? clips.Strong : clips.Weak;
            if (frames.Count > 0)
            {
                HeldKeyframe released = At(clips.Charge, swing.Power);
                return Pose(At(frames, swing.Progress, released));
            }
        }

        return Pose(At(clips.Charge, Math.Clamp(charge, 0f, 1f)));
    }

    /// <summary>The donor's head bob for a walking speed (tiles per tick) at a tick.</summary>
    public static float HeadBob(float speed, long ticks) =>
        MathF.Sin(ticks * 0.319f) * Math.Min(speed, 0.15f) * 0.3f;

    /// <summary>How much of the head bob the held weapon follows ([donor] GlRenderer.java:2034).</summary>
    public const float WeaponBobShare = 0.55f;

    private static HeldPose Pose(HeldKeyframe frame) => new(
        RestPosition + new Vector3(frame.Right, frame.Up, -frame.Forward),
        frame.Roll,
        frame.Pitch);

    /// <summary>Linear sample of a clip at <paramref name="t"/>; the first keyframe may be replaced.</summary>
    private static HeldKeyframe At(IReadOnlyList<HeldKeyframe> frames, float t, HeldKeyframe? start = null)
    {
        if (frames.Count == 0)
        {
            return HeldKeyframe.Rest(t);
        }

        HeldKeyframe Frame(int index) => index == 0 && start is not null ? start : frames[index];

        if (t <= frames[0].T)
        {
            return Frame(0);
        }

        for (int i = 1; i < frames.Count; i++)
        {
            if (t > frames[i].T)
            {
                continue;
            }

            HeldKeyframe from = Frame(i - 1);
            HeldKeyframe to = frames[i];
            float span = Math.Max(0.0001f, to.T - frames[i - 1].T);
            float k = (t - frames[i - 1].T) / span;
            return new HeldKeyframe
            {
                T = t,
                Right = float.Lerp(from.Right, to.Right, k),
                Up = float.Lerp(from.Up, to.Up, k),
                Forward = float.Lerp(from.Forward, to.Forward, k),
                Roll = float.Lerp(from.Roll, to.Roll, k),
                Pitch = float.Lerp(from.Pitch, to.Pitch, k),
            };
        }

        return frames[^1];
    }
}

/// <summary>JSON binding for the held-animation manifest (NativeAOT-safe source generation).</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HeldAnimationManifest))]
public sealed partial class HeldAnimationJsonContext : JsonSerializerContext;
