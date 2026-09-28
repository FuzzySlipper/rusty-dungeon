using DelveRpg.Kit.Random;
using Rusty.Engine;

namespace DelveRpg.Host;

/// <summary>
/// The Kit's random seam answered by the Engine's keyed random service. Keys
/// are counter-based inside one scope, so a draw depends only on the run seed
/// and its position in the draw order. The Kit's <see cref="IRandomSource"/>
/// bounds are half-open while <see cref="KeyedRngRequest"/> is inclusive on
/// both ends; the translation happens here and nowhere else.
/// </summary>
public sealed class KeyedRandomSource : IRandomSource
{
    private readonly IRandomService _random;
    private readonly ulong _seed;
    private readonly string _scope;
    private long _counter;

    public KeyedRandomSource(IRandomService random, ulong seed, string scope)
    {
        _random = random;
        _seed = seed;
        _scope = scope;
    }

    public int Next(int minimum, int maximum)
    {
        if (minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum must exceed minimum.");
        }

        long value = _random.DrawKeyed(
            new KeyedRngRequest(_seed, _scope, $"draw:{_counter++}", minimum, maximum - 1)).Value;
        return (int)value;
    }
}
