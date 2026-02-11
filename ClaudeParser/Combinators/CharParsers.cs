using ClaudeParser.Core;

namespace ClaudeParser.Combinators;

/// <summary>
/// 文字（char）用のパーサーコンビネーター
/// </summary>
public static class CharParsers
{
    /// <summary>
    /// 指定した文字をパースします
    /// </summary>
    public static Parser<char, char> Char(char c) =>
        Parsers.Satisfy<char>(x => x == c, $"'{c}'").Named($"Char('{c}')");

    /// <summary>
    /// 指定した文字以外をパースします
    /// </summary>
    public static Parser<char, char> NoneOf(string chars) =>
        Parsers.Satisfy<char>(c => !chars.Contains(c), $"'{chars}'以外の文字").Named($"NoneOf(\"{chars}\")");

    /// <summary>
    /// 指定した文字のいずれかをパースします
    /// </summary>
    public static Parser<char, char> OneOf(string chars) =>
        Parsers.Satisfy<char>(c => chars.Contains(c), $"'{chars}'のいずれか").Named($"OneOf(\"{chars}\")");

    /// <summary>
    /// 指定した文字列をパースします
    /// </summary>
    public static Parser<string, char> String(string s) =>
        new((input, ctx) =>
        {
            var current = input;
            foreach (var c in s)
            {
                if (current.IsAtEnd || current.Current != c)
                {
                    // 失敗時は現在位置を返す（消費した入力を示すことでOrのバックトラックを制御）
                    return ParseResult<string, char>.Failure(
                        new ParseError(current.Position, ErrorMessage.Expected($"\"{ s }\"")),
                        current);
                }
                current = current.Advance();
            }
            return ParseResult<string, char>.Success(s, current);
        }, $"String(\"{s}\")");

    /// <summary>
    /// 大文字小文字を区別せずに文字列をパースします
    /// </summary>
    public static Parser<string, char> StringIgnoreCase(string s) =>
        new((input, ctx) =>
        {
            var current = input;
            foreach (var c in s)
            {
                if (current.IsAtEnd || char.ToLowerInvariant(current.Current) != char.ToLowerInvariant(c))
                {
                    return ParseResult<string, char>.Failure(
                        new ParseError(current.Position, ErrorMessage.Expected($"\"{ s }\" (大文字小文字無視)")),
                        current);
                }
                current = current.Advance();
            }
            return ParseResult<string, char>.Success(s, current);
        }, $"StringIgnoreCase(\"{s}\")");

    /// <summary>
    /// 任意の1文字をパースします
    /// </summary>
    public static Parser<char, char> AnyChar => 
        Parsers.AnyToken<char>().Named("AnyChar");

    /// <summary>
    /// 英字をパースします
    /// </summary>
    public static Parser<char, char> Letter =>
        Parsers.Satisfy<char>(char.IsLetter, "英字").Named("Letter");

    /// <summary>
    /// 数字をパースします
    /// </summary>
    public static Parser<char, char> Digit =>
        Parsers.Satisfy<char>(char.IsDigit, "数字").Named("Digit");

