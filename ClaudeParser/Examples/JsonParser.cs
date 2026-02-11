using ClaudeParser.Core;
using ClaudeParser.Combinators;
using System.Text;

namespace ClaudeParser.Examples;

/// <summary>
/// JSON (JavaScript Object Notation) パーサー
/// RFC 8259に準拠したJSONをパースします。
/// </summary>
public static class JsonParser
{
    /// <summary>
    /// JSONの値を表す型
    /// </summary>
    public abstract record JsonValue
    {
        /// <summary>
        /// インデント付きで文字列化します
        /// </summary>
        public abstract string ToPrettyString(int indent = 0);
    }

    public record JsonNull() : JsonValue
    {
        public override string ToString() => "null";
        public override string ToPrettyString(int indent = 0) => "null";
    }

    public record JsonBool(bool Value) : JsonValue
    {
        public override string ToString() => Value ? "true" : "false";
        public override string ToPrettyString(int indent = 0) => ToString();
    }

    public record JsonNumber(double Value) : JsonValue
    {
        public override string ToString() => Value.ToString();
        public override string ToPrettyString(int indent = 0) => ToString();
    }

    public record JsonString(string Value) : JsonValue
    {
        public override string ToString() => $"\"{EscapeString(Value)}\"";
        public override string ToPrettyString(int indent = 0) => ToString();

