using System.Globalization;
using System.Text;

namespace Delver.Import.RelaxedJson;

/// <summary>
/// Tolerant reader for the relaxed JSON dialect the donor ships in its
/// <c>.dat</c> content files. Donor: com.interrupt.utils.JsonUtil / libGDX
/// Json relaxed syntax — unquoted keys and values (e.g.
/// <c>class:com.interrupt.dungeoneer.entities.Door</c>), trailing commas,
/// and (in <c>data/messages/wizard.dat</c>) missing commas between array
/// elements. Strict JSON is a subset of what this reader accepts.
/// </summary>
public static class RelaxedJsonReader
{
    // Recursion guard: donor documents nest only a few levels deep, so a
    // generous cap turns hostile input into a clean error instead of a crash.
    private const int MaxDepth = 512;

    /// <summary>Parses UTF-8 text into an immutable DOM.</summary>
    /// <exception cref="RelaxedJsonException">Input is malformed.</exception>
    public static JsonValue Parse(ReadOnlySpan<byte> utf8)
    {
        // Replacement fallback: donor bytes are UTF-8 but never worth failing on.
        return Parse(Encoding.UTF8.GetString(utf8));
    }

    /// <summary>Parses text into an immutable DOM.</summary>
    /// <exception cref="RelaxedJsonException">Input is malformed.</exception>
    public static JsonValue Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Parser parser = new(text);
        return parser.ParseDocument();
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _position;

        public Parser(string text) => _text = text;

        public JsonValue ParseDocument()
        {
            JsonValue value = ParseValue(0);
            SkipTrivia();
            if (_position < _text.Length)
            {
                throw Error("Unexpected trailing content after the top-level value.");
            }

            return value;
        }

        private JsonValue ParseValue(int depth)
        {
            SkipTrivia();
            if (_position >= _text.Length)
            {
                throw Error("Unexpected end of input; expected a value.");
            }

            return _text[_position] switch
            {
                '{' => ParseObject(depth + 1),
                '[' => ParseArray(depth + 1),
                '"' => JsonValue.String(ParseQuotedString()),
                _ => ParseBareValue(),
            };
        }

        private JsonValue ParseObject(int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error($"Maximum nesting depth of {MaxDepth} exceeded.");
            }

            _position++; // '{'
            List<JsonProperty> properties = new();
            while (true)
            {
                SkipTrivia();
                if (_position >= _text.Length)
                {
                    throw Error("Unterminated object.");
                }

                if (_text[_position] == '}')
                {
                    _position++;
                    break;
                }

                string name = ParseObjectKey();
                SkipTrivia();
                if (_position >= _text.Length || _text[_position] != ':')
                {
                    throw Error("Expected ':' after object key.");
                }

                _position++;
                properties.Add(new JsonProperty(name, ParseValue(depth)));
                SkipTrivia();
                if (_position >= _text.Length)
                {
                    throw Error("Unterminated object.");
                }

                char c = _text[_position];
                if (c == ',')
                {
                    _position++;
                    continue;
                }

                if (c == '}')
                {
                    _position++;
                    break;
                }

                // Donor: data/messages/wizard.dat omits commas between elements;
                // the same tolerance covers object members.
            }

