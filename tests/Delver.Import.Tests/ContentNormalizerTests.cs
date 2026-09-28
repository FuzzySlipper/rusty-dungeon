using System.Text;
using System.Text.Json;
using Delver.Import.Normalized;
using Xunit;

namespace Delver.Import.Tests;

public class ContentNormalizerTests
{
    [Fact]
    public void NormalizeDocument_SortsObjectKeysOrdinal()
    {
        string normalized = ContentNormalizer.NormalizeDocument("{\"b\": 1, \"a\": 2, \"B\": 3}");

        // Ordinal order: uppercase 'B' (0x42) before lowercase 'a'/'b'.
        Assert.Equal(
            "{\n  \"B\": 3,\n  \"a\": 2,\n  \"b\": 1\n}\n",
            normalized);
    }

    [Fact]
    public void NormalizeDocument_IsByteIdenticalAcrossRuns()
    {
        const string donor = "{ b: 2, a: [1, 3.0, {z: true, y: null}], }";
        byte[] first = ContentNormalizer.NormalizeDocumentBytes(donor);
        byte[] second = ContentNormalizer.NormalizeDocumentBytes(donor);

        Assert.Equal(first, second);
    }

    [Fact]
    public void NormalizeDocument_WritesStrictJsonWithoutBom()
    {
        byte[] bytes = ContentNormalizer.NormalizeDocumentBytes("{ x: 3.0, y: -5, s: \"a\\tb\" }");

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        string text = Encoding.UTF8.GetString(bytes);
        using JsonDocument document = JsonDocument.Parse(text);
        Assert.Equal(3, document.RootElement.GetProperty("x").GetDouble());
        Assert.Equal(-5, document.RootElement.GetProperty("y").GetDouble());
        Assert.Equal("a\tb", document.RootElement.GetProperty("s").GetString());
    }

    [Fact]
    public void NormalizeDocument_UsesInvariantNumberFormatting()
    {
        string normalized = ContentNormalizer.NormalizeDocument("{\"value\": 16.039}");

        Assert.Contains("16.039", normalized);
        Assert.DoesNotContain("16,039", normalized);
    }

    [Fact]
    public void NormalizeDocument_EscapesStringsForStrictJson()
    {
        string normalized = ContentNormalizer.NormalizeDocument("{\"s\": \"a\\\"b\\\\c\\u0001\"}");

        Assert.Contains("a\\\"b\\\\c\\u0001", normalized);
    }

    [Fact]
    public void NormalizeDocument_RejectsMalformedJson()
    {
        Assert.Throws<RelaxedJson.RelaxedJsonException>(() => ContentNormalizer.NormalizeDocument("{"));
    }
}
