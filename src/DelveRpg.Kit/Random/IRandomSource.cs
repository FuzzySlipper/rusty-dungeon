namespace DelveRpg.Kit.Random;

/// <summary>
/// Deterministic randomness for generation and rules. The Host adapts the
/// Engine's keyed random service behind this; tests use a scripted source.
/// </summary>
public interface IRandomSource
{
    /// <summary>Draw an integer in [<paramref name="minimum"/>, <paramref name="maximum"/>).</summary>
    int Next(int minimum, int maximum);

    /// <summary>Draw a boolean with the given probability of true.</summary>
    bool Chance(double probability) => Next(0, 1_000_000) < probability * 1_000_000;
}