            return JsonValue.Object(properties);
        }

        private JsonValue ParseArray(int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error($"Maximum nesting depth of {MaxDepth} exceeded.");
            }

            _position++; // '['
            List<JsonValue> items = new();
            while (true)
            {
                SkipTrivia();
                if (_position >= _text.Length)
                {
                    throw Error("Unterminated array.");
                }

                if (_text[_position] == ']')
                {
                    _position++;
                    break;
                }

                items.Add(ParseValue(depth));
                SkipTrivia();
                if (_position >= _text.Length)
                {
                    throw Error("Unterminated array.");
                }

                char c = _text[_position];
                if (c == ',')
                {
                    _position++;
                    continue;
                }

                if (c == ']')
                {
                    _position++;
                    break;
                }

                // Tolerated missing separator between elements (wizard.dat).
            }

            return JsonValue.Array(items);
        }

        private string ParseObjectKey()
        {
            SkipTrivia();
            if (_position < _text.Length && _text[_position] == '"')
            {
                return ParseQuotedString();
            }

            int start = _position;
            while (_position < _text.Length && _text[_position] != ':')
            {
                char c = _text[_position];
                if (c == ',' || c == '}' || c == ']')
                {
                    throw Error("Expected ':' after object key.");
                }

                _position++;
            }

            if (_position >= _text.Length)
            {
                throw Error("Unterminated object key; expected ':'.");
            }

            string key = _text[start.._position].Trim();
            if (key.Length == 0)
            {
                throw Error("Expected ':' after object key.");
            }

            return key;
        }

        private string ParseQuotedString()
        {
            _position++; // '"'
            StringBuilder builder = new();
            while (true)
            {
                if (_position >= _text.Length)
                {
                    throw Error("Unterminated string.");
                }

                char c = _text[_position];
                if (c == '"')
                {
                    _position++;
                    return builder.ToString();
                }

                if (c == '\n' || c == '\r')
                {
                    throw Error("Unterminated string.");
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    _position++;
                    continue;
                }

                _position++;
                if (_position >= _text.Length)
                {
                    throw Error("Unterminated escape sequence.");
                }

                char escape = _text[_position];
                _position++;
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': builder.Append(ParseUnicodeEscape()); break;
                    default: throw Error($"Invalid escape sequence '\\{escape}'.");
                }
            }
        }

        private char ParseUnicodeEscape()
        {
            if (_position + 4 > _text.Length)
            {
                throw Error("Unterminated unicode escape sequence.");
            }

            int code = 0;
            for (int i = 0; i < 4; i++)
            {
                char c = _text[_position + i];
                int digit = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'a' and <= 'f' => c - 'a' + 10,
                    >= 'A' and <= 'F' => c - 'A' + 10,
                    _ => throw Error("Invalid unicode escape sequence."),
                };
                code = (code << 4) | digit;
            }

            _position += 4;
            return (char)code;
        }

        private JsonValue ParseBareValue()
        {
            int start = _position;
            while (_position < _text.Length)
            {
                char c = _text[_position];
                if (c == ',' || c == '}' || c == ']')
                {
                    break;
                }

                // A comment terminates a bare value; SkipTrivia handles it next.
                if (c == '/' && _position + 1 < _text.Length &&
                    (_text[_position + 1] == '/' || _text[_position + 1] == '*'))
                {
                    break;
                }

                _position++;
            }

            string token = _text[start.._position].Trim();
            if (token.Length == 0)
            {
                throw Error("Expected a value.");
            }

            if (token == "true")
            {
                return JsonValue.Bool(true);
            }

            if (token == "false")
            {
                return JsonValue.Bool(false);
            }

            if (token == "null")
            {
                return JsonValue.Null;
            }

            if (TryParseNumber(token, out double number))
            {
                return JsonValue.Number(number);
            }

            // Donor: unquoted string values such as
            // {class:com.interrupt.dungeoneer.entities.Door} and
            // category:Cave Sets (spaces included, so the token is greedy).
            return JsonValue.String(token);
        }

        private static bool TryParseNumber(string token, out double value)
        {
            value = 0;
            int i = 0;
            if (i < token.Length && (token[i] == '-' || token[i] == '+'))
            {
                i++;
            }

            int digits = 0;
            while (i < token.Length && char.IsAsciiDigit(token[i]))
            {
                i++;
                digits++;
            }

            if (i < token.Length && token[i] == '.')
            {
                i++;
                while (i < token.Length && char.IsAsciiDigit(token[i]))
                {
                    i++;
                    digits++;
                }
            }

            if (digits == 0)
            {
                return false;
            }

            if (i < token.Length && (token[i] == 'e' || token[i] == 'E'))
            {
                i++;
                if (i < token.Length && (token[i] == '-' || token[i] == '+'))
                {
                    i++;
                }

                int exponentDigits = 0;
                while (i < token.Length && char.IsAsciiDigit(token[i]))
                {
                    i++;
                    exponentDigits++;
                }

                if (exponentDigits == 0)
                {
                    return false;
                }
            }

            if (i != token.Length)
            {
                return false;
            }

            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void SkipTrivia()
        {
            while (_position < _text.Length)
            {
                char c = _text[_position];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    _position++;
                    continue;
                }

                if (c == '/' && _position + 1 < _text.Length)
                {
                    char next = _text[_position + 1];
                    if (next == '/')
                    {
                        _position += 2;
                        while (_position < _text.Length && _text[_position] != '\n')
                        {
                            _position++;
                        }

                        continue;
                    }

                    if (next == '*')
                    {
                        int start = _position;
                        _position += 2;
                        while (_position + 1 < _text.Length &&
                               !(_text[_position] == '*' && _text[_position + 1] == '/'))
                        {
                            _position++;
                        }

                        if (_position + 1 >= _text.Length)
                        {
                            throw Error("Unterminated block comment.", start);
                        }

                        _position += 2;
                        continue;
                    }
                }

                break;
            }
        }

        private RelaxedJsonException Error(string message, int position = -1)
        {
            int p = position >= 0 ? position : _position;
            int line = 1;
            int column = 1;
            for (int i = 0; i < p && i < _text.Length; i++)
            {
                if (_text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return new RelaxedJsonException(message, line, column, p);
        }
    }
}
