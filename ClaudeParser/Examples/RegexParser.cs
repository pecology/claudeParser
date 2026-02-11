using ClaudeParser.Core;
using ClaudeParser.Combinators;
using System.Text;

namespace ClaudeParser.Examples;

/// <summary>
/// 正規表現パーサー
/// 正規表現文字列をパースし、それを適用可能なパーサーに変換します。
/// 
/// サポートする構文:
/// - リテラル文字
/// - エスケープ: \n, \r, \t, \d, \w, \s, \\, など
/// - 文字クラス: [abc], [a-z], [^abc]
/// - 量指定子: *, +, ?, {n}, {n,}, {n,m}
/// - 選択: a|b
/// - グループ: (...)
/// - アンカー: ^, $
/// - メタ文字: . (任意の1文字)
/// </summary>
public static class RegexParser
{
    #region 正規表現のAST

    /// <summary>
    /// 正規表現の抽象構文木
    /// </summary>
    public abstract record RegexNode
    {
        /// <summary>
        /// このノードに対応するパーサーを生成します
        /// </summary>
        public abstract Parser<string, char> ToParser();
    }

    /// <summary>
    /// リテラル文字
    /// </summary>
    public record LiteralNode(char Char) : RegexNode
    {
        public override Parser<string, char> ToParser() =>
            CharParsers.Char(Char).Select(c => c.ToString()).Named($"Literal('{Char}')");
    }

    /// <summary>
    /// 任意の1文字（.）
    /// </summary>
    public record AnyCharNode() : RegexNode
    {
        public override Parser<string, char> ToParser() =>
            Parsers.Satisfy<char>(c => c != '\n', "任意の文字")
                   .Select(c => c.ToString())
                   .Named("AnyChar");
    }

    /// <summary>
    /// 文字クラス（[abc]や[a-z]）
    /// </summary>
    public record CharClassNode(IReadOnlyList<CharClassItem> Items, bool Negated) : RegexNode
    {
        public override Parser<string, char> ToParser()
        {
            bool Matches(char c)
            {
                foreach (var item in Items)
                {
                    if (item.Matches(c))
                        return !Negated;
                }
                return Negated;
            }

            var description = Negated ? $"[^{string.Join("", Items)}]" : $"[{string.Join("", Items)}]";
            return Parsers.Satisfy<char>(Matches, description)
                          .Select(c => c.ToString())
                          .Named($"CharClass({description})");
        }
    }

    /// <summary>
    /// 文字クラスの要素
    /// </summary>
    public abstract record CharClassItem
    {
        public abstract bool Matches(char c);
    }

    /// <summary>
    /// 単一文字
    /// </summary>
    public record SingleChar(char Char) : CharClassItem
    {
        public override bool Matches(char c) => c == Char;
        public override string ToString() => Char.ToString();
    }

    /// <summary>
    /// 文字範囲（a-z）
    /// </summary>
    public record CharRange(char Start, char End) : CharClassItem
    {
        public override bool Matches(char c) => c >= Start && c <= End;
        public override string ToString() => $"{Start}-{End}";
    }

    /// <summary>
    /// 連結
    /// </summary>
    public record SequenceNode(IReadOnlyList<RegexNode> Nodes) : RegexNode
    {
        public override Parser<string, char> ToParser()
        {
            if (Nodes.Count == 0)
                return Parsers.Return<string, char>("");

            var parser = Nodes[0].ToParser();
            for (int i = 1; i < Nodes.Count; i++)
            {
                var next = Nodes[i].ToParser();
                parser = from a in parser
                         from b in next
                         select a + b;
            }
            return parser.Named("Sequence");
        }
    }

    /// <summary>
    /// 選択（|）
    /// </summary>
    public record AlternationNode(IReadOnlyList<RegexNode> Alternatives) : RegexNode
    {
        public override Parser<string, char> ToParser()
        {
            if (Alternatives.Count == 0)
                return Parsers.Return<string, char>("");
            if (Alternatives.Count == 1)
                return Alternatives[0].ToParser();

            return Parsers.Choice(Alternatives.Select(a => a.ToParser().Try())).Named("Alternation");
        }
    }

