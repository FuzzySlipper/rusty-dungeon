using DelveRpg.Host.Presentation;
using Xunit;

namespace DelveRpg.Host.Tests;

/// <summary>Torch flicker, ember spawning and each theme's fog.</summary>
public sealed class TorchAndFogTests
{
    [Fact]
    public void Flicker_stays_within_the_donor_fire_bounds_and_moves()
    {
        var intensities = new List<float>();
        for (int tick = 0; tick < 600; tick++)
        {
            (float intensity, float range) = TorchFlicker.At(tick);
            Assert.InRange(intensity, 0.9f * 0.9f * 0.9f, 1.1f * 1.1f * 1.1f);
            Assert.InRange(range, 0.95f * 0.95f * 0.95f, 1.05f * 1.05f * 1.05f);
            intensities.Add(intensity);
        }

        Assert.True(intensities.Max() - intensities.Min() > 0.2f);
        Assert.Equal(1f, TorchFlicker.At(0).Intensity);
        Assert.NotEqual(TorchFlicker.At(TorchFlicker.Phase(0)), TorchFlicker.At(TorchFlicker.Phase(1)));
    }

    [Fact]
    public void Embers_slow_with_distance_and_stop_at_the_donor_spawn_distance()
    {
        Assert.Equal(1f, TorchEffects.EmberRate(0f));
        Assert.Equal(0.5f, TorchEffects.EmberRate(7.5f), 3);
        Assert.Equal(0f, TorchEffects.EmberRate(15f));
        Assert.Equal(0f, TorchEffects.EmberRate(40f));
    }

    [Fact]
    public void Shipped_themes_carry_the_donor_section_fog_without_art()
    {
        DelveArtAssets assets = DelveArtAssets.Load(null!, ShippedContent.Read, _ => false);

        DelveArtFog sewer = assets.FogFor("Sewer")!;
        Assert.Equal(0f, sewer.Start);
        Assert.Equal(12f, sewer.End);
        Assert.Equal([0.1f, 0.56f, 0.53f], sewer.Color);
        Assert.Equal(16.039f, assets.FogFor("Temple")!.End);
        Assert.All(new[] { "Sewer", "Temple", "Undead", "Cave", "Cold" }, theme => Assert.NotNull(assets.FogFor(theme)));
        Assert.Null(assets.FogFor("Nowhere"));
    }

    [Fact]
    public void The_ember_flipbook_is_the_donor_torch_emitter_cells()
    {
        DelveSpriteManifest manifest = System.Text.Json.JsonSerializer.Deserialize(
            ShippedContent.Read("delve/art/sprites.json")!, DelveSpriteJsonContext.Default.DelveSpriteManifest)!;

        DelveParticleDefinition ember = manifest.Particles["ember"];
        Assert.Equal("particles.png", ember.Atlas);
        Assert.Equal((64, 69), (ember.Start, ember.End));
        Assert.Equal(6, ember.Frames);
    }
}
