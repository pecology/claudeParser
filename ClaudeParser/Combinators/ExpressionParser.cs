using ClaudeParser.Core;

namespace ClaudeParser.Combinators;

/// <summary>
/// 演算子の結合規則
/// </summary>
public enum Associativity
{
    /// <summary>左結合（例: 1-2-3 = (1-2)-3）</summary>
    Left,
    /// <summary>右結合（例: 2^3^4 = 2^(3^4)）</summary>
    Right,
    /// <summary>結合なし（同じ優先度の演算子が連続するとエラー）</summary>
    None
}

/// <summary>
/// 演算子の種類
/// </summary>
public enum OperatorType
{
    /// <summary>中置演算子（例: +, -, *, /）</summary>
    Infix,
    /// <summary>前置演算子（例: -, !）</summary>
    Prefix,
    /// <summary>後置演算子（例: ++, --）</summary>
    Postfix
}

/// <summary>
/// 演算子の定義
/// </summary>
/// <typeparam name="T">式の型</typeparam>
/// <typeparam name="TToken">入力トークンの型</typeparam>
public abstract class Operator<T, TToken>
{
    /// <summary>
    /// 演算子の種類
    /// </summary>
    public abstract OperatorType Type { get; }
}

/// <summary>
/// 中置演算子
/// </summary>
public class InfixOperator<T, TToken> : Operator<T, TToken>
{
    public override OperatorType Type => OperatorType.Infix;
    
    /// <summary>結合規則</summary>
    public Associativity Associativity { get; }
    
    /// <summary>演算子をパースし、二項関数を返すパーサー</summary>
    public Parser<Func<T, T, T>, TToken> Parser { get; }

    public InfixOperator(Associativity assoc, Parser<Func<T, T, T>, TToken> parser)
    {
        Associativity = assoc;
        Parser = parser;
    }
}

/// <summary>
/// 前置演算子
/// </summary>
public class PrefixOperator<T, TToken> : Operator<T, TToken>
{
    public override OperatorType Type => OperatorType.Prefix;
    
    /// <summary>演算子をパースし、単項関数を返すパーサー</summary>
    public Parser<Func<T, T>, TToken> Parser { get; }

    public PrefixOperator(Parser<Func<T, T>, TToken> parser)
    {
        Parser = parser;
    }
}

/// <summary>
/// 後置演算子
/// </summary>
public class PostfixOperator<T, TToken> : Operator<T, TToken>
{
    public override OperatorType Type => OperatorType.Postfix;
    
    /// <summary>演算子をパースし、単項関数を返すパーサー</summary>
    public Parser<Func<T, T>, TToken> Parser { get; }

    public PostfixOperator(Parser<Func<T, T>, TToken> parser)
    {
        Parser = parser;
    }
}

/// <summary>
/// 演算子テーブル（優先度の低い順にリスト化）
/// </summary>
/// <typeparam name="T">式の型</typeparam>
/// <typeparam name="TToken">入力トークンの型</typeparam>
public class OperatorTable<T, TToken>
{
    private readonly List<List<Operator<T, TToken>>> _levels = new();

    /// <summary>
    /// 演算子優先度のレベル数
    /// </summary>
    public int LevelCount => _levels.Count;

    /// <summary>
    /// 新しい優先度レベルを追加します（低い優先度から順に追加）
    /// </summary>
    public OperatorTable<T, TToken> AddLevel(params Operator<T, TToken>[] operators)
    {
        _levels.Add(operators.ToList());
        return this;
    }

    /// <summary>
    /// 指定レベルの演算子を取得します
    /// </summary>
    public IReadOnlyList<Operator<T, TToken>> GetLevel(int level) => _levels[level];

    /// <summary>
    /// 中置演算子を作成するヘルパー
    /// </summary>
    public static InfixOperator<T, TToken> Infix(
        Associativity assoc,
        Parser<Func<T, T, T>, TToken> parser) => new(assoc, parser);

    /// <summary>
    /// 中置演算子を作成するヘルパー（演算子記号から）
    /// </summary>
    public static InfixOperator<T, TToken> Infix(
        Associativity assoc,
        Parser<Unit, TToken> opParser,
        Func<T, T, T> combine) =>
        new(assoc, opParser.Select(_ => combine));

    /// <summary>
    /// 前置演算子を作成するヘルパー
    /// </summary>
    public static PrefixOperator<T, TToken> Prefix(
        Parser<Func<T, T>, TToken> parser) => new(parser);

    /// <summary>
    /// 前置演算子を作成するヘルパー（演算子記号から）
    /// </summary>
    public static PrefixOperator<T, TToken> Prefix(
        Parser<Unit, TToken> opParser,
        Func<T, T> apply) =>
        new(opParser.Select(_ => apply));

    /// <summary>
    /// 後置演算子を作成するヘルパー
    /// </summary>
    public static PostfixOperator<T, TToken> Postfix(
        Parser<Func<T, T>, TToken> parser) => new(parser);