    /// <summary>
    /// 繰り返し
    /// </summary>
    public record RepeatNode(RegexNode Inner, int Min, int? Max, bool Greedy) : RegexNode
    {
        public override Parser<string, char> ToParser()
        {
            var inner = Inner.ToParser();

            // 特別なケース
            if (Min == 0 && Max == 1)
            {
                // ?
                return inner.OptionalOr("").Named("Optional");
            }
            if (Min == 0 && Max == null)
            {
                // *
                return inner.Many().Select(xs => string.Concat(xs)).Named("ZeroOrMore");
            }
            if (Min == 1 && Max == null)
            {
                // +
                return inner.Many1().Select(xs => string.Concat(xs)).Named("OneOrMore");
            }

            // 一般的な繰り返し {n,m}
            return new Parser<string, char>((input, ctx) =>
            {
                var results = new List<string>();
                var current = input;
                int count = 0;

                while (Max == null || count < Max)
                {
                    var result = inner.Parse(current, ctx);
                    if (result is not SuccessResult<string, char> success)
                    {
                        if (count < Min)
                        {
                            return ParseResult<string, char>.Failure(
                                new ParseError(current.Position,
                                    ErrorMessage.Expected($"少なくとも{Min}回のマッチ")),
                                current);
                        }
                        break;
                    }
                    
                    // 入力を消費していない場合は無限ループを防ぐ
                    if (success.Remaining.Equals(current))
                        break;

                    results.Add(success.Value);
                    current = success.Remaining;
                    count++;
                }

                return ParseResult<string, char>.Success(string.Concat(results), current);
            }, $"Repeat({Min}, {Max?.ToString() ?? "∞"})");
        }
    }

    /// <summary>
    /// グループ
    /// </summary>
    public record GroupNode(RegexNode Inner) : RegexNode
    {
        public override Parser<string, char> ToParser() =>
            Inner.ToParser().Named("Group");
    }

    /// <summary>
    /// 開始アンカー（^）
    /// </summary>
    public record StartAnchorNode() : RegexNode
    {
        public override Parser<string, char> ToParser() =>
            new Parser<string, char>((input, ctx) =>
            {
                if (input.Position.Column == 1)
                    return ParseResult<string, char>.Success("", input);
                return ParseResult<string, char>.Failure(
                    new ParseError(input.Position, ErrorMessage.Expected("行頭")), input);
            }, "StartAnchor");
    }

    /// <summary>
    /// 終了アンカー（$）
    /// </summary>
    public record EndAnchorNode() : RegexNode
    {
        public override Parser<string, char> ToParser() =>
            new Parser<string, char>((input, ctx) =>
            {
                if (input.IsAtEnd || input.Current == '\n')
                    return ParseResult<string, char>.Success("", input);
                return ParseResult<string, char>.Failure(
                    new ParseError(input.Position, ErrorMessage.Expected("行末")), input);
            }, "EndAnchor");
    }

    /// <summary>
    /// 特殊文字クラス（\d, \w, \s）
    /// </summary>
    public record SpecialClassNode(char Type) : RegexNode
    {
        public override Parser<string, char> ToParser()
        {
            Func<char, bool> predicate = Type switch
            {
                'd' => char.IsDigit,
                'D' => c => !char.IsDigit(c),
                'w' => c => char.IsLetterOrDigit(c) || c == '_',
                'W' => c => !char.IsLetterOrDigit(c) && c != '_',
                's' => char.IsWhiteSpace,
                'S' => c => !char.IsWhiteSpace(c),
                _ => throw new InvalidOperationException($"不明な特殊文字クラス: \\{Type}")
            };
            
            var name = Type switch
            {
                'd' => "数字",
                'D' => "非数字",
                'w' => "単語文字",
                'W' => "非単語文字",
                's' => "空白",
                'S' => "非空白",
                _ => $"\\{Type}"
            };

            return Parsers.Satisfy<char>(predicate, name)
                          .Select(c => c.ToString())
                          .Named($"SpecialClass(\\{Type})");
        }
    }

