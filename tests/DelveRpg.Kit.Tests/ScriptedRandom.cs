using DelveRpg.Kit.Random;

namespace DelveRpg.Kit.Tests;

/// <summary>Draws queued values, then a constant; tests own every draw.</summary>
public sealed class ScriptedRandom : IRandomSource
{
    private readonly Queue<int> _values;
    private readonly int _fallback;

    public ScriptedRandom(params int[] values)
    {
        _values = new Queue<int>(values);
        _fallback = 0;
    }

    private ScriptedRandom(int fallback)
    {
        _values = new Queue<int>();
        _fallback = fallback;
    }

    /// <summary>Every draw returns this value, clamped into the drawn range.</summary>
    public static ScriptedRandom Always(int value) => new(value);

    public int Next(int minimum, int maximum)
    {
        int value = _values.Count > 0 ? _values.Dequeue() : _fallback;
        if (value < minimum)
        {
            return minimum;
        }

        return value >= maximum ? maximum - 1 : value;
    }
}
