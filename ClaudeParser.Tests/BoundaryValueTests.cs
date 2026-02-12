using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Tests;

/// <summary>
/// 境界値テストとC2カバレッジテスト
/// </summary>
public class BoundaryValueTests
{
    #region Position Boundary Tests

    [Fact]
    public void Position_MultipleNewlines_ShouldTrackCorrectly()
    {
        var pos = Position.Initial();
        pos = pos.Advance('\n').Advance('\n').Advance('\n');
        Assert.Equal(4, pos.Line);
        Assert.Equal(1, pos.Column);
        Assert.Equal(3, pos.Offset);
    }

    [Theory]
    [InlineData('\r', 1, 2)] // CR alone should just advance column
    [InlineData('\t', 1, 2)] // Tab should just advance column
    public void Position_SpecialChars_ShouldAdvanceCorrectly(char c, int expectedLine, int expectedColumn)
    {
        var pos = Position.Initial().Advance(c);
        Assert.Equal(expectedLine, pos.Line);
        Assert.Equal(expectedColumn, pos.Column);
    }

    [Fact]
    public void Position_LongLineOffset_ShouldTrackCorrectly()
    {
        var pos = Position.Initial();
        for (int i = 0; i < 1000; i++)
        {
            pos = pos.Advance('x');
        }
        Assert.Equal(1, pos.Line);
        Assert.Equal(1001, pos.Column);
        Assert.Equal(1000, pos.Offset);
    }

    #endregion

    #region InputStream Boundary Tests

    [Fact]
    public void StringInputStream_EmptyString_ShouldBeAtEnd()
    {
        var input = new StringInputStream("");
        Assert.True(input.IsAtEnd);
        Assert.Equal('\0', input.Current);
    }

    [Fact]
    public void StringInputStream_SingleChar_ShouldWorkCorrectly()
    {
        var input = new StringInputStream("x");
        Assert.False(input.IsAtEnd);
        Assert.Equal('x', input.Current);
        
        var next = input.Advance();
        Assert.True(next.IsAtEnd);
    }

    [Fact]
    public void StringInputStream_AdvancePastEnd_ShouldReturnSelf()
    {
        var input = new StringInputStream("");
        var advanced = input.Advance();
        Assert.True(ReferenceEquals(input, advanced) || input.Equals(advanced));
    }

    [Fact]
    public void StringInputStream_GetContext_AtEnd_ShouldReturnEof()
    {
        var input = new StringInputStream("");
        Assert.Equal("<EOF>", input.GetContext());
    }

    [Fact]
    public void StringInputStream_GetContext_LongInput_ShouldTruncate()
    {
        var input = new StringInputStream(new string('x', 100));
        var context = input.GetContext(10);
        Assert.Contains("...", context);
    }

    [Fact]
    public void StringInputStream_GetContext_WithSpecialChars_ShouldEscape()
    {
        var input = new StringInputStream("a\nb\rc\td");
        var context = input.GetContext();
        Assert.Contains("\\n", context);
        Assert.Contains("\\r", context);
        Assert.Contains("\\t", context);
    }

    [Fact]
    public void ByteInputStream_EmptyArray_ShouldBeAtEnd()
    {
        var input = new ByteInputStream(Array.Empty<byte>());
        Assert.True(input.IsAtEnd);
    }

    [Fact]
    public void ByteInputStream_SingleByte_ShouldWorkCorrectly()
    {
        var input = new ByteInputStream(new byte[] { 0xFF });
        Assert.False(input.IsAtEnd);
        Assert.Equal((byte)0xFF, input.Current);
        
        var next = input.Advance();
        Assert.True(next.IsAtEnd);
    }

    [Fact]
    public void ByteInputStream_AdvancePastEnd_ShouldReturnSelf()
    {
        var input = new ByteInputStream(Array.Empty<byte>());
        var advanced = input.Advance();
        Assert.True(input.Equals(advanced));
    }

    #endregion

    #region Basic Parser Boundary Tests