    #endregion

    #region 正規表現構文のパーサー

    /// <summary>
    /// エスケープ文字
    /// </summary>
    private static Parser<RegexNode, char> EscapeParser =>
        from _ in CharParsers.Char('\\')
        from c in CharParsers.AnyChar
        select (RegexNode)(c switch
        {
            'd' or 'D' or 'w' or 'W' or 's' or 'S' => new SpecialClassNode(c),
            'n' => new LiteralNode('\n'),
            'r' => new LiteralNode('\r'),
            't' => new LiteralNode('\t'),
            _ => new LiteralNode(c)
        });

    /// <summary>
    /// メタ文字
    /// </summary>
    private static readonly string MetaChars = @"\.^$|*+?()[]{}";

    /// <summary>
    /// リテラル文字（メタ文字以外）
    /// </summary>
    private static Parser<RegexNode, char> LiteralParser =>
        Parsers.Satisfy<char>(c => !MetaChars.Contains(c), "リテラル文字")
               .Select(c => (RegexNode)new LiteralNode(c))
               .Named("Literal");

    /// <summary>
    /// 任意の文字（.）
    /// </summary>
    private static Parser<RegexNode, char> AnyCharParser =>
        CharParsers.Char('.').Select(_ => (RegexNode)new AnyCharNode());

