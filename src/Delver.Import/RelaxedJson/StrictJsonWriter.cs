using System.Globalization;
using System.Text;

namespace Delver.Import.RelaxedJson;

/// <summary>
/// Writes a <see cref="JsonValue"/> as strict JSON: 2-space indentation,
/// ordinal-sorted object keys, invariant numbers, no trailing commas.
/// </summary>
internal static class StrictJsonWriter
{
    public static string Write(JsonValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder builder = new();
        WriteValue(builder, value, 0);
        builder.Append('\n');
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, JsonValue value, int depth)
    {
        switch (value.Kind)
        {
            case JsonValueKind.Object:
                WriteObject(builder, value, depth);
                break;
            case JsonValueKind.Array:
                WriteArray(builder, value, depth);
                break;
            case JsonValueKind.String:
                WriteString(builder, value.StringValue);
                break;
            case JsonValueKind.Number:
                builder.Append(value.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.Bool:
                builder.Append(value.BoolValue ? "true" : "false");
                break;
            default:
                builder.Append("null");
                break;
        }
    }

    private static void WriteObject(StringBuilder builder, JsonValue value, int depth)
    {
        builder.Append('{');
        List<JsonProperty> properties = value.Properties
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToList();
        for (int i = 0; i < properties.Count; i++)
        {
            builder.Append('\n');
            Indent(builder, depth + 1);
            WriteString(builder, properties[i].Name);
            builder.Append(": ");
            WriteValue(builder, properties[i].Value, depth + 1);
            if (i < properties.Count - 1)
            {
                builder.Append(',');
            }
        }

        if (properties.Count > 0)
        {
            builder.Append('\n');
            Indent(builder, depth);
        }

        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, JsonValue value, int depth)
    {
        builder.Append('[');
        IReadOnlyList<JsonValue> items = value.Items;
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            WriteValue(builder, items[i], depth + 1);
        }

        builder.Append(']');
    }

    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static void Indent(StringBuilder builder, int depth)
    {
        builder.Append(' ', depth * 2);
    }
}
