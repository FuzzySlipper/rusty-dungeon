using System.Globalization;
using Delver.Import.RelaxedJson;

namespace Delver.Import.Normalized;

/// <summary>
/// Tolerant field extraction for donor documents. Donor quirk: numeric fields
/// sometimes arrive as strings (generator/Sewer/section.dat:
/// <c>"ambientSoundVolume": "0.15"</c>) and multi-sound fields are
/// comma-separated strings rather than arrays.
/// </summary>
internal static class JsonRead
{
    public static string? String(JsonValue? value) =>
        value?.Kind == JsonValueKind.String ? value.StringValue : null;

    public static string StringOr(JsonValue? value, string fallback) => String(value) ?? fallback;

    public static double? Number(JsonValue? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Kind == JsonValueKind.Number)
        {
            return value.NumberValue;
        }

        if (value.Kind == JsonValueKind.String &&
            double.TryParse(value.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        return null;
    }

    public static double NumberOr(JsonValue? value, double fallback) => Number(value) ?? fallback;

    public static int? Int(JsonValue? value)
    {
        double? number = Number(value);
        return number is null ? null : (int)Math.Round(number.Value, MidpointRounding.AwayFromZero);
    }

    public static int IntOr(JsonValue? value, int fallback) => Int(value) ?? fallback;

    public static bool BoolOr(JsonValue? value, bool fallback) =>
        value?.Kind == JsonValueKind.Bool ? value.BoolValue : fallback;

    /// <summary>Array of strings, or the donor's comma-separated string form.</summary>
    public static IReadOnlyList<string> StringList(JsonValue? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value.Kind == JsonValueKind.Array)
        {
            return value.Items.Select(item => String(item) ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
        }

        if (value.Kind == JsonValueKind.String)
        {
            return value.StringValue.Split(',')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();
        }

        return Array.Empty<string>();
    }
}