    /// <summary>
    /// 文字クラス内のエスケープ
    /// </summary>
    private static Parser<char, char> CharClassEscape =>
        from _ in CharParsers.Char('\\')
        from c in CharParsers.AnyChar
        select c switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            _ => c
        };

    /// <summary>
    /// 文字クラス
    /// </summary>
    private static Parser<RegexNode, char> CharClassParser =>
        from _ in CharParsers.Char('[')
        from negated in CharParsers.Char('^').OptionalValue()
        from items in CharClassItemParser.Many1()
        from __ in CharParsers.Char(']')
        select (RegexNode)new CharClassNode(items, negated.HasValue);

    private static Parser<CharClassItem, char> CharClassItemParser =>
        (from start in CharClassEscape.Or(CharParsers.NoneOf(@"]\"))
         from range in (from _ in CharParsers.Char('-')
                        from end in CharClassEscape.Or(CharParsers.NoneOf(@"]\"))
                        select end).OptionalValue()
         select range.HasValue
             ? (CharClassItem)new CharRange(start, range.Value)
             : new SingleChar(start)).Named("CharClassItem");

    /// <summary>
    /// グループ
    /// </summary>
    private static Parser<RegexNode, char> GroupParser =>
        from _ in CharParsers.Char('(')
        from inner in Parsers.Lazy(() => RegexExpr)
        from __ in CharParsers.Char(')')
        select (RegexNode)new GroupNode(inner);

    /// <summary>
    /// アンカー
    /// </summary>
    private static Parser<RegexNode, char> AnchorParser =>
        CharParsers.Char('^').Select(_ => (RegexNode)new StartAnchorNode())
            .Or(CharParsers.Char('$').Select(_ => (RegexNode)new EndAnchorNode()));

    /// <summary>
    /// アトム（基本要素）
    /// </summary>
    private static Parser<RegexNode, char> Atom =>
        EscapeParser.Try()
            .Or(CharClassParser.Try())
            .Or(GroupParser.Try())
            .Or(AnchorParser.Try())
            .Or(AnyCharParser)
            .Or(LiteralParser);

    /// <summary>
    /// 量指定子
    /// </summary>
    private static Parser<(int min, int? max), char> QuantifierParser =>
        CharParsers.Char('*').Select(_ => (0, (int?)null))
            .Or(CharParsers.Char('+').Select(_ => (1, (int?)null)))
            .Or(CharParsers.Char('?').Select(_ => (0, (int?)1)))
            .Or(from _ in CharParsers.Char('{')
                from min in CharParsers.UnsignedInteger
                from maxPart in (from __ in CharParsers.Char(',')
                                 from m in CharParsers.UnsignedInteger.Select(n => (long?)n).OptionalOr(null)
                                 select m).OptionalOr((long?)min)
                from ___ in CharParsers.Char('}')
                select maxPart == min && !maxPart.HasValue
                    ? ((int)min, (int?)min) // {n} の場合
                    : ((int)min, maxPart.HasValue ? (int?)maxPart.Value : null)); // {n,m} または {n,} の場合

    /// <summary>
    /// 量指定子付きアトム
    /// </summary>
    private static Parser<RegexNode, char> QuantifiedAtom =>
        from atom in Atom
        from quantifier in QuantifierParser.OptionalValue()
        select quantifier.HasValue
            ? (RegexNode)new RepeatNode(atom, quantifier.Value.min, quantifier.Value.max, true)
            : atom;

    /// <summary>
    /// 連結
    /// </summary>
    private static Parser<RegexNode, char> Sequence =>
        QuantifiedAtom.Many()
            .Select(nodes => nodes.Count == 1
                ? nodes[0]
                : (RegexNode)new SequenceNode(nodes))
            .Named("Sequence");

    /// <summary>
    /// 選択（|）
    /// </summary>
    private static Parser<RegexNode, char> RegexExpr =>
        Sequence.SepBy1(CharParsers.Char('|'))
            .Select(alts => alts.Count == 1
                ? alts[0]
                : (RegexNode)new AlternationNode(alts))
            .Named("Alternation");

    #endregion

    /// <summary>
    /// 正規表現パターンをパースしてASTを返します
    /// </summary>
    public static RegexNode ParsePattern(string pattern)
    {
        var input = new StringInputStream(pattern, "<regex>");
        var parser = from expr in RegexExpr
                     from _ in Parsers.Eof<char>()
                     select expr;
        var result = parser.Parse(input);
        return result.GetValueOrThrow(pattern);
    }

    /// <summary>
    /// 正規表現パターンからパーサーを生成します
    /// </summary>
    public static Parser<string, char> Compile(string pattern) =>
        ParsePattern(pattern).ToParser().Named($"Regex({pattern})");

    /// <summary>
    /// 正規表現で文字列をマッチします
    /// </summary>
    public static string? Match(string pattern, string input)
    {
        var parser = Compile(pattern);
        var stream = new StringInputStream(input);
        var result = parser.Parse(stream);
        
        return result switch
        {
            SuccessResult<string, char> s => s.Value,
            _ => null
        };
    }

    /// <summary>
    /// 正規表現で文字列全体がマッチするか確認します
    /// </summary>
    public static bool IsMatch(string pattern, string input)
    {
        var parser = from m in Compile(pattern)
                     from _ in Parsers.Eof<char>()
                     select m;
        var stream = new StringInputStream(input);
        var result = parser.Parse(stream);
        return result.IsSuccess;
    }

    /// <summary>
    /// 入力文字列内のすべてのマッチを返します
    /// </summary>
    public static IEnumerable<(int position, string match)> FindAll(string pattern, string input)
    {
        var parser = Compile(pattern);
        IInputStream<char> current = new StringInputStream(input);
        int position = 0;

        while (!current.IsAtEnd)
        {
            var result = parser.Parse(current);
            if (result is SuccessResult<string, char> success && success.Value.Length > 0)
            {
                yield return (position, success.Value);
                current = success.Remaining;
                position = (int)current.Position.Offset;
            }
            else
            {
                current = current.Advance();
                position++;
            }
        }
    }
}
