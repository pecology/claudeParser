using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Tests;

/// <summary>
/// ExpressionParser（演算子優先順位）のテスト
/// </summary>
public class ExpressionParserTests
{
    private static Parser<int, char> Number =>
        CharParsers.UnsignedInteger.Select(n => (int)n).Lexeme();

    private static Parser<int, char> ParenExpr =>
        Parsers.Lazy(() => Expr).Named("ParenExpr");

    private static Parser<int, char> Term =>
        Number.Or(CharParsers.Parens(ParenExpr));

    private static OperatorTable<int, char> Table
    {
        get
        {
            var table = new OperatorTable<int, char>();
            
            // 優先度1（最低）：加算・減算
            table.AddLevel(
                OperatorTable<int, char>.Infix(Associativity.Left, CharParsers.Symbol("+").Ignore(), (a, b) => a + b),
                OperatorTable<int, char>.Infix(Associativity.Left, CharParsers.Symbol("-").Ignore(), (a, b) => a - b));
            
            // 優先度2：乗算・除算
            table.AddLevel(
                OperatorTable<int, char>.Infix(Associativity.Left, CharParsers.Symbol("*").Ignore(), (a, b) => a * b),
                OperatorTable<int, char>.Infix(Associativity.Left, CharParsers.Symbol("/").Ignore(), (a, b) => a / b));
            
            // 優先度3（最高）：単項マイナス
            table.AddLevel(
                OperatorTable<int, char>.Prefix(CharParsers.Symbol("-").Ignore(), x => -x));
            
            return table;
        }
    }

    private static Parser<int, char> Expr =>
        (from _ in CharParsers.Spaces
         from result in ExpressionParser.Build(Table, Term)
         select result).Named("Expr");

    private static int Evaluate(string input)
    {
        var stream = new StringInputStream(input);
        var result = Expr.Parse(stream);
        return ((SuccessResult<int, char>)result).Value;
    }

    [Fact]
    public void ExpressionParser_ShouldParseSimpleNumber()
    {
        Assert.Equal(42, Evaluate("42"));
    }

    [Fact]
    public void ExpressionParser_ShouldParseAddition()
    {
        Assert.Equal(3, Evaluate("1 + 2"));
    }

    [Fact]
    public void ExpressionParser_ShouldParseMultiplication()
    {
        Assert.Equal(6, Evaluate("2 * 3"));
    }

    [Fact]
    public void ExpressionParser_ShouldRespectPrecedence()
    {
        // 1 + 2 * 3 = 1 + (2 * 3) = 7
        Assert.Equal(7, Evaluate("1 + 2 * 3"));
    }

    [Fact]
    public void ExpressionParser_ShouldHandleParentheses()
    {
        // (1 + 2) * 3 = 9
        Assert.Equal(9, Evaluate("(1 + 2) * 3"));
    }

    [Fact]
    public void ExpressionParser_ShouldHandleLeftAssociativity()
    {
        // 10 - 5 - 2 = (10 - 5) - 2 = 3
        Assert.Equal(3, Evaluate("10 - 5 - 2"));
    }

    [Fact]
    public void ExpressionParser_ShouldHandleUnaryMinus()
    {
        Assert.Equal(-5, Evaluate("- 5"));
    }

    [Fact]
    public void ExpressionParser_ShouldHandleComplexExpression()
    {
        // 2 + 3 * 4 - 5 = 2 + 12 - 5 = 9
        Assert.Equal(9, Evaluate("2 + 3 * 4 - 5"));
    }

    [Fact]
    public void ExpressionParser_ShouldHandleNestedParentheses()
    {
        // ((1 + 2) * 3) = 9
        Assert.Equal(9, Evaluate("((1 + 2) * 3)"));
    }

    [Fact]
    public void ExpressionParser_WithRightAssociativeOperator()
    {
        // 累乗用のテーブルを作成
        var table = new OperatorTable<double, char>();
        table.AddLevel(
            OperatorTable<double, char>.Infix(Associativity.Right, 
                CharParsers.Symbol("^").Ignore(), 
                (a, b) => Math.Pow(a, b)));

        var number = CharParsers.UnsignedInteger.Select(n => (double)n).Lexeme();
        var expr = ExpressionParser.Build(table, number);

        var stream = new StringInputStream("2 ^ 3 ^ 2");
        var result = expr.Parse(stream);
        
        // 2^3^2 = 2^(3^2) = 2^9 = 512
        Assert.True(result.IsSuccess);
        Assert.Equal(512, ((SuccessResult<double, char>)result).Value);
    }

    [Fact]
    public void ExpressionParser_WithPostfixOperator()
    {
        var table = new OperatorTable<int, char>();
        table.AddLevel(
            OperatorTable<int, char>.Postfix(CharParsers.Symbol("!").Ignore(), x => Factorial(x)));

        var number = CharParsers.UnsignedInteger.Select(n => (int)n).Lexeme();
        var expr = ExpressionParser.Build(table, number);

        var stream = new StringInputStream("5!");
        var result = expr.Parse(stream);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(120, ((SuccessResult<int, char>)result).Value);
    }

    private static int Factorial(int n)
    {
        int result = 1;
        for (int i = 2; i <= n; i++)
            result *= i;
        return result;
    }
}
