using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DelveRpg.Kit.Session;
using Rusty.Engine;

namespace DelveRpg.Host.Presentation;

/// <summary>The audio manifest: cue, monster and theme sounds by donor file name.</summary>
public sealed class DelveAudioManifest
{
    [JsonPropertyName("audioContentPrefix")]
    public string AudioContentPrefix { get; set; } = string.Empty;

    [JsonPropertyName("musicVolume")]
    public float MusicVolume { get; set; } = 0.5f;

    [JsonPropertyName("cues")]
    public Dictionary<string, DelveCueSound> Cues { get; set; } = new();

    [JsonPropertyName("monsters")]
    public Dictionary<string, DelveMonsterSounds> Monsters { get; set; } = new();

    [JsonPropertyName("themes")]
    public Dictionary<string, DelveThemeSounds> Themes { get; set; } = new();
}

public sealed class DelveCueSound
{
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    [JsonPropertyName("volume")]
    public float Volume { get; set; } = 1f;

    /// <summary>Played where the cue happened, falling off with distance; otherwise at the listener.</summary>
    [JsonPropertyName("world")]
    public bool World { get; init; }
}

/// <summary>A monster's donor sound set ([data] data/monsters.dat alertSound, attackSound, hurtSound, dieSound).</summary>
public sealed class DelveMonsterSounds
{
    [JsonPropertyName("alert")]
    public List<string> Alert { get; set; } = new();

    [JsonPropertyName("attack")]
    public List<string> Attack { get; set; } = new();

    [JsonPropertyName("hurt")]
    public List<string> Hurt { get; set; } = new();

    [JsonPropertyName("die")]
    public List<string> Die { get; set; } = new();
}

/// <summary>A floor theme's music tracks and looping ambience ([data] generator/&lt;Section&gt;/section.dat).</summary>
public sealed class DelveThemeSounds
{
    [JsonPropertyName("music")]
    public List<string> Music { get; set; } = new();

    [JsonPropertyName("ambient")]
    public string Ambient { get; set; } = string.Empty;

    [JsonPropertyName("ambientVolume")]
    public float AmbientVolume { get; set; } = 0.15f;
}

[JsonSerializable(typeof(DelveAudioManifest))]
public sealed partial class DelveAudioJsonContext : JsonSerializerContext;

/// <summary>
/// Voices the run through the Engine's audio service: each tick's
/// <see cref="RunSession.Cues"/> become one-shot emissions, and each floor
/// theme keeps a looping music track and ambience. Sounds are donor files
/// staged locally; a missing file is silence, never an error. Which file of a
/// set plays, and its slight pitch spread, is presentation randomness.
/// </summary>
public sealed class DelveAudio : IDisposable
{
    public const string ManifestPath = "delve/audio/sounds.json";

    /// <summary>World sounds fade out over this many tiles.</summary>
    private const float WorldRange = 16f;

    private readonly IEngineContext _engine;
    private readonly DelveAudioManifest _manifest;
    private readonly Func<string, bool> _contentExists;
    private readonly Dictionary<string, AudioClip?> _clips = new(StringComparer.Ordinal);
    private readonly System.Random _random = new();
    private string? _theme;
    private AudioVoice? _music;
    private AudioVoice? _ambient;
    private long _emitted;
    private bool _disposed;

    private DelveAudio(IEngineContext engine, DelveAudioManifest manifest, Func<string, bool> contentExists)
    {
        _engine = engine;
        _manifest = manifest;
        _contentExists = contentExists;
    }

    public static DelveAudio Load(IEngineContext engine, Func<string, string?> readText, Func<string, bool> contentExists)
    {
        string? text = readText(ManifestPath);
        DelveAudioManifest manifest = text is null
            ? new DelveAudioManifest()
            : JsonSerializer.Deserialize(text, DelveAudioJsonContext.Default.DelveAudioManifest)!;
        return new DelveAudio(engine, manifest, contentExists);
    }

