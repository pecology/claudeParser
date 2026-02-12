using ClaudeParser.Core;
using ClaudeParser.Combinators;
using ClaudeParser.Examples;

namespace ClaudeParser.Tests;

/// <summary>
/// C2カバレッジ（条件網羅）テスト
/// 各条件分岐のtrue/false両方のパスをテスト
/// </summary>
public class ConditionCoverageTests
{
    #region Parser.cs Condition Coverage

    [Fact]
    public void Parser_Named_ShouldPreserveBehavior()
    {
        var original = CharParsers.Digit;
        var named = original.Named("MyDigit");
        
        Assert.Equal("MyDigit", named.Name);
        
        // 動作は同じ
        var result1 = original.Parse(new StringInputStream("5"));
        var result2 = named.Parse(new StringInputStream("5"));
        
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public void Parser_WithExpected_OnSuccess_ShouldNotModify()
    {
        var parser = CharParsers.Digit.WithExpected("数字です");
        var result = parser.Parse(new StringInputStream("5"));
        
        Assert.True(result.IsSuccess);
        Assert.Equal('5', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Parser_WithExpected_OnFailure_ShouldModifyMessage()
    {
        var parser = CharParsers.Digit.WithExpected("数字が必要です");
        var result = parser.Parse(new StringInputStream("abc"));
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<char, char>)result;
        Assert.Contains(failure.ErrorValue.Messages, m => m.Text == "数字が必要です");
    }

    [Fact]
    public void Parser_WithContext_OnSuccess_ShouldNotModify()
    {
        var parser = CharParsers.Digit.WithContext("数値リテラル");
        var result = parser.Parse(new StringInputStream("5"));
        
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parser_WithContext_OnFailure_ShouldAddContext()
    {
        var parser = CharParsers.Digit.WithContext("数値リテラル");
        var result = parser.Parse(new StringInputStream("abc"));
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<char, char>)result;
        Assert.Contains("数値リテラル", failure.ErrorValue.ContextStack);
    }

    [Fact]
    public void Parser_Select_OnFailure_ShouldPropagateFailure()
    {
        var parser = CharParsers.Digit.Select(c => c - '0');
        var result = parser.Parse(new StringInputStream("abc"));
        
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parser_SelectMany_FirstFails_ShouldPropagateFailure()
    {
        var parser = from a in CharParsers.Digit
                     from b in CharParsers.Letter
                     select (a, b);
        
        var result = parser.Parse(new StringInputStream("abc"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parser_SelectMany_SecondFails_ShouldPropagateFailure()
    {
        var parser = from a in CharParsers.Digit
                     from b in CharParsers.Digit
                     select (a, b);
        
        var result = parser.Parse(new StringInputStream("5a"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parser_Where_PredicateFalse_ShouldFail()
    {
        var parser = CharParsers.Digit
            .Select(c => c - '0')
            .Where(n => n > 5);
        
        var result = parser.Parse(new StringInputStream("3"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parser_Where_PredicateTrue_ShouldSucceed()
    {
        var parser = CharParsers.Digit
            .Select(c => c - '0')
            .Where(n => n > 5);
        
        var result = parser.Parse(new StringInputStream("7"));
        Assert.True(result.IsSuccess);
    }

    #endregion

    #region CharParsers Condition Coverage

    [Fact]
    public void Char_Match_ShouldSucceed()
    {
        var result = CharParsers.Char('x').Parse(new StringInputStream("xyz"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Char_NoMatch_ShouldFail()
    {
        var result = CharParsers.Char('x').Parse(new StringInputStream("abc"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void NoneOf_CharNotInSet_ShouldSucceed()
    {
        var result = CharParsers.NoneOf("xyz").Parse(new StringInputStream("abc"));
        Assert.True(result.IsSuccess);
        Assert.Equal('a', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void NoneOf_CharInSet_ShouldFail()
    {
        var result = CharParsers.NoneOf("xyz").Parse(new StringInputStream("xyz"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void OneOf_CharInSet_ShouldSucceed()
    {
        var result = CharParsers.OneOf("xyz").Parse(new StringInputStream("y"));
        Assert.True(result.IsSuccess);
        Assert.Equal('y', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void OneOf_CharNotInSet_ShouldFail()
    {
        var result = CharParsers.OneOf("xyz").Parse(new StringInputStream("a"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void String_EmptyString_ShouldSucceed()
    {
        var result = CharParsers.String("").Parse(new StringInputStream("abc"));
        Assert.True(result.IsSuccess);
        Assert.Equal("", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void String_PartialMatch_ShouldFail()
    {
        var result = CharParsers.String("hello").Parse(new StringInputStream("hel"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void StringIgnoreCase_DifferentCase_ShouldMatch()
    {
        var result = CharParsers.StringIgnoreCase("Hello").Parse(new StringInputStream("HELLO"));
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void StringIgnoreCase_NoMatch_ShouldFail()
    {
        var result = CharParsers.StringIgnoreCase("Hello").Parse(new StringInputStream("World"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void NewLine_LF_ShouldMatch()
    {
        var result = CharParsers.NewLine.Parse(new StringInputStream("\n"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NewLine_CR_ShouldMatchAsLF()
    {
        var result = CharParsers.NewLine.Parse(new StringInputStream("\r"));
        Assert.True(result.IsSuccess);
        Assert.Equal('\n', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void NewLine_CRLF_ShouldMatchAsLF()
    {
        var result = CharParsers.NewLine.Parse(new StringInputStream("\r\n"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EndOfLine_LF_ShouldSucceed()
    {
        var result = CharParsers.EndOfLine.Parse(new StringInputStream("\n"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EndOfLine_CRLF_ShouldSucceed()
    {
        var result = CharParsers.EndOfLine.Parse(new StringInputStream("\r\n"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TakeWhile_NoMatch_ShouldReturnEmptyString()
    {
        var result = CharParsers.TakeWhile(char.IsDigit).Parse(new StringInputStream("abc"));
        Assert.True(result.IsSuccess);
        Assert.Equal("", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void TakeWhile1_NoMatch_ShouldFail()
    {
        var result = CharParsers.TakeWhile1(char.IsDigit).Parse(new StringInputStream("abc"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TakeUntil_CharFound_ShouldSucceed()
    {
        var result = CharParsers.TakeUntil(':').Parse(new StringInputStream("hello:world"));
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void TakeUntil_CharNotFound_ShouldTakeAll()
    {
        var result = CharParsers.TakeUntil(':').Parse(new StringInputStream("hello"));
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void TakeUntilString_StringFound_ShouldSucceed()
    {
        var result = CharParsers.TakeUntilString("*/").Parse(new StringInputStream("comment*/end"));
        Assert.True(result.IsSuccess);
        Assert.Equal("comment", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void TakeUntilString_StringNotFound_ShouldTakeAll()
    {
        var result = CharParsers.TakeUntilString("*/").Parse(new StringInputStream("no end marker"));
        Assert.True(result.IsSuccess);
        Assert.Equal("no end marker", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void TakeUntilString_PartialMatch_ShouldContinue()
    {
        // "*" appears but not followed by "/"
        var result = CharParsers.TakeUntilString("*/").Parse(new StringInputStream("* not end */done"));
        Assert.True(result.IsSuccess);
        Assert.Equal("* not end ", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Integer_Negative_ShouldSucceed()
    {
        var result = CharParsers.Integer.Parse(new StringInputStream("-42"));
        Assert.True(result.IsSuccess);
        Assert.Equal(-42, ((SuccessResult<long, char>)result).Value);
    }

    [Fact]
    public void Integer_Positive_WithSign_ShouldSucceed()
    {
        var result = CharParsers.Integer.Parse(new StringInputStream("+42"));
        Assert.True(result.IsSuccess);
        Assert.Equal(42, ((SuccessResult<long, char>)result).Value);
    }

    [Fact]
    public void Double_Integer_ShouldSucceed()
    {
        var result = CharParsers.Double.Parse(new StringInputStream("42"));
        Assert.True(result.IsSuccess);
        Assert.Equal(42.0, ((SuccessResult<double, char>)result).Value);
    }

    [Fact]
    public void Double_WithDecimal_ShouldSucceed()
    {
        var result = CharParsers.Double.Parse(new StringInputStream("3.14"));
        Assert.True(result.IsSuccess);
        Assert.Equal(3.14, ((SuccessResult<double, char>)result).Value, 0.001);
    }

    [Fact]
    public void Double_WithExponent_ShouldSucceed()
    {
        var result = CharParsers.Double.Parse(new StringInputStream("1e10"));
        Assert.True(result.IsSuccess);
        Assert.Equal(1e10, ((SuccessResult<double, char>)result).Value);
    }

    [Fact]
    public void Double_WithNegativeExponent_ShouldSucceed()
    {
        var result = CharParsers.Double.Parse(new StringInputStream("1e-5"));
        Assert.True(result.IsSuccess);
        Assert.Equal(1e-5, ((SuccessResult<double, char>)result).Value, 0.0000001);
    }

    [Fact]
    public void Double_Full_ShouldSucceed()
    {
        var result = CharParsers.Double.Parse(new StringInputStream("-3.14e+2"));
        Assert.True(result.IsSuccess);
        Assert.Equal(-314.0, ((SuccessResult<double, char>)result).Value, 0.001);
    }

    [Fact]
    public void Double_NoDigits_ShouldFail()
    {
        var result = CharParsers.Double.Parse(new StringInputStream("abc"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Identifier_ValidStart_ShouldSucceed()
    {
        var result = CharParsers.Identifier.Parse(new StringInputStream("_test123"));
        Assert.True(result.IsSuccess);
        Assert.Equal("_test123", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Identifier_InvalidStart_ShouldFail()
    {
        var result = CharParsers.Identifier.Parse(new StringInputStream("123test"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void QuotedString_WithEscapes_ShouldHandleCorrectly()
    {
        var result = CharParsers.QuotedString().Parse(new StringInputStream("\"hello\\nworld\\t!\""));
        Assert.True(result.IsSuccess);
        Assert.Equal("hello\nworld\t!", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void QuotedString_WithEscapedQuote_ShouldHandleCorrectly()
    {
        var result = CharParsers.QuotedString().Parse(new StringInputStream("\"say \\\"hi\\\"\""));
        Assert.True(result.IsSuccess);
        Assert.Equal("say \"hi\"", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void QuotedString_EscapedBackslash_ShouldHandleCorrectly()
    {
        var result = CharParsers.QuotedString().Parse(new StringInputStream("\"C:\\\\path\""));
        Assert.True(result.IsSuccess);
        Assert.Equal("C:\\path", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void QuotedString_UnknownEscape_ShouldPassThrough()
    {
        var result = CharParsers.QuotedString().Parse(new StringInputStream("\"\\x\""));
        Assert.True(result.IsSuccess);
        Assert.Equal("x", ((SuccessResult<string, char>)result).Value);
    }

    #endregion

    #region Parsers Combinator Condition Coverage

    [Fact]
    public void Or_BothFail_ShouldMergeErrors()
    {
        var parser = CharParsers.String("cat").Or(CharParsers.String("dog"));
        var result = parser.Parse(new StringInputStream("xyz"));
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<string, char>)result;
        // 複数のエラーメッセージがマージされている
        Assert.True(failure.ErrorValue.Messages.Count >= 1);
    }

    [Fact]
    public void Or_FirstConsumesThenFails_ShouldNotTrySecond()
    {
        var parser = CharParsers.String("cat").Or(CharParsers.String("car"));
        var result = parser.Parse(new StringInputStream("car"));
        
        // "cat" は "ca" を消費してから失敗するため、"car" は試されない
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Many_InfiniteLoopPrevention_ShouldError()
    {
        var parser = Parsers.Return<int, char>(42).Many();
        var result = parser.Parse(new StringInputStream("test"));
        
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Many_PartialConsumptionThenFail_ShouldReturnPartial()
    {
        var parser = CharParsers.Digit.Many();
        var result = parser.Parse(new StringInputStream("123abc"));
        
        Assert.True(result.IsSuccess);
        var success = (SuccessResult<IReadOnlyList<char>, char>)result;
        Assert.Equal(3, success.Value.Count);
    }

    [Fact]
    public void Lazy_ShouldEnableRecursion()
    {
        // パーサーの自己参照（括弧で囲まれた数式）
        Parser<char, char>? expr = null;
        expr = CharParsers.Digit.Or(
            from _ in CharParsers.Char('(')
            from inner in Parsers.Lazy(() => expr!)
            from __ in CharParsers.Char(')')
            select inner);
        
        var result = expr.Parse(new StringInputStream("((5))"));
        Assert.True(result.IsSuccess);
        Assert.Equal('5', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Debug_ShouldCallCallbacks()
    {
        bool beforeCalled = false;
        bool afterCalled = false;
        
        var parser = CharParsers.Digit.Debug(
            input => beforeCalled = true,
            result => afterCalled = true);
        
        parser.Parse(new StringInputStream("5"));
        
        Assert.True(beforeCalled);
        Assert.True(afterCalled);
    }

    [Fact]
    public void Ignore_ShouldReturnUnit()
    {
        var parser = CharParsers.Digit.Ignore();
        var result = parser.Parse(new StringInputStream("5"));
        
        Assert.True(result.IsSuccess);
        Assert.Equal(Unit.Value, ((SuccessResult<Unit, char>)result).Value);
    }

    #endregion

    #region Tracing Condition Coverage

    [Fact]
    public void Tracing_Disabled_ShouldNotRecord()
    {
        var context = ParseContext.Default;
        var parser = CharParsers.Digit.Named("Digit");
        
        parser.Parse(new StringInputStream("5"), context);
        
        Assert.False(context.IsTraceEnabled);
        Assert.Null(context.Trace);
    }

    [Fact]
    public void Tracing_Enabled_Success_ShouldRecordEntry()
    {
        var context = ParseContext.WithTracing();
        var parser = CharParsers.Digit.Named("DigitParser");
        
        parser.Parse(new StringInputStream("5"), context);
        
        Assert.True(context.IsTraceEnabled);
        Assert.Contains(context.Trace!.Entries, e => e.ParserName == "DigitParser" && e.Success);
    }

    [Fact]
    public void Tracing_Enabled_Failure_ShouldRecordEntry()
    {
        var context = ParseContext.WithTracing();
        var parser = CharParsers.Digit.Named("DigitParser");
        
        parser.Parse(new StringInputStream("abc"), context);
        
        Assert.Contains(context.Trace!.Entries, e => e.ParserName == "DigitParser" && !e.Success);
    }

    #endregion

    #region ParseResult Condition Coverage

    [Fact]
    public void ParseResult_GetValueOrThrow_OnSuccess_ShouldReturnValue()
    {
        var result = CharParsers.Digit.Parse(new StringInputStream("5"));
        var value = result.GetValueOrThrow();
        Assert.Equal('5', value);
    }

    [Fact]
    public void ParseResult_GetValueOrThrow_OnFailure_ShouldThrow()
    {
        var result = CharParsers.Digit.Parse(new StringInputStream("abc"));
        Assert.Throws<ParseException>(() => result.GetValueOrThrow());
    }

    [Fact]
    public void ParseResult_GetValueOrThrow_WithSource_ShouldIncludeSource()
    {
        var source = "abc";
        var result = CharParsers.Digit.Parse(new StringInputStream(source));
        
        var ex = Assert.Throws<ParseException>(() => result.GetValueOrThrow(source));
        Assert.NotNull(ex.Error);
    }

    #endregion
}
