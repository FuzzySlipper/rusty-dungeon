using Rusty.Engine;

namespace DelveRpg.Host.Tests;

/// <summary>Captures keyed draws and echoes a deterministic value.</summary>
public sealed class FakeRandomService : IRandomService
{
    public List<KeyedRngRequest> Draws { get; } = new();

    public long EchoValue { get; set; }

    public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
    {
        Draws.Add(request);
        return new KeyedRngReceipt(EchoValue);
    }

    public Rng CreateScoped(ScopedRngCreateRequest request) => null!;

    public Rng ForkScoped(ScopedRngForkRequest request) => null!;

    public Lcg15Receipt DrawLcg15(Lcg15Request request) => default;

    public RngValue NextBool(Rng rng) => default;

    public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => default;

    public RngValue NextU64(Rng rng) => default;
}

public sealed class KeyedRandomSourceTests
{
    [Fact]
    public void Half_open_kits_bounds_become_engine_inclusive_bounds()
    {
        var service = new FakeRandomService { EchoValue = 7 };
        var source = new KeyedRandomSource(service, 42UL, "run-draws");

        int value = source.Next(2, 10);

        Assert.Equal(7, value);
        KeyedRngRequest draw = Assert.Single(service.Draws);
        Assert.Equal(2, draw.Minimum);
        Assert.Equal(9, draw.Maximum); // Kit [2,10) == Engine 2..=9
        Assert.Equal(42UL, draw.Seed);
        Assert.Equal("run-draws", draw.Scope);
        Assert.Equal("draw:0", draw.Key);
    }

    [Fact]
    public void Single_value_draws_map_to_one_legal_inclusive_value()
    {
        var service = new FakeRandomService { EchoValue = 5 };
        var source = new KeyedRandomSource(service, 1UL, "s");

        Assert.Equal(5, source.Next(5, 6));
        KeyedRngRequest draw = Assert.Single(service.Draws);
        Assert.Equal(5, draw.Minimum);
        Assert.Equal(5, draw.Maximum);
    }

    [Fact]
    public void Empty_ranges_are_refused_before_reaching_the_engine()
    {
        var service = new FakeRandomService();
        var source = new KeyedRandomSource(service, 1UL, "s");
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Next(4, 4));
        Assert.Empty(service.Draws);
    }
}
