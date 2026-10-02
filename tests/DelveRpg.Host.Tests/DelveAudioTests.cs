using DelveRpg.Host.Presentation;
using DelveRpg.Kit.Session;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Host.Tests;

/// <summary>Which sounds a cue plays, from the shipped audio manifest.</summary>
public sealed class DelveAudioTests
{
    private static readonly DelveAudioManifest Manifest = System.Text.Json.JsonSerializer.Deserialize(
        ShippedContent.Read(DelveAudio.ManifestPath)!, DelveAudioJsonContext.Default.DelveAudioManifest)!;

    private static List<string> Files(RunCue cue) =>
        DelveAudio.Sounds(Manifest, cue, files => files.Count > 0 ? files[0] : null).Select(sound => sound.File).ToList();

    [Fact]
    public void Every_cue_has_a_sound_or_a_monster_set()
    {
        CueKind[] monsterOnly = [CueKind.MonsterAlert];
        foreach (CueKind kind in Enum.GetValues<CueKind>().Except(monsterOnly))
        {
            Assert.True(Manifest.Cues.ContainsKey(kind.ToString()), $"no sound for {kind}");
        }
    }

    [Fact]
    public void Every_shipped_monster_has_alert_attack_hurt_and_die_sounds()
    {
        DelverContentPack pack = DelverComposition.Load(ShippedContent.Read).Pack;
        Assert.All(pack.Monsters, monster =>
        {
            DelveMonsterSounds sounds = Manifest.Monsters[monster.Id];
            Assert.NotEmpty(sounds.Alert);
            Assert.NotEmpty(sounds.Attack);
            Assert.NotEmpty(sounds.Hurt);
            Assert.NotEmpty(sounds.Die);
        });
    }

    [Fact]
    public void A_hurt_monster_cries_over_the_hit_and_a_dying_one_only_cries()
    {
        List<string> hurt = Files(new RunCue(CueKind.MonsterHurt, 1f, 1f, "delve.monster.skeleton"));
        Assert.Equal(["mobs/skeleton/en_skel_hurt_01.mp3", "hit.mp3"], hurt);

        List<string> die = Files(new RunCue(CueKind.MonsterDie, 1f, 1f, "delve.monster.skeleton"));
        Assert.Equal(["mobs/skeleton/en_skel_death_01.mp3"], die);

        List<string> unknown = Files(new RunCue(CueKind.MonsterDie, 1f, 1f, "delve.monster.nobody"));
        Assert.Equal(["sfx_death_enemy_01.mp3"], unknown);

        Assert.Equal(["mobs/skeleton/en_skel_alert_01.mp3"], Files(new RunCue(CueKind.MonsterAlert, 1f, 1f, "delve.monster.skeleton")));
        Assert.Empty(Files(new RunCue(CueKind.MonsterAlert, 1f, 1f, "delve.monster.nobody")));
    }

    [Fact]
    public void Each_theme_loops_its_donor_section_music_and_ambience()
    {
        Assert.Equal(["music/02_descending.mp3", "music/Nathan-3.mp3"], Manifest.Themes["Sewer"].Music);
        Assert.All(Manifest.Themes.Values, theme =>
        {
            Assert.NotEmpty(theme.Music);
            Assert.Equal("env_indoor.mp3", theme.Ambient);
            Assert.Equal(0.15f, theme.AmbientVolume);
        });
    }
}
