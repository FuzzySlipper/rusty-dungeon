using System.Text;
using Delver.Import.RelaxedJson;

namespace Delver.Import.Normalized;

/// <summary>
/// Normalizes donor content documents to stable strict JSON for reference:
/// sorted object keys (ordinal), invariant number formatting, UTF-8 without
/// BOM. The same input always yields the same bytes.
/// </summary>
public static class ContentNormalizer
{
    /// <summary>Parses relaxed donor JSON and returns canonical strict JSON text.</summary>
    public static string NormalizeDocument(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return StrictJsonWriter.Write(RelaxedJsonReader.Parse(json));
    }

    /// <summary>Canonical strict JSON as UTF-8 without BOM.</summary>
    public static byte[] NormalizeDocumentBytes(string json) => ToUtf8(NormalizeDocument(json));

    /// <summary>Writes a DOM tree as canonical strict JSON text (sorted keys).</summary>
    public static string WriteDocument(JsonValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return StrictJsonWriter.Write(value);
    }

    /// <summary>Writes a DOM tree as canonical strict JSON, UTF-8 without BOM.</summary>
    public static byte[] WriteDocumentBytes(JsonValue value) => ToUtf8(WriteDocument(value));

    private static byte[] ToUtf8(string text) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
}