        private static string EscapeString(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                sb.Append(c switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    _ when c < ' ' => $"\\u{(int)c:X4}",
                    _ => c.ToString()
                });
            }
            return sb.ToString();
        }
    }

    public record JsonArray(IReadOnlyList<JsonValue> Elements) : JsonValue
    {
        public override string ToString() => $"[{string.Join(",", Elements)}]";
        public override string ToPrettyString(int indent = 0)
        {
            if (Elements.Count == 0) return "[]";
            var indentStr = new string(' ', indent + 2);
            var items = Elements.Select(e => indentStr + e.ToPrettyString(indent + 2));
            return $"[\n{string.Join(",\n", items)}\n{new string(' ', indent)}]";
        }
    }

    public record JsonObject(IReadOnlyDictionary<string, JsonValue> Properties) : JsonValue
    {
        public override string ToString()
        {
            var props = Properties.Select(kv => $"\"{kv.Key}\":{kv.Value}");
            return $"{{{string.Join(",", props)}}}";
        }
        public override string ToPrettyString(int indent = 0)
        {
            if (Properties.Count == 0) return "{}";
            var indentStr = new string(' ', indent + 2);
            var props = Properties.Select(kv => $"{indentStr}\"{kv.Key}\": {kv.Value.ToPrettyString(indent + 2)}");
            return $"{{\n{string.Join(",\n", props)}\n{new string(' ', indent)}}}";
        }
    }

    #region パーサー定義

    /// <summary>
    /// 空白をスキップ
    /// </summary>
    private static Parser<Unit, char> Whitespace =>
        CharParsers.TakeWhile(c => c == ' ' || c == '\t' || c == '\n' || c == '\r').Ignore();

    /// <summary>
    /// トークンの周囲の空白をスキップ
    /// </summary>
    private static Parser<T, char> Token<T>(Parser<T, char> parser) =>
        from _ in Whitespace
        from v in parser
        from __ in Whitespace
        select v;

    /// <summary>
    /// null
    /// </summary>
    private static Parser<JsonValue, char> JNull =>
        CharParsers.String("null")
                   .Select(_ => (JsonValue)new JsonNull())
                   .Named("null")
                   .WithExpected("null");

    /// <summary>
    /// true/false
    /// </summary>
    private static Parser<JsonValue, char> JBool =>
        CharParsers.String("true").Select(_ => (JsonValue)new JsonBool(true))
            .Or(CharParsers.String("false").Select(_ => (JsonValue)new JsonBool(false)))
            .Named("boolean")
            .WithExpected("true または false");

    /// <summary>
    /// 数値
    /// </summary>
    private static Parser<JsonValue, char> JNumber =>
        new Parser<JsonValue, char>((input, ctx) =>
        {
            var chars = new List<char>();
            var current = input;

            // 符号
            if (!current.IsAtEnd && current.Current == '-')
            {
                chars.Add('-');
                current = current.Advance();
            }

            // 整数部
            if (current.IsAtEnd)
                return ParseResult<JsonValue, char>.Failure(
                    new ParseError(input.Position, ErrorMessage.Expected("数値")), input);

            if (current.Current == '0')
            {
                chars.Add('0');
                current = current.Advance();
            }
            else if (current.Current >= '1' && current.Current <= '9')
            {
                while (!current.IsAtEnd && char.IsDigit(current.Current))
                {
                    chars.Add(current.Current);
                    current = current.Advance();
                }
            }
            else
            {
                return ParseResult<JsonValue, char>.Failure(
                    new ParseError(input.Position, ErrorMessage.Expected("数値")), input);
            }

            // 小数部
            if (!current.IsAtEnd && current.Current == '.')
            {
                chars.Add('.');
                current = current.Advance();
                
                if (current.IsAtEnd || !char.IsDigit(current.Current))
                {
                    return ParseResult<JsonValue, char>.Failure(
                        new ParseError(current.Position, ErrorMessage.Expected("小数部")), current);
                }
                
                while (!current.IsAtEnd && char.IsDigit(current.Current))
                {
                    chars.Add(current.Current);
                    current = current.Advance();
                }
            }

            // 指数部
            if (!current.IsAtEnd && (current.Current == 'e' || current.Current == 'E'))
            {
                chars.Add('e');
                current = current.Advance();
                
                if (!current.IsAtEnd && (current.Current == '+' || current.Current == '-'))
                {
                    chars.Add(current.Current);
                    current = current.Advance();
                }
                
                if (current.IsAtEnd || !char.IsDigit(current.Current))
                {
                    return ParseResult<JsonValue, char>.Failure(
                        new ParseError(current.Position, ErrorMessage.Expected("指数")), current);
                }
                
                while (!current.IsAtEnd && char.IsDigit(current.Current))
                {
                    chars.Add(current.Current);
                    current = current.Advance();
                }
            }

            if (double.TryParse(new string(chars.ToArray()), out var value))
                return ParseResult<JsonValue, char>.Success(new JsonNumber(value), current);

            return ParseResult<JsonValue, char>.Failure(
                new ParseError(input.Position, ErrorMessage.Expected("数値")), input);
        }, "number").WithExpected("数値");

    /// <summary>
    /// 文字列
    /// </summary>
    private static Parser<string, char> JStringContent =>
        new Parser<string, char>((input, ctx) =>
        {
            var sb = new StringBuilder();
            var current = input;

            while (!current.IsAtEnd)
            {
                var c = current.Current;
                
                if (c == '"')
                    break;
                
                if (c == '\\')
                {
                    current = current.Advance();
                    if (current.IsAtEnd)
                    {
                        return ParseResult<string, char>.Failure(
                            new ParseError(current.Position, ErrorMessage.Expected("エスケープ文字")), current);
                    }
                    
                    var escaped = current.Current;
                    current = current.Advance();
                    
                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            // Unicode escape: \uXXXX
                            var hex = new char[4];
                            for (int i = 0; i < 4; i++)
                            {
                                if (current.IsAtEnd || !IsHexDigit(current.Current))
                                {
                                    return ParseResult<string, char>.Failure(
                                        new ParseError(current.Position, ErrorMessage.Expected("4桁の16進数")), current);
                                }
                                hex[i] = current.Current;
                                current = current.Advance();
                            }
                            sb.Append((char)Convert.ToInt32(new string(hex), 16));
                            break;
                        default:
                            return ParseResult<string, char>.Failure(
                                new ParseError(current.Position, 
                                    ErrorMessage.Unexpected($"エスケープ文字 '\\{escaped}'")), current);
                    }
                }
                else if (c < ' ')
                {
                    return ParseResult<string, char>.Failure(
                        new ParseError(current.Position, 
                            ErrorMessage.Unexpected("制御文字")), current);
                }
                else
                {
                    sb.Append(c);
                    current = current.Advance();
                }
            }

            return ParseResult<string, char>.Success(sb.ToString(), current);
        }, "string-content");

    private static bool IsHexDigit(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static Parser<JsonValue, char> JString =>
        (from _ in CharParsers.Char('"')
         from content in JStringContent
         from __ in CharParsers.Char('"')
         select (JsonValue)new JsonString(content))
        .Named("string")
        .WithExpected("文字列");

    /// <summary>
    /// 配列
    /// </summary>
    private static Parser<JsonValue, char> JArray =>
        (from _ in Token(CharParsers.Char('['))
         from elements in Parsers.Lazy(() => JValue).SepBy(Token(CharParsers.Char(',')))
         from __ in Token(CharParsers.Char(']'))
         select (JsonValue)new JsonArray(elements))
        .Named("array")
        .WithExpected("配列");

    /// <summary>
    /// オブジェクト
    /// </summary>
    private static Parser<JsonValue, char> JObject =>
        (from _ in Token(CharParsers.Char('{'))
         from props in JProperty.SepBy(Token(CharParsers.Char(',')))
         from __ in Token(CharParsers.Char('}'))
         select (JsonValue)new JsonObject(props.ToDictionary(p => p.Key, p => p.Value)))
        .Named("object")
        .WithExpected("オブジェクト");

    private static Parser<KeyValuePair<string, JsonValue>, char> JProperty =>
        from key in Token(from _ in CharParsers.Char('"')
                          from content in JStringContent
                          from __ in CharParsers.Char('"')
                          select content)
        from _ in Token(CharParsers.Char(':'))
        from value in Parsers.Lazy(() => JValue)
        select new KeyValuePair<string, JsonValue>(key, value);

    /// <summary>
    /// JSON値
    /// </summary>
    public static Parser<JsonValue, char> JValue =>
        Token(
            JNull
            .Or(JBool)
            .Or(JNumber)
            .Or(JString.Try())
            .Or(JArray.Try())
            .Or(JObject.Try()))
        .Named("value")
        .WithExpected("JSON値");

    #endregion

    /// <summary>
    /// JSON全体をパースします
    /// </summary>
    public static Parser<JsonValue, char> Json =>
        from _ in Whitespace
        from v in JValue
        from __ in Whitespace
        from ___ in Parsers.Eof<char>()
        select v;

    /// <summary>
    /// JSON文字列をパースします
    /// </summary>
    public static JsonValue Parse(string json)
    {
        var input = new StringInputStream(json);
        var result = Json.Parse(input);
        return result.GetValueOrThrow(json);
    }

    /// <summary>
    /// JSON文字列をパースします（エラーの場合はnull）
    /// </summary>
    public static JsonValue? TryParse(string json)
    {
        try
        {
            return Parse(json);
        }
        catch (ParseException)
        {
            return null;
        }
    }
}
