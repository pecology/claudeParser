using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Examples;

/// <summary>
/// 四則演算の計算式パーサー
/// 演算子の優先順位と結合規則を正しく処理します。
/// 
/// 対応する演算子:
/// - 加算 (+), 減算 (-) : 優先度低、左結合
/// - 乗算 (*), 除算 (/), 剰余 (%) : 優先度中、左結合
/// - 累乗 (^) : 優先度高、右結合
/// - 単項マイナス (-), 単項プラス (+) : 前置演算子
/// </summary>
public static class CalculatorParser
{
    /// <summary>
    /// 数値リテラル（整数または浮動小数点数）
    /// </summary>
    private static Parser<double, char> Number =>
        CharParsers.Double.Lexeme().Named("数値").WithExpected("数値");

    /// <summary>
    /// 括弧で囲まれた式
    /// </summary>
    private static Parser<double, char> ParenExpr =>
        Parsers.Lazy(() => CharParsers.Parens(Expression))
               .Named("括弧式")
               .WithExpected("'('");

    /// <summary>
    /// 項（原子式）：数値または括弧式
    /// </summary>
    private static Parser<double, char> Term =>
        Number.Or(ParenExpr).Named("項");

    /// <summary>
    /// 演算子テーブル（優先度の低い順）
    /// </summary>
    private static OperatorTable<double, char> OperatorTable
    {
        get
        {
            var table = new OperatorTable<double, char>();

            // 優先度1（最低）：加算と減算
            table.AddLevel(
                OperatorTable<double, char>.Infix(
                    Associativity.Left,
                    CharParsers.Symbol("+").Ignore(),
                    (a, b) => a + b),
                OperatorTable<double, char>.Infix(
                    Associativity.Left,
                    CharParsers.Symbol("-").Ignore(),
                    (a, b) => a - b));

            // 優先度2：乗算、除算、剰余
            table.AddLevel(
                OperatorTable<double, char>.Infix(
                    Associativity.Left,
                    CharParsers.Symbol("*").Ignore(),
                    (a, b) => a * b),
                OperatorTable<double, char>.Infix(
                    Associativity.Left,
                    CharParsers.Symbol("/").Ignore(),
                    (a, b) => a / b),
                OperatorTable<double, char>.Infix(
                    Associativity.Left,
                    CharParsers.Symbol("%").Ignore(),
                    (a, b) => a % b));

            // 優先度3（最高）：累乗（右結合）
            table.AddLevel(
                OperatorTable<double, char>.Infix(
                    Associativity.Right,
                    CharParsers.Symbol("^").Ignore(),
                    (a, b) => Math.Pow(a, b)));

            // 前置演算子：単項マイナスとプラス
            // （各レベルに追加可能だが、最高優先度のレベルに追加するのが一般的）
            table.AddLevel(
                OperatorTable<double, char>.Prefix(
                    CharParsers.Symbol("-").Ignore(),
                    x => -x),
                OperatorTable<double, char>.Prefix(
                    CharParsers.Symbol("+").Ignore(),
                    x => x));

            return table;
        }
    }

    /// <summary>
    /// 式全体のパーサー
    /// </summary>
    public static Parser<double, char> Expression =>
        (from _ in CharParsers.Spaces
         from result in ExpressionParser.Build(OperatorTable, Term)
         select result).Named("式");

    /// <summary>
    /// 式をパースして計算結果を返します。
    /// </summary>
    public static double Evaluate(string expression)
    {
        var input = new StringInputStream(expression);
        var parser = from expr in Expression
                     from _ in CharParsers.Spaces
                     from __ in Parsers.Eof<char>()
                     select expr;
        
        var result = parser.Parse(input);
        return result.GetValueOrThrow(expression);
    }

    /// <summary>
    /// 式をパースして結果を返します（エラーの場合はnull）
    /// </summary>
    public static double? TryEvaluate(string expression)
    {
        try
        {
            return Evaluate(expression);
        }
        catch (ParseException)
        {
            return null;
        }
    }
}

/// <summary>
/// AST（抽象構文木）ベースの式パーサー
/// 計算だけでなく、式の構造を保持したい場合に使用します。
/// </summary>
public static class ExpressionAstParser
{
    /// <summary>
    /// 式のAST
    /// </summary>
    public abstract record Expr
    {
        public abstract double Evaluate();
    }