    /// <summary>
    /// 16進数字をパースします
    /// </summary>
    public static Parser<char, char> HexDigit =>
        Parsers.Satisfy<char>(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'), "16進数字")
               .Named("HexDigit");

    /// <summary>
    /// 8進数字をパースします
    /// </summary>
    public static Parser<char, char> OctDigit =>
        Parsers.Satisfy<char>(c => c >= '0' && c <= '7', "8進数字").Named("OctDigit");

    /// <summary>
    /// 英数字をパースします
    /// </summary>
    public static Parser<char, char> AlphaNum =>
        Parsers.Satisfy<char>(char.IsLetterOrDigit, "英数字").Named("AlphaNum");

    /// <summary>
    /// 空白文字をパースします
    /// </summary>
    public static Parser<char, char> Space =>
        Parsers.Satisfy<char>(char.IsWhiteSpace, "空白").Named("Space");

    /// <summary>
    /// 空白文字（改行以外）をパースします
    /// </summary>
    public static Parser<char, char> HorizontalSpace =>
        Parsers.Satisfy<char>(c => c == ' ' || c == '\t', "水平空白").Named("HorizontalSpace");

    /// <summary>
    /// 改行をパースします
    /// </summary>
    public static Parser<char, char> NewLine =>
        Char('\n').Or(Char('\r').ThenSkip(Char('\n').OptionalOr('\n')).Select(_ => '\n'))
                  .Named("NewLine");

    /// <summary>
    /// CRLFまたはLFをパースします
    /// </summary>
    public static Parser<string, char> EndOfLine =>
        String("\r\n").Or(String("\n")).Named("EndOfLine");

    /// <summary>
    /// 大文字をパースします
    /// </summary>
    public static Parser<char, char> Upper =>
        Parsers.Satisfy<char>(char.IsUpper, "大文字").Named("Upper");

    /// <summary>
    /// 小文字をパースします
    /// </summary>
    public static Parser<char, char> Lower =>
        Parsers.Satisfy<char>(char.IsLower, "小文字").Named("Lower");

    /// <summary>
    /// 連続した空白をスキップします
    /// </summary>
    public static Parser<Unit, char> Spaces =>
        Space.Many().Ignore().Named("Spaces");

    /// <summary>
    /// 連続した空白を少なくとも1つスキップします
    /// </summary>
    public static Parser<Unit, char> Spaces1 =>
        Space.Many1().Ignore().Named("Spaces1");

    /// <summary>
    /// 入力の終端か改行をパースします
    /// </summary>
    public static Parser<Unit, char> EndOfLineOrInput =>
        EndOfLine.Ignore().Or(Parsers.Eof<char>()).Named("EndOfLineOrInput");

    /// <summary>
    /// 条件を満たす文字が連続する部分を文字列としてパースします
    /// </summary>
    public static Parser<string, char> TakeWhile(Func<char, bool> predicate, string name = "条件を満たす文字列") =>
        Parsers.Satisfy<char>(predicate, name).Many()
               .Select(chars => new string(chars.ToArray()))
               .Named($"TakeWhile({name})");

    /// <summary>
    /// 条件を満たす文字が1つ以上連続する部分を文字列としてパースします
    /// </summary>
    public static Parser<string, char> TakeWhile1(Func<char, bool> predicate, string name = "条件を満たす文字列") =>
        Parsers.Satisfy<char>(predicate, name).Many1()
               .Select(chars => new string(chars.ToArray()))
               .Named($"TakeWhile1({name})");

    /// <summary>
    /// 指定した文字まで（その文字は含まない）をパースします
    /// </summary>
    public static Parser<string, char> TakeUntil(char stopChar) =>
        TakeWhile(c => c != stopChar, $"'{stopChar}'以外").Named($"TakeUntil('{stopChar}')");

    /// <summary>
    /// 指定した文字列までをパースします（その文字列は含まない）
    /// </summary>
    public static Parser<string, char> TakeUntilString(string stopString) =>
        new((input, ctx) =>
        {
            var current = input;
            var result = new List<char>();
            
            while (!current.IsAtEnd)
            {
                // stopStringの先頭文字かチェック
                if (current.Current == stopString[0])
                {
                    // 完全一致をチェック
                    var temp = current;
                    bool match = true;
                    foreach (var c in stopString)
                    {
                        if (temp.IsAtEnd || temp.Current != c)
                        {
                            match = false;
                            break;
                        }
                        temp = temp.Advance();
                    }
                    if (match)
                        break;
                }
                result.Add(current.Current);
                current = current.Advance();
            }

            return ParseResult<string, char>.Success(new string(result.ToArray()), current);
        }, $"TakeUntilString(\"{stopString}\")");

    /// <summary>
    /// 整数をパースします（符号なし）
    /// </summary>
    public static Parser<long, char> UnsignedInteger =>
        Digit.Many1()
             .Select(digits => long.Parse(new string(digits.ToArray())))
             .Named("UnsignedInteger");

    /// <summary>
    /// 整数をパースします（符号あり）
    /// </summary>
    public static Parser<long, char> Integer =>
        from sign in Char('-').Or(Char('+')).OptionalOr('+')
        from digits in Digit.Many1()
        select long.Parse(new string(new[] { sign }.Concat(digits).ToArray()));

    /// <summary>
    /// 浮動小数点数をパースします
    /// </summary>
    public static Parser<double, char> Double =>
        new((input, ctx) =>
        {
            var signResult = Char('-').Or(Char('+')).OptionalOr('+').Parse(input, ctx);
            if (signResult is not SuccessResult<char, char> signSuccess)
                return ParseResult<double, char>.Failure(
                    new ParseError(input.Position, ErrorMessage.Expected("数値")), input);

            var intPartResult = Digit.Many1().Parse(signSuccess.Remaining, ctx);
            if (intPartResult is not SuccessResult<IReadOnlyList<char>, char> intSuccess)
                return ParseResult<double, char>.Failure(
                    new ParseError(input.Position, ErrorMessage.Expected("数値")), input);

            var chars = new List<char>();
            if (signSuccess.Value == '-')
                chars.Add('-');
            chars.AddRange(intSuccess.Value);

            var current = intSuccess.Remaining;

            // 小数部
            if (!current.IsAtEnd && current.Current == '.')
            {
                chars.Add('.');
                current = current.Advance();
                
                var fracResult = Digit.Many().Parse(current, ctx);
                if (fracResult is SuccessResult<IReadOnlyList<char>, char> fracSuccess)
                {
                    chars.AddRange(fracSuccess.Value);
                    current = fracSuccess.Remaining;
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

                var expResult = Digit.Many1().Parse(current, ctx);
                if (expResult is SuccessResult<IReadOnlyList<char>, char> expSuccess)
                {
                    chars.AddRange(expSuccess.Value);
                    current = expSuccess.Remaining;
                }
            }

            if (double.TryParse(new string(chars.ToArray()), out var value))
                return ParseResult<double, char>.Success(value, current);

            return ParseResult<double, char>.Failure(
                new ParseError(input.Position, ErrorMessage.Expected("浮動小数点数")), input);
        }, "Double");

    /// <summary>
    /// 識別子をパースします（英字またはアンダースコアで始まり、英数字またはアンダースコアが続く）
    /// </summary>
    public static Parser<string, char> Identifier =>
        from first in Letter.Or(Char('_'))
        from rest in AlphaNum.Or(Char('_')).Many()
        select new string(new[] { first }.Concat(rest).ToArray());

    /// <summary>
    /// クォートで囲まれた文字列をパースします（エスケープ対応）
    /// </summary>
    public static Parser<string, char> QuotedString(char quote = '"', char escape = '\\')
    {
        var normalChar = NoneOf($"{quote}{escape}");
        var escapedChar = from _ in Char(escape)
                          from c in AnyChar
                          select c switch
                          {
                              'n' => '\n',
                              'r' => '\r',
                              't' => '\t',
                              '\\' => '\\',
                              _ when c == quote => quote,
                              _ => c
                          };
        
        return from _ in Char(quote)
               from content in normalChar.Or(escapedChar).Many()
               from __ in Char(quote)
               select new string(content.ToArray());
    }

    /// <summary>
    /// 指定したパーサーの周囲の空白をスキップします
    /// </summary>
    public static Parser<T, char> Lexeme<T>(this Parser<T, char> parser) =>
        parser.ThenSkip(Spaces);

    /// <summary>
    /// シンボル（空白で囲まれた文字列）をパースします
    /// </summary>
    public static Parser<string, char> Symbol(string s) =>
        String(s).Lexeme();

    /// <summary>
    /// 括弧で囲まれたパーサー
    /// </summary>
    public static Parser<T, char> Parens<T>(Parser<T, char> parser) =>
        Parsers.Between(Symbol("("), Symbol(")"), parser);

    /// <summary>
    /// 角括弧で囲まれたパーサー
    /// </summary>
    public static Parser<T, char> Brackets<T>(Parser<T, char> parser) =>
        Parsers.Between(Symbol("["), Symbol("]"), parser);

    /// <summary>
    /// 波括弧で囲まれたパーサー
    /// </summary>
    public static Parser<T, char> Braces<T>(Parser<T, char> parser) =>
        Parsers.Between(Symbol("{"), Symbol("}"), parser);

    /// <summary>
    /// コンマ区切りのリスト
    /// </summary>
    public static Parser<IReadOnlyList<T>, char> CommaSeparated<T>(Parser<T, char> parser) =>
        parser.SepBy(Symbol(","));

    /// <summary>
    /// セミコロン区切りのリスト
    /// </summary>
    public static Parser<IReadOnlyList<T>, char> SemicolonSeparated<T>(Parser<T, char> parser) =>
        parser.SepBy(Symbol(";"));
}
