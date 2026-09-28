using Delver.Import.RelaxedJson;
using Xunit;

namespace Delver.Import.Tests;

public class RelaxedJsonReaderTests
{
    [Fact]
    public void Parse_AcceptsUnquotedKeysAndValues()
    {
        // Donor: levels/test/test1.dat — {entities:[{class:com.interrupt.dungeoneer.entities.Door,…
        JsonValue value = RelaxedJsonReader.Parse("{class:com.interrupt.dungeoneer.entities.Door,x:4.5,breakable:false,gone:null}");

        Assert.Equal(JsonValueKind.Object, value.Kind);
        Assert.True(value.TryGetProperty("class", out JsonValue className));
        Assert.Equal("com.interrupt.dungeoneer.entities.Door", className.StringValue);
        Assert.True(value.TryGetProperty("x", out JsonValue x));
        Assert.Equal(4.5, x.NumberValue);
        Assert.True(value.TryGetProperty("breakable", out JsonValue breakable));
        Assert.False(breakable.BoolValue);
        Assert.True(value.TryGetProperty("gone", out JsonValue gone));
        Assert.Equal(JsonValueKind.Null, gone.Kind);
    }

    [Fact]
    public void Parse_BareValuesKeepSpaces()
    {
        // Donor: generator/Cave/Starts/2.dat — …,category:Cave Sets,name:Hanging_Crystal,…
        JsonValue value = RelaxedJsonReader.Parse("{category:Cave Sets,name:Hanging_Crystal}");

        Assert.True(value.TryGetProperty("category", out JsonValue category));
        Assert.Equal("Cave Sets", category.StringValue);
    }

    [Fact]
    public void Parse_AcceptsTrailingCommas()
    {
        JsonValue value = RelaxedJsonReader.Parse("{\"a\": [1, 2,],}");

        Assert.True(value.TryGetProperty("a", out JsonValue a));
        Assert.Equal(2, a.Items.Count);
        Assert.Equal(1, a.Items[0].NumberValue);
    }

    [Fact]
    public void Parse_AcceptsComments()
    {
        JsonValue value = RelaxedJsonReader.Parse("{\n  // line comment\n  a: 1, /* block */ b: 2\n}");

        Assert.Equal(2, value.Properties.Count);
    }

    [Fact]
    public void Parse_AcceptsMissingCommaBetweenElements()
    {
        // Donor: data/messages/wizard.dat lines 6-7 omit the comma between strings.
        JsonValue value = RelaxedJsonReader.Parse("{\"messages\": [[\"one\" \"two\" \"three\"]]}");

        Assert.True(value.TryGetProperty("messages", out JsonValue messages));
        Assert.Equal(3, messages.Items[0].Items.Count);
        Assert.Equal("two", messages.Items[0].Items[1].StringValue);
    }

    [Fact]
    public void Parse_StrictJsonIsASubset()
    {
        const string strict = """
            {"a": "x\ny", "b": [true, false, null, 1.5e3], "c": {"d": -2}}
            """;
        JsonValue value = RelaxedJsonReader.Parse(strict);

        Assert.True(value.TryGetProperty("a", out JsonValue a));
        Assert.Equal("x\ny", a.StringValue);
        Assert.True(value.TryGetProperty("b", out JsonValue b));
        Assert.Equal(4, b.Items.Count);
        Assert.Equal(1500, b.Items[3].NumberValue);
        Assert.True(value.TryGetProperty("c", out JsonValue c));
        Assert.True(c.TryGetProperty("d", out JsonValue d));
        Assert.Equal(-2, d.NumberValue);
    }

    [Fact]
    public void Parse_ReportsErrorPositionForBadValue()
    {
        RelaxedJsonException error = Assert.Throws<RelaxedJsonException>(
            () => RelaxedJsonReader.Parse("{\n  \"a\": }"));

        Assert.Equal(2, error.Line);
        Assert.Equal(8, error.Column);
        Assert.Equal(9, error.Position);
        Assert.Contains("Expected a value", error.Message);
    }

    [Fact]
    public void Parse_ReportsErrorPositionForDoubleTrailingComma()
    {
        RelaxedJsonException error = Assert.Throws<RelaxedJsonException>(
            () => RelaxedJsonReader.Parse("[1,,]"));

        Assert.Equal(1, error.Line);
        Assert.Equal(4, error.Column);
        Assert.Contains("Expected a value", error.Message);
    }

    [Fact]
    public void Parse_ReportsErrorPositionForUnterminatedString()
    {
        RelaxedJsonException error = Assert.Throws<RelaxedJsonException>(
            () => RelaxedJsonReader.Parse("{\"a\": \"abc"));

        Assert.Equal(1, error.Line);
        Assert.Equal(11, error.Column);
        Assert.Contains("Unterminated string", error.Message);
    }

    [Fact]
    public void Parse_RejectsTrailingContent()
    {
        RelaxedJsonException error = Assert.Throws<RelaxedJsonException>(
            () => RelaxedJsonReader.Parse("[1] x"));

        Assert.Contains("trailing content", error.Message);
    }

    [Fact]
    public void Parse_ReadsUtf8Bytes()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("{\"name\": \"déjà\"}");
        JsonValue value = RelaxedJsonReader.Parse(utf8);

        Assert.True(value.TryGetProperty("name", out JsonValue name));
        Assert.Equal("déjà", name.StringValue);
    }

    [Fact]
    public void Parse_AcceptsBareValuesWithSlashes()
    {
        // Donor: levels/test/test1.dat — meshFile:meshes/door_0.obj
        JsonValue value = RelaxedJsonReader.Parse("{meshFile:meshes/door_0.obj}");

        Assert.True(value.TryGetProperty("meshFile", out JsonValue mesh));
        Assert.Equal("meshes/door_0.obj", mesh.StringValue);
    }

    [Fact]
    public void Parse_RejectsEmptyInput()
    {
        RelaxedJsonException error = Assert.Throws<RelaxedJsonException>(
            () => RelaxedJsonReader.Parse(""));

        Assert.Contains("Unexpected end of input", error.Message);
    }

    [Fact]
    public void Parse_RejectsExcessiveNestingWithoutCrashing()
    {
        string deep = new string('[', 2000) + new string(']', 2000);

        RelaxedJsonException error = Assert.Throws<RelaxedJsonException>(
            () => RelaxedJsonReader.Parse(deep));

        Assert.Contains("nesting depth", error.Message);
    }
}