    /// <summary>
    /// 後置演算子を作成するヘルパー（演算子記号から）
    /// </summary>
    public static PostfixOperator<T, TToken> Postfix(
        Parser<Unit, TToken> opParser,
        Func<T, T> apply) =>
        new(opParser.Select(_ => apply));
}

/// <summary>
/// 式パーサーのビルダー（Text.Parsec.Expr相当）
/// </summary>
public static class ExpressionParser
{
    /// <summary>
    /// 演算子テーブルと項パーサーから式パーサーを構築します。
    /// 
    /// 使用例（四則演算）：
    /// <code>
    /// var table = new OperatorTable&lt;double, char&gt;()
    ///     .AddLevel(
    ///         OperatorTable&lt;double, char&gt;.Infix(Associativity.Left, CharParsers.Symbol("+").Ignore(), (a, b) =&gt; a + b),
    ///         OperatorTable&lt;double, char&gt;.Infix(Associativity.Left, CharParsers.Symbol("-").Ignore(), (a, b) =&gt; a - b))
    ///     .AddLevel(
    ///         OperatorTable&lt;double, char&gt;.Infix(Associativity.Left, CharParsers.Symbol("*").Ignore(), (a, b) =&gt; a * b),
    ///         OperatorTable&lt;double, char&gt;.Infix(Associativity.Left, CharParsers.Symbol("/").Ignore(), (a, b) =&gt; a / b));
    /// 
    /// var expr = ExpressionParser.Build(table, termParser);
    /// </code>
    /// </summary>
    /// <param name="table">演算子テーブル（優先度の低い順）</param>
    /// <param name="term">項（原子式）のパーサー</param>
    /// <returns>式のパーサー</returns>
    public static Parser<T, TToken> Build<T, TToken>(
        OperatorTable<T, TToken> table,
        Parser<T, TToken> term)
    {
        // 優先度の高い方から低い方へ処理する
        // 各レベルで、そのレベルより優先度の高い式をオペランドとして扱う
        var currentLevel = term;

        for (int i = table.LevelCount - 1; i >= 0; i--)
        {
            currentLevel = BuildLevel(table.GetLevel(i), currentLevel);
        }

        return currentLevel.Named("Expression");
    }

    private static Parser<T, TToken> BuildLevel<T, TToken>(
        IReadOnlyList<Operator<T, TToken>> operators,
        Parser<T, TToken> higherPrecedence)
    {
        // このレベルの演算子を種類別に分類
        var infixLeft = new List<Parser<Func<T, T, T>, TToken>>();
        var infixRight = new List<Parser<Func<T, T, T>, TToken>>();
        var infixNone = new List<Parser<Func<T, T, T>, TToken>>();
        var prefix = new List<Parser<Func<T, T>, TToken>>();
        var postfix = new List<Parser<Func<T, T>, TToken>>();

        foreach (var op in operators)
        {
            switch (op)
            {
                case InfixOperator<T, TToken> infix:
                    switch (infix.Associativity)
                    {
                        case Associativity.Left:
                            infixLeft.Add(infix.Parser);
                            break;
                        case Associativity.Right:
                            infixRight.Add(infix.Parser);
                            break;
                        case Associativity.None:
                            infixNone.Add(infix.Parser);
                            break;
                    }
                    break;
                case PrefixOperator<T, TToken> pre:
                    prefix.Add(pre.Parser);
                    break;
                case PostfixOperator<T, TToken> post:
                    postfix.Add(post.Parser);
                    break;
            }
        }

        // 前置演算子パーサーを構築
        var prefixParser = prefix.Count > 0
            ? Parsers.Choice(prefix).Try()
            : (Parser<Func<T, T>, TToken>?)null;

        // 後置演算子パーサーを構築
        var postfixParser = postfix.Count > 0
            ? Parsers.Choice(postfix).Try()
            : (Parser<Func<T, T>, TToken>?)null;

        // 項パーサー（前置・後置演算子を適用）
        var termWithUnary = BuildTermWithUnary(higherPrecedence, prefixParser, postfixParser);

        // 中置演算子を適用
        if (infixLeft.Count > 0 || infixRight.Count > 0 || infixNone.Count > 0)
        {
            return BuildInfixExpr(termWithUnary, infixLeft, infixRight, infixNone);
        }

        return termWithUnary;
    }

