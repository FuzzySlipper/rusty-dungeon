namespace Delver.Import.RelaxedJson;

/// <summary>Kind of a <see cref="JsonValue"/> node.</summary>
public enum JsonValueKind
{
    Object,
    Array,
    String,
    Number,
    Bool,
    Null,
}

/// <summary>One object member: a name and its value.</summary>
public readonly record struct JsonProperty(string Name, JsonValue Value);

/// <summary>
/// Immutable DOM node produced by <see cref="RelaxedJsonReader"/>. Object
/// members keep document order; canonical output sorts keys at write time
/// (see <see cref="Normalized.ContentNormalizer"/>).
/// </summary>
public sealed class JsonValue
{
    private static readonly IReadOnlyList<JsonProperty> EmptyProperties = System.Array.Empty<JsonProperty>();
    private static readonly IReadOnlyList<JsonValue> EmptyItems = System.Array.Empty<JsonValue>();

    private readonly IReadOnlyList<JsonProperty> _properties;
    private readonly IReadOnlyList<JsonValue> _items;
    private readonly string _stringValue;
    private readonly double _numberValue;
    private readonly bool _boolValue;

    private JsonValue(
        JsonValueKind kind,
        IReadOnlyList<JsonProperty> properties,
        IReadOnlyList<JsonValue> items,
        string stringValue,
        double numberValue,
        bool boolValue)
    {
        Kind = kind;
        _properties = properties;
        _items = items;
        _stringValue = stringValue;
        _numberValue = numberValue;
        _boolValue = boolValue;
    }

    /// <summary>The JSON null literal.</summary>
    public static JsonValue Null { get; } = new(JsonValueKind.Null, EmptyProperties, EmptyItems, string.Empty, 0, false);

    public JsonValueKind Kind { get; }

    /// <summary>Object members in document order; empty for non-objects.</summary>
    public IReadOnlyList<JsonProperty> Properties => Kind == JsonValueKind.Object ? _properties : EmptyProperties;

    /// <summary>Array elements in document order; empty for non-arrays.</summary>
    public IReadOnlyList<JsonValue> Items => Kind == JsonValueKind.Array ? _items : EmptyItems;

    public string StringValue => Kind == JsonValueKind.String
        ? _stringValue
        : throw new InvalidOperationException($"Expected a string node but found {Kind}.");

    public double NumberValue => Kind == JsonValueKind.Number
        ? _numberValue
        : throw new InvalidOperationException($"Expected a number node but found {Kind}.");

    public bool BoolValue => Kind == JsonValueKind.Bool
        ? _boolValue
        : throw new InvalidOperationException($"Expected a bool node but found {Kind}.");

    public static JsonValue Object(IEnumerable<JsonProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return new JsonValue(JsonValueKind.Object, properties.ToArray(), EmptyItems, string.Empty, 0, false);
    }

    public static JsonValue Object(params JsonProperty[] properties) => Object((IEnumerable<JsonProperty>)properties);

    public static JsonValue Array(IEnumerable<JsonValue> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new JsonValue(JsonValueKind.Array, EmptyProperties, items.ToArray(), string.Empty, 0, false);
    }

    public static JsonValue Array(params JsonValue[] items) => Array((IEnumerable<JsonValue>)items);

    public static JsonValue String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonValue(JsonValueKind.String, EmptyProperties, EmptyItems, value, 0, false);
    }

    public static JsonValue Number(double value) =>
        new(JsonValueKind.Number, EmptyProperties, EmptyItems, string.Empty, value, false);

    public static JsonValue Bool(bool value) =>
        new(JsonValueKind.Bool, EmptyProperties, EmptyItems, string.Empty, 0, value);

    /// <summary>Looks up an object member by name; false for non-objects or absent names.</summary>
    public bool TryGetProperty(string name, out JsonValue value)
    {
        foreach (JsonProperty property in _properties)
        {
            if (property.Name == name)
            {
                value = property.Value;
                return true;
            }
        }

        value = Null;
        return false;
    }
}
