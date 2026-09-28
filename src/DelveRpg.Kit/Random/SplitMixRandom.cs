namespace DelveRpg.Kit.Random;

/// <summary>
/// A small deterministic generator seeded per floor. Floor generation must
/// replay identically when an ascended floor is regenerated, so it owns its
/// arithmetic here; live draws (loot at runtime, AI jitter) go through the
/// Host's Engine-random adapter instead.
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