    private static Parser<T, TToken> BuildTermWithUnary<T, TToken>(
        Parser<T, TToken> term,
        Parser<Func<T, T>, TToken>? prefixOp,
        Parser<Func<T, T>, TToken>? postfixOp)
    {
        return new Parser<T, TToken>((input, ctx) =>
        {
            // 前置演算子を収集
            var prefixOps = new List<Func<T, T>>();
            var current = input;

            if (prefixOp != null)
            {
                while (true)
                {
                    var prefixResult = prefixOp.Parse(current, ctx);
                    if (prefixResult is not SuccessResult<Func<T, T>, TToken> prefixSuccess)
                        break;
                    
                    // 入力を消費しているか確認
                    if (prefixSuccess.Remaining.Equals(current))
                        break;
                    
                    prefixOps.Add(prefixSuccess.Value);
                    current = prefixSuccess.Remaining;
                }
            }

            // 項をパース
            var termResult = term.Parse(current, ctx);
            if (termResult is not SuccessResult<T, TToken> termSuccess)
                return termResult;

            var value = termSuccess.Value;
            current = termSuccess.Remaining;

            // 後置演算子を収集して適用
            if (postfixOp != null)
            {
                while (true)
                {
                    var postfixResult = postfixOp.Parse(current, ctx);
                    if (postfixResult is not SuccessResult<Func<T, T>, TToken> postfixSuccess)
                        break;
                    
                    // 入力を消費しているか確認
                    if (postfixSuccess.Remaining.Equals(current))
                        break;
                    
                    value = postfixSuccess.Value(value);
                    current = postfixSuccess.Remaining;
                }
            }

            // 前置演算子を逆順で適用
            for (int i = prefixOps.Count - 1; i >= 0; i--)
            {
                value = prefixOps[i](value);
            }

            return ParseResult<T, TToken>.Success(value, current, termSuccess.Error);
        }, "TermWithUnary");
    }

    private static Parser<T, TToken> BuildInfixExpr<T, TToken>(
        Parser<T, TToken> term,
        List<Parser<Func<T, T, T>, TToken>> leftOps,
        List<Parser<Func<T, T, T>, TToken>> rightOps,
        List<Parser<Func<T, T, T>, TToken>> noneOps)
    {
        // 左結合演算子
        var leftOpParser = leftOps.Count > 0 ? Parsers.Choice(leftOps).Try() : null;
        // 右結合演算子
        var rightOpParser = rightOps.Count > 0 ? Parsers.Choice(rightOps).Try() : null;
        // 結合なし演算子
        var noneOpParser = noneOps.Count > 0 ? Parsers.Choice(noneOps).Try() : null;

        return new Parser<T, TToken>((input, ctx) =>
        {
            var leftResult = term.Parse(input, ctx);
            if (leftResult is not SuccessResult<T, TToken> leftSuccess)
                return leftResult;

            var left = leftSuccess.Value;
            var current = leftSuccess.Remaining;

            while (true)
            {
                // 左結合演算子を試す
                if (leftOpParser != null)
                {
                    var opResult = leftOpParser.Parse(current, ctx);
                    if (opResult is SuccessResult<Func<T, T, T>, TToken> opSuccess)
                    {
                        var rightResult = term.Parse(opSuccess.Remaining, ctx);
                        if (rightResult is not SuccessResult<T, TToken> rightSuccess)
                            return rightResult;

                        left = opSuccess.Value(left, rightSuccess.Value);
                        current = rightSuccess.Remaining;
                        continue;
                    }
                }

                // 右結合演算子を試す
                if (rightOpParser != null)
                {
                    var opResult = rightOpParser.Parse(current, ctx);
                    if (opResult is SuccessResult<Func<T, T, T>, TToken> opSuccess)
                    {
                        // 右結合なので再帰的にパース
                        var selfParser = BuildInfixExpr(term, leftOps, rightOps, noneOps);
                        var rightResult = selfParser.Parse(opSuccess.Remaining, ctx);
                        if (rightResult is not SuccessResult<T, TToken> rightSuccess)
                            return rightResult;

                        return ParseResult<T, TToken>.Success(
                            opSuccess.Value(left, rightSuccess.Value),
                            rightSuccess.Remaining,
                            rightSuccess.Error);
                    }
                }

                // 結合なし演算子を試す
                if (noneOpParser != null)
                {
                    var opResult = noneOpParser.Parse(current, ctx);
                    if (opResult is SuccessResult<Func<T, T, T>, TToken> opSuccess)
                    {
                        var rightResult = term.Parse(opSuccess.Remaining, ctx);
                        if (rightResult is not SuccessResult<T, TToken> rightSuccess)
                            return rightResult;

                        // 結合なしなので、続けて同じレベルの演算子があればエラー
                        var checkOp = noneOpParser.Parse(rightSuccess.Remaining, ctx);
                        if (checkOp.IsSuccess)
                        {
                            return ParseResult<T, TToken>.Failure(
                                new ParseError(rightSuccess.Remaining.Position,
                                    ErrorMessage.Message("この演算子は連続して使用できません（結合規則なし）")),
                                rightSuccess.Remaining);
                        }

                        return ParseResult<T, TToken>.Success(
                            opSuccess.Value(left, rightSuccess.Value),
                            rightSuccess.Remaining,
                            rightSuccess.Error);
                    }
                }

                break;
            }

            return ParseResult<T, TToken>.Success(left, current, leftSuccess.Error);
        }, "InfixExpr");
    }
}
