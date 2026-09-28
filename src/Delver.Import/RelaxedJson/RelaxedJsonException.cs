namespace Delver.Import.RelaxedJson;

/// <summary>Thrown when <see cref="RelaxedJsonReader"/> cannot parse input; carries a 1-based line/column.</summary>
public sealed class RelaxedJsonException : FormatException
{
    public RelaxedJsonException(string message, int line, int column, int position)
        : base($"{message} (line {line}, column {column})")
    {
        Line = line;
        Column = column;
        Position = position;
    }

    /// <summary>1-based line of the offending input.</summary>
    public int Line { get; }

    /// <summary>1-based column of the offending input.</summary>
    public int Column { get; }

    /// <summary>0-based character offset of the offending input.</summary>
    public int Position { get; }
}
