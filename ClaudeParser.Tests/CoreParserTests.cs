using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Tests;

/// <summary>
/// コアパーサー機能のテスト
/// </summary>
public class CoreParserTests
{
    #region Position Tests

    [Fact]
    public void Position_Initial_ShouldBeAtLineOneColumnOne()
    {
        var pos = Position.Initial("test");
        Assert.Equal(1, pos.Line);
        Assert.Equal(1, pos.Column);
        Assert.Equal(0, pos.Offset);
        Assert.Equal("test", pos.SourceName);
    }

    [Fact]
    public void Position_Advance_ShouldIncrementColumn()
    {
        var pos = Position.Initial().Advance('a');
        Assert.Equal(1, pos.Line);
        Assert.Equal(2, pos.Column);
        Assert.Equal(1, pos.Offset);
    }

    [Fact]
    public void Position_Advance_Newline_ShouldIncrementLine()
    {
        var pos = Position.Initial().Advance('\n');
        Assert.Equal(2, pos.Line);
        Assert.Equal(1, pos.Column);
    }

    #endregion

    #region Input Stream Tests

    [Fact]
    public void StringInputStream_ShouldReadCharacters()
    {
        var input = new StringInputStream("hello");
        Assert.Equal('h', input.Current);
        Assert.False(input.IsAtEnd);
    }

    [Fact]
    public void StringInputStream_Advance_ShouldMoveToNextCharacter()
    {
        var input = new StringInputStream("hello");
        var next = input.Advance();
        Assert.Equal('e', next.Current);
    }

    [Fact]
    public void StringInputStream_ShouldDetectEndOfInput()
    {
        var input = new StringInputStream("a");
        Assert.False(input.IsAtEnd);
        var next = input.Advance();
        Assert.True(next.IsAtEnd);
    }

    [Fact]
    public void ByteInputStream_ShouldReadBytes()
    {
        var input = new ByteInputStream(new byte[] { 0x01, 0x02, 0x03 });
        Assert.Equal((byte)0x01, input.Current);
        var next = input.Advance();
        Assert.Equal((byte)0x02, next.Current);
    }

    #endregion

    #region Basic Parser Tests

    [Fact]
    public void Return_ShouldSucceedWithValue()
    {
        var parser = Parsers.Return<int, char>(42);
        var input = new StringInputStream("test");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(42, ((SuccessResult<int, char>)result).Value);
    }

    [Fact]
    public void Fail_ShouldAlwaysFail()
    {
        var parser = Parsers.Fail<int, char>("error");
        var input = new StringInputStream("test");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Satisfy_ShouldSucceedWhenPredicateMatches()
    {
        var parser = Parsers.Satisfy<char>(char.IsDigit, "digit");
        var input = new StringInputStream("123");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal('1', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Satisfy_ShouldFailWhenPredicateDoesNotMatch()
    {
        var parser = Parsers.Satisfy<char>(char.IsDigit, "digit");
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Eof_ShouldSucceedAtEnd()
    {
        var parser = Parsers.Eof<char>();
        var input = new StringInputStream("");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Eof_ShouldFailWhenNotAtEnd()
    {
        var parser = Parsers.Eof<char>();
        var input = new StringInputStream("a");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Combinator Tests

    [Fact]
    public void Or_ShouldTrySecondParserOnFirstFailure()
    {
        var parser = CharParsers.Char('a').Or(CharParsers.Char('b'));
        var input = new StringInputStream("b");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal('b', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Or_ShouldNotBacktrackIfFirstConsumed()
    {
        var parser = CharParsers.String("abc").Or(CharParsers.String("abd"));
        var input = new StringInputStream("abd");
        var result = parser.Parse(input);
        
        // "abc"は最初の2文字を消費してから失敗するため、バックトラックしない
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Try_ShouldEnableBacktracking()
    {
        var parser = CharParsers.String("abc").Try().Or(CharParsers.String("abd"));
        var input = new StringInputStream("abd");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal("abd", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Many_ShouldParseZeroOrMore()
    {
        var parser = CharParsers.Digit.Many();
        var input = new StringInputStream("123abc");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Equal(3, success.Value.Count);
        Assert.Equal(new[] { '1', '2', '3' }, success.Value);
    }

    [Fact]
    public void Many_ShouldSucceedWithEmptyOnNoMatch()
    {
        var parser = CharParsers.Digit.Many();
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Empty(((SuccessResult<IReadOnlyList<char>, char>)result).Value);
    }

    [Fact]
    public void Many1_ShouldRequireAtLeastOne()
    {
        var parser = CharParsers.Digit.Many1();
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SepBy_ShouldParseCommaSeparatedValues()
    {
        var parser = CharParsers.Digit.Select(c => c - '0').SepBy(CharParsers.Char(','));
        var input = new StringInputStream("1,2,3");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<int>, char>)result;
        Assert.Equal(new[] { 1, 2, 3 }, success.Value);
    }

    [Fact]
    public void Between_ShouldParseBetweenDelimiters()
    {
        var parser = Parsers.Between(CharParsers.Char('('), CharParsers.Char(')'), CharParsers.Digit);
        var input = new StringInputStream("(5)");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal('5', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void ChainLeft_ShouldParseLeftAssociative()
    {
        var number = CharParsers.Digit.Select(c => (double)(c - '0'));
        var sub = CharParsers.Char('-').Select(_ => (Func<double, double, double>)((a, b) => a - b));
        var parser = number.ChainLeft(sub);
        
        var input = new StringInputStream("9-5-2");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        // 9-5-2 = (9-5)-2 = 2
        Assert.Equal(2, ((SuccessResult<double, char>)result).Value);
    }

    [Fact]
    public void ChainRight_ShouldParseRightAssociative()
    {
        var number = CharParsers.Digit.Select(c => (double)(c - '0'));
        var pow = CharParsers.Char('^').Select(_ => (Func<double, double, double>)((a, b) => Math.Pow(a, b)));
        var parser = number.ChainRight(pow);
        
        var input = new StringInputStream("2^3^2");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        // 2^3^2 = 2^(3^2) = 2^9 = 512
        Assert.Equal(512, ((SuccessResult<double, char>)result).Value);
    }

    #endregion

    #region LINQ Query Syntax Tests

    [Fact]
    public void QuerySyntax_ShouldSupportSelectMany()
    {
        var parser = from a in CharParsers.Char('a')
                     from b in CharParsers.Char('b')
                     select (a, b);

        var input = new StringInputStream("ab");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(('a', 'b'), ((SuccessResult<(char, char), char>)result).Value);
    }

    [Fact]
    public void QuerySyntax_ShouldSupportSelect()
    {
        var parser = from d in CharParsers.Digit
                     select d - '0';

        var input = new StringInputStream("5");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(5, ((SuccessResult<int, char>)result).Value);
    }

    [Fact]
    public void QuerySyntax_ShouldSupportWhere()
    {
        var parser = from d in CharParsers.Digit
                     let n = d - '0'
                     where n > 5
                     select n;

        var input = new StringInputStream("7");
        var result = parser.Parse(input);
        Assert.True(result.IsSuccess);

        input = new StringInputStream("3");
        result = parser.Parse(input);
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Tracing Tests

    [Fact]
    public void Tracing_ShouldRecordParserExecution()
    {
        var context = ParseContext.WithTracing();
        var parser = CharParsers.String("hello").Named("HelloParser");
        
        var input = new StringInputStream("hello");
        parser.Parse(input, context);
        
        Assert.NotEmpty(context.Trace!.Entries);
        Assert.Contains(context.Trace.Entries, e => e.ParserName == "HelloParser");
    }

    #endregion
}
