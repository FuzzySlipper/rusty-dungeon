using DelveRpg.Host.Presentation;
using DelveRpg.Kit.Combat;
using DelveRpg.Rulesets.Delver.Content;
using Xunit;

namespace DelveRpg.Host.Tests;

/// <summary>Every sprite id the shipped pack names is mapped by the shipped sprite manifest.</summary>
public sealed class ShippedSpriteCoverageTests
{
    private static string ContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content", "delve")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Repository content tree not found."),
            "content");
    }

    private static string? Read(string path)
    {
        string full = Path.Combine(ContentRoot(), path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    [Fact]
    public void Items_monsters_and_their_projectiles_all_have_sprites()
    {
        DelverContentPack pack = DelverComposition.Load(Read).Pack;
        DelveSpriteManifest manifest = System.Text.Json.JsonSerializer.Deserialize(
            Read("delve/art/sprites.json")!, DelveSpriteJsonContext.Default.DelveSpriteManifest)!;

        IEnumerable<string> named = pack.Items.Select(item => item.Sprite)
            .Concat(pack.Items.Select(item => item.ProjectileSprite).Where(id => id.Length > 0))
            .Concat(pack.Monsters.Select(monster => monster.Sprite))
            .Concat(pack.Monsters.Where(monster => monster.Ranged is not null).Select(monster => monster.Ranged!.Sprite));

        Assert.All(named, id => Assert.True(manifest.Sprites.ContainsKey(id), $"no sprite for '{id}'"));
        Assert.True(manifest.Sprites["projectile.spark"].Fullbright);
        Assert.True(manifest.Sprites["projectile.eyebolt"].Fullbright);
    }

    [Fact]
    public void Bolts_take_the_donor_damage_colours()
    {
        Assert.Equal(new System.Numerics.Vector3(0.6172f, 0.0937f, 0.7695f), DelveSceneRenderer.DamageColor(DamageType.Magic));
        Assert.Equal(System.Numerics.Vector3.UnitX, DelveSceneRenderer.DamageColor(DamageType.Fire));
        Assert.Equal(System.Numerics.Vector3.One, DelveSceneRenderer.DamageColor(DamageType.Physical));
    }
}
