namespace DelveRpg.Kit.Random;

/// <summary>
/// A small deterministic generator seeded per scope. It exists for floor
/// generation and run-plan expansion only: a floor must replay identically
/// when an ascended run regenerates it, independent of how many live draws
/// happened elsewhere. Live draws (combat, loot, offers, escape spawns) go
/// through the injected <see cref="IRandomSource"/>, which the Host answers
/// with the Engine's keyed random service.
/// </summary>
public sealed class SplitMixRandom : IRandomSource
{
    private ulong _state;

    public SplitMixRandom(ulong seed) => _state = seed;

    public static ulong FloorSeed(ulong runSeed, int runIndex) =>
        runSeed ^ (0x9E3779B97F4A7C15UL * ((ulong)runIndex + 1UL));

    public int Next(int minimum, int maximum)
    {
        if (minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum must exceed minimum.");
        }

        ulong range = (ulong)(maximum - minimum);
        return minimum + (int)(Draw() % range);
    }

    private ulong Draw()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