    [Fact]
    public void Satisfy_EmptyInput_ShouldFail()
    {
        var parser = Parsers.Satisfy<char>(c => true, "any");
        var result = parser.Parse(new StringInputStream(""));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void AnyToken_EmptyInput_ShouldFail()
    {
        var parser = Parsers.AnyToken<char>();
        var result = parser.Parse(new StringInputStream(""));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void GetPosition_ShouldNotConsumeInput()
    {
        var parser = Parsers.GetPosition<char>();
        var input = new StringInputStream("test");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<Position, char>)result;
        Assert.Equal(1, success.Value.Column);
        Assert.Equal('t', success.Remaining.Current);
    }

    #endregion

    #region Combinator Boundary Tests

    [Fact]
    public void Many_EmptyInput_ShouldReturnEmptyList()
    {
        var parser = CharParsers.Digit.Many();
        var result = parser.Parse(new StringInputStream(""));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Empty(success.Value);
    }

    [Fact]
    public void Many1_SingleElement_ShouldSucceed()
    {
        var parser = CharParsers.Digit.Many1();
        var result = parser.Parse(new StringInputStream("5"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Single(success.Value);
    }

    [Fact]
    public void Many1_EmptyInput_ShouldFail()
    {
        var parser = CharParsers.Digit.Many1();
        var result = parser.Parse(new StringInputStream(""));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Count_ZeroCount_ShouldReturnEmptyList()
    {
        var parser = CharParsers.Digit.Count(0);
        var result = parser.Parse(new StringInputStream("123"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Empty(success.Value);
    }

    [Fact]
    public void Count_ExactMatch_ShouldSucceed()
    {
        var parser = CharParsers.Digit.Count(3);
        var result = parser.Parse(new StringInputStream("123"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Equal(3, success.Value.Count);
    }

    [Fact]
    public void Count_NotEnoughInput_ShouldFail()
    {
        var parser = CharParsers.Digit.Count(5);
        var result = parser.Parse(new StringInputStream("12"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SepBy_EmptyInput_ShouldReturnEmptyList()
    {
        var parser = CharParsers.Digit.Select(c => c - '0').SepBy(CharParsers.Char(','));
        var result = parser.Parse(new StringInputStream(""));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<int>, char>)result;
        Assert.Empty(success.Value);
    }

    [Fact]
    public void SepBy_SingleElement_ShouldSucceed()
    {
        var parser = CharParsers.Digit.Select(c => c - '0').SepBy(CharParsers.Char(','));
        var result = parser.Parse(new StringInputStream("5"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<int>, char>)result;
        Assert.Single(success.Value);
        Assert.Equal(5, success.Value[0]);
    }

    [Fact]
    public void SepBy1_EmptyInput_ShouldFail()
    {
        var parser = CharParsers.Digit.SepBy1(CharParsers.Char(','));
        var result = parser.Parse(new StringInputStream(""));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SepEndBy_NoTrailingSeparator_ShouldSucceed()
    {
        var parser = CharParsers.Digit.SepEndBy(CharParsers.Char(','));
        var result = parser.Parse(new StringInputStream("1,2,3"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Equal(3, success.Value.Count);
    }

    [Fact]
    public void SepEndBy_EmptyInput_ShouldReturnEmptyList()
    {
        var parser = CharParsers.Digit.SepEndBy(CharParsers.Char(','));
        var result = parser.Parse(new StringInputStream(""));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Empty(success.Value);
    }

    [Fact]
    public void EndBy_EmptyInput_ShouldSucceed()
    {
        var parser = CharParsers.Digit.EndBy(CharParsers.Char(';'));
        var result = parser.Parse(new StringInputStream(""));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Empty(success.Value);
    }

    [Fact]
    public void EndBy1_SingleElement_ShouldSucceed()
    {
        var parser = CharParsers.Digit.EndBy1(CharParsers.Char(';'));
        var result = parser.Parse(new StringInputStream("5;"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Single(success.Value);
    }

    [Fact]
    public void EndBy1_MissingTerminator_ShouldFail()
    {
        var parser = CharParsers.Digit.EndBy1(CharParsers.Char(';'));
        var result = parser.Parse(new StringInputStream("5"));
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Optional Boundary Tests

    [Fact]
    public void Optional_EmptyInput_ShouldReturnNull()
    {
        var parser = CharParsers.String("test").Optional();
        var result = parser.Parse(new StringInputStream(""));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<string?, char>)result;
        Assert.Null(success.Value);
    }

    [Fact]
    public void OptionalValue_EmptyInput_ShouldReturnNull()
    {
        var parser = CharParsers.Digit.Select(c => (int)(c - '0')).OptionalValue();
        var result = parser.Parse(new StringInputStream(""));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<int?, char>)result;
        Assert.Null(success.Value);
    }

    [Fact]
    public void OptionalOr_NoMatch_ShouldReturnDefault()
    {
        var parser = CharParsers.Digit.OptionalOr('0');
        var result = parser.Parse(new StringInputStream("abc"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<char, char>)result;
        Assert.Equal('0', success.Value);
    }

    #endregion

    #region LookAhead and NotFollowedBy Boundary Tests

    [Fact]
    public void LookAhead_EmptyInput_ShouldFail()
    {
        var parser = CharParsers.Digit.LookAhead();
        var result = parser.Parse(new StringInputStream(""));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void LookAhead_Match_ShouldNotConsumeInput()
    {
        var parser = CharParsers.Digit.LookAhead();
        var input = new StringInputStream("123");
        var result = parser.Parse(input);
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<char, char>)result;
        Assert.Equal('1', success.Value);
        Assert.Equal('1', success.Remaining.Current); // 入力は消費されていない
    }

    [Fact]
    public void NotFollowedBy_EmptyInput_ShouldSucceed()
    {
        var parser = CharParsers.Digit.NotFollowedBy();
        var result = parser.Parse(new StringInputStream(""));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NotFollowedBy_Match_ShouldFail()
    {
        var parser = CharParsers.Digit.NotFollowedBy();
        var result = parser.Parse(new StringInputStream("5"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void NotFollowedBy_NoMatch_ShouldSucceed()
    {
        var parser = CharParsers.Digit.NotFollowedBy();
        var result = parser.Parse(new StringInputStream("abc"));
        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Try and Or Boundary Tests

    [Fact]
    public void Or_FirstSucceeds_ShouldNotTrySecond()
    {
        bool secondCalled = false;
        var second = new Parser<char, char>((input, ctx) =>
        {
            secondCalled = true;
            return CharParsers.Char('b').Parse(input, ctx);
        }, "second");
        
        var parser = CharParsers.Char('a').Or(second);
        var result = parser.Parse(new StringInputStream("a"));
        
        Assert.True(result.IsSuccess);
        Assert.False(secondCalled);
    }

    [Fact]
    public void Try_FailureAfterConsumption_ShouldBacktrack()
    {
        var parser = CharParsers.String("abc").Try().Or(CharParsers.String("ab"));
        var result = parser.Parse(new StringInputStream("ab"));
        
        Assert.True(result.IsSuccess);
        Assert.Equal("ab", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Choice_EmptyArray_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var parsers = Array.Empty<Parser<char, char>>();
            Parsers.Choice(parsers);
        });
    }

    [Fact]
    public void Choice_SingleParser_ShouldBehaveIdentically()
    {
        var parser = Parsers.Choice(CharParsers.Char('a'));
        var result = parser.Parse(new StringInputStream("a"));
        
        Assert.True(result.IsSuccess);
        Assert.Equal('a', ((SuccessResult<char, char>)result).Value);
    }

    #endregion

    #region ChainLeft and ChainRight Boundary Tests

    [Fact]
    public void ChainLeft_SingleOperand_ShouldSucceed()
    {
        var number = CharParsers.Digit.Select(c => c - '0');
        var add = CharParsers.Char('+').Select(_ => (Func<int, int, int>)((a, b) => a + b));
        var parser = number.ChainLeft(add);
        
        var result = parser.Parse(new StringInputStream("5"));
        Assert.True(result.IsSuccess);
        Assert.Equal(5, ((SuccessResult<int, char>)result).Value);
    }

    [Fact]
    public void ChainRight_SingleOperand_ShouldSucceed()
    {
        var number = CharParsers.Digit.Select(c => (double)(c - '0'));
        var pow = CharParsers.Char('^').Select(_ => (Func<double, double, double>)Math.Pow);
        var parser = number.ChainRight(pow);
        
        var result = parser.Parse(new StringInputStream("2"));
        Assert.True(result.IsSuccess);
        Assert.Equal(2.0, ((SuccessResult<double, char>)result).Value);
    }

    [Fact]
    public void ChainLeft_MultipleOperators_ShouldBeLeftAssociative()
    {
        var number = CharParsers.Digit.Select(c => c - '0');
        var sub = CharParsers.Char('-').Select(_ => (Func<int, int, int>)((a, b) => a - b));
        var parser = number.ChainLeft(sub);
        
        // 9-5-2 = (9-5)-2 = 2
        var result = parser.Parse(new StringInputStream("9-5-2"));
        Assert.True(result.IsSuccess);
        Assert.Equal(2, ((SuccessResult<int, char>)result).Value);
    }

    [Fact]
    public void ChainRight_MultipleOperators_ShouldBeRightAssociative()
    {
        var number = CharParsers.Digit.Select(c => (double)(c - '0'));
        var pow = CharParsers.Char('^').Select(_ => (Func<double, double, double>)Math.Pow);
        var parser = number.ChainRight(pow);
        
        // 2^3^2 = 2^(3^2) = 2^9 = 512
        var result = parser.Parse(new StringInputStream("2^3^2"));
        Assert.True(result.IsSuccess);
        Assert.Equal(512.0, ((SuccessResult<double, char>)result).Value);
    }

    #endregion
}