    /// <summary>
    /// 数値リテラル
    /// </summary>
    public record NumberExpr(double Value) : Expr
    {
        public override double Evaluate() => Value;
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// 二項演算
    /// </summary>
    public record BinaryExpr(string Operator, Expr Left, Expr Right) : Expr
    {
        public override double Evaluate() => Operator switch
        {
            "+" => Left.Evaluate() + Right.Evaluate(),
            "-" => Left.Evaluate() - Right.Evaluate(),
            "*" => Left.Evaluate() * Right.Evaluate(),
            "/" => Left.Evaluate() / Right.Evaluate(),
            "%" => Left.Evaluate() % Right.Evaluate(),
            "^" => Math.Pow(Left.Evaluate(), Right.Evaluate()),
            _ => throw new InvalidOperationException($"不明な演算子: {Operator}")
        };
        public override string ToString() => $"({Left} {Operator} {Right})";
    }

    /// <summary>
    /// 単項演算
    /// </summary>
    public record UnaryExpr(string Operator, Expr Operand) : Expr
    {
        public override double Evaluate() => Operator switch
        {
            "-" => -Operand.Evaluate(),
            "+" => Operand.Evaluate(),
            _ => throw new InvalidOperationException($"不明な演算子: {Operator}")
        };
        public override string ToString() => $"({Operator}{Operand})";
    }

    /// <summary>
    /// 数値リテラル
    /// </summary>
    private static Parser<Expr, char> Number =>
        CharParsers.Double.Lexeme()
                   .Select(n => (Expr)new NumberExpr(n))
                   .Named("数値");

    /// <summary>
    /// 括弧式
    /// </summary>
    private static Parser<Expr, char> ParenExpr =>
        Parsers.Lazy(() => CharParsers.Parens(Expression)).Named("括弧式");

    /// <summary>
    /// 項
    /// </summary>
    private static Parser<Expr, char> Term => Number.Or(ParenExpr);

    /// <summary>
    /// 演算子テーブル
    /// </summary>
    private static OperatorTable<Expr, char> OperatorTable
    {
        get
        {
            var table = new OperatorTable<Expr, char>();

            table.AddLevel(
                OperatorTable<Expr, char>.Infix(Associativity.Left, CharParsers.Symbol("+").Ignore(),
                    (a, b) => new BinaryExpr("+", a, b)),
                OperatorTable<Expr, char>.Infix(Associativity.Left, CharParsers.Symbol("-").Ignore(),
                    (a, b) => new BinaryExpr("-", a, b)));

            table.AddLevel(
                OperatorTable<Expr, char>.Infix(Associativity.Left, CharParsers.Symbol("*").Ignore(),
                    (a, b) => new BinaryExpr("*", a, b)),
                OperatorTable<Expr, char>.Infix(Associativity.Left, CharParsers.Symbol("/").Ignore(),
                    (a, b) => new BinaryExpr("/", a, b)),
                OperatorTable<Expr, char>.Infix(Associativity.Left, CharParsers.Symbol("%").Ignore(),
                    (a, b) => new BinaryExpr("%", a, b)));

            table.AddLevel(
                OperatorTable<Expr, char>.Infix(Associativity.Right, CharParsers.Symbol("^").Ignore(),
                    (a, b) => new BinaryExpr("^", a, b)));

            table.AddLevel(
                OperatorTable<Expr, char>.Prefix(CharParsers.Symbol("-").Ignore(),
                    x => new UnaryExpr("-", x)),
                OperatorTable<Expr, char>.Prefix(CharParsers.Symbol("+").Ignore(),
                    x => new UnaryExpr("+", x)));

            return table;
        }
    }

    /// <summary>
    /// 式パーサー
    /// </summary>
    public static Parser<Expr, char> Expression =>
        (from _ in CharParsers.Spaces
         from result in Combinators.ExpressionParser.Build(OperatorTable, Term)
         select result).Named("式");

    /// <summary>
    /// 式をパースしてASTを返します。
    /// </summary>
    public static Expr Parse(string expression)
    {
        var input = new StringInputStream(expression);
        var parser = from expr in Expression
                     from _ in CharParsers.Spaces
                     from __ in Parsers.Eof<char>()
                     select expr;
        
        var result = parser.Parse(input);
        return result.GetValueOrThrow(expression);
    }
}