    /// <summary>
    /// The files a cue sounds, in play order: a monster cue plays its
    /// monster's own sound with the cue's shared one (the donor plays the hit
    /// and the monster's hurt cry together), and a monster's death cry
    /// replaces the generic one.
    /// </summary>
    public static IEnumerable<(string File, DelveCueSound Sound)> Sounds(DelveAudioManifest manifest, RunCue cue, Func<IReadOnlyList<string>, string?> pick)
    {
        DelveCueSound? shared = manifest.Cues.TryGetValue(cue.Kind.ToString(), out DelveCueSound? found) ? found : null;
        DelveMonsterSounds? monster = cue.Subject is string subject && manifest.Monsters.TryGetValue(subject, out DelveMonsterSounds? sounds)
            ? sounds
            : null;
        List<string>? own = monster is null ? null : cue.Kind switch
        {
            CueKind.MonsterAlert => monster.Alert,
            CueKind.MonsterAttack => monster.Attack,
            CueKind.MonsterHurt => monster.Hurt,
            CueKind.MonsterDie => monster.Die,
            _ => null,
        };

        var voice = shared ?? new DelveCueSound { World = true };
        if (own is { Count: > 0 } && pick(own) is string ownFile)
        {
            yield return (ownFile, voice);
            if (cue.Kind == CueKind.MonsterDie)
            {
                yield break;
            }
        }

        if (shared is not null && pick(shared.Files) is string sharedFile)
        {
            yield return (sharedFile, shared);
        }
    }

    /// <summary>Voice one tick's cues. <paramref name="floorHeight"/> gives the ground under a point.</summary>
    public void Voice(IReadOnlyList<RunCue> cues, Func<float, float, float> floorHeight)
    {
        foreach (RunCue cue in cues)
        {
            foreach ((string file, DelveCueSound sound) in Sounds(_manifest, cue, Pick))
            {
                if (Clip(file) is not AudioClip clip)
                {
                    continue;
                }

                float pitch = cue.Kind is CueKind.Footstep or CueKind.Wade
                    ? 0.9f + ((float)_random.NextDouble() * 0.2f)
                    : 1f;
                var position = new Vector3(cue.X, floorHeight(cue.X, cue.Y) + 0.5f, cue.Y);
                _engine.Audio.Emit(new AudioEmitRequest(
                    $"delve.cue.{++_emitted}",
                    new AudioSourceDescriptor(
                        clip,
                        AudioBus.Sfx,
                        sound.Volume,
                        pitch,
                        false,
                        sound.World ? 1f : 0f,
                        WorldRange,
                        AudioRolloff.Linear,
                        0f,
                        sound.World ? AudioEmitterKind.World3d : AudioEmitterKind.Global2d,
                        position,
                        0,
                        Vector3.Zero)));
            }
        }
    }

    /// <summary>
    /// Keep the theme's music and ambience looping; a new theme changes the
    /// track, a floor of the same theme keeps it. Null stops both (no run).
    /// </summary>
    public void Theme(string? theme)
    {
        if (theme == _theme)
        {
            return;
        }

        _theme = theme;
        _music?.Dispose();
        _ambient?.Dispose();
        _music = null;
        _ambient = null;
        if (theme is null || !_manifest.Themes.TryGetValue(theme, out DelveThemeSounds? sounds))
        {
            return;
        }

        _music = Loop(Pick(sounds.Music), _manifest.MusicVolume);
        _ambient = Loop(sounds.Ambient, sounds.AmbientVolume);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _music?.Dispose();
        _ambient?.Dispose();
        foreach (AudioClip? clip in _clips.Values)
        {
            clip?.Dispose();
        }

        _clips.Clear();
    }

    private AudioVoice? Loop(string? file, float volume) =>
        file is not null && Clip(file) is AudioClip clip
            ? _engine.Audio.CreateVoice(new AudioSourceDescriptor(
                clip, AudioBus.Ambient, volume, 1f, true, 0f, WorldRange, AudioRolloff.Linear, 0f,
                AudioEmitterKind.Global2d, Vector3.Zero, 0, Vector3.Zero))
            : null;

    private string? Pick(IReadOnlyList<string> files) =>
        files.Count == 0 ? null : files[_random.Next(files.Count)];

    private AudioClip? Clip(string file)
    {
        if (_clips.TryGetValue(file, out AudioClip? cached))
        {
            return cached;
        }

        string path = string.IsNullOrEmpty(_manifest.AudioContentPrefix) ? file : $"{_manifest.AudioContentPrefix.TrimEnd('/')}/{file}";
        AudioClip? clip = _contentExists(path) ? _engine.Audio.OpenClip(new AudioClipRequest(path)) : null;
        _clips[file] = clip;
        return clip;
    }
}
