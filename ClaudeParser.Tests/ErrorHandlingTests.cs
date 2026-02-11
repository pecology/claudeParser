using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Tests;

/// <summary>
/// エラー処理とトレース機能のテスト
/// </summary>
public class ErrorHandlingTests
{
    [Fact]
    public void ParseError_ShouldContainPositionInfo()
    {
        var parser = CharParsers.String("hello");
        var input = new StringInputStream("help");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<string, char>)result;
        Assert.NotNull(failure.Error);
        Assert.Equal(1, failure.Error.Position.Line);
        // "help"に対して"hello"を試すと、'l'(4文字目)の位置で失敗する
        Assert.Equal(4, failure.Error.Position.Column);
    }

    [Fact]
    public void ParseError_ShouldMergeOnAlternation()
    {
        var parser = CharParsers.String("cat").Or(CharParsers.String("dog"));
        var input = new StringInputStream("xyz");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<string, char>)result;
        // 両方のエラーがマージされている
        Assert.True(failure.Error.Messages.Count >= 2);
    }

    [Fact]
    public void ParseError_WithExpected_ShouldCustomizeMessage()
    {
        var parser = CharParsers.Digit.WithExpected("整数の開始");
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<char, char>)result;
        Assert.Contains(failure.Error.Messages, m => m.Text == "整数の開始");
    }

    [Fact]
    public void ParseError_WithContext_ShouldAddContextStack()
    {
        var parser = CharParsers.Digit.WithContext("数値のパース");
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<char, char>)result;
        Assert.Contains("数値のパース", failure.Error.ContextStack);
    }

    [Fact]
    public void ParseError_ToString_ShouldBeHumanReadable()
    {
        var parser = CharParsers.Digit.WithExpected("数字");
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        var failure = (FailureResult<char, char>)result;
        var errorString = failure.Error.ToString();
        
        Assert.Contains("パースエラー", errorString);
        Assert.Contains("数字", errorString);
    }

    [Fact]
    public void ParseError_ToDetailedString_ShouldShowSourceLine()
    {
        var parser = CharParsers.Digit.WithExpected("数字");
        var input = new StringInputStream("hello\nworld");
        
        // 2行目に移動
        var advanced = input.Advance().Advance().Advance().Advance().Advance().Advance(); // "world"の先頭
        var result = parser.Parse(advanced);
        
        var failure = (FailureResult<char, char>)result;
        var detailed = failure.Error.ToDetailedString("hello\nworld");
        
        Assert.Contains("2", detailed); // 行番号
    }

    [Fact]
    public void ParseException_ShouldContainError()
    {
        var parser = CharParsers.Digit.Many1();
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        var ex = Assert.Throws<ParseException>(() => result.GetValueOrThrow());
        Assert.NotNull(ex.Error);
    }

    [Fact]
    public void ParseException_WithSource_ShouldShowDetailedError()
    {
        var source = "abc";
        var parser = CharParsers.Digit.Many1();
        var input = new StringInputStream(source);
        var result = parser.Parse(input);
        
        var ex = Assert.Throws<ParseException>(() => result.GetValueOrThrow(source));
        Assert.Contains("abc", ex.Message);
    }

    [Fact]
    public void Named_ShouldAppearInTrace()
    {
        var context = ParseContext.WithTracing();
        var parser = CharParsers.String("hello").Named("HelloParser");
        
        var input = new StringInputStream("hello");
        parser.Parse(input, context);
        
        var trace = context.Trace!.ToReport();
        Assert.Contains("HelloParser", trace);
    }

    [Fact]
    public void Trace_ShouldRecordSuccessAndFailure()
    {
        var context = ParseContext.WithTracing();
        // Try()を使ってバックトラックを有効にし、成功と失敗の両方を記録
        var parser = CharParsers.String("abc").Try().Or(CharParsers.String("abd")).Named("ABParser");
        
        var input = new StringInputStream("abd");
        parser.Parse(input, context);
        
        // 成功と失敗の両方が記録される
        var entries = context.Trace!.Entries;
        Assert.Contains(entries, e => e.Success);
        Assert.Contains(entries, e => !e.Success);
    }

    [Fact]
    public void Trace_CanBeDisabled()
    {
        var context = new ParseContext { Trace = new TraceCollector { IsEnabled = false } };
        var parser = CharParsers.String("hello").Named("HelloParser");
        
        var input = new StringInputStream("hello");
        parser.Parse(input, context);
        
        Assert.Empty(context.Trace!.Entries);
    }

    [Fact]
    public void Trace_ShouldHaveDepthInfo()
    {
        var context = ParseContext.WithTracing();
        var parser = from a in CharParsers.Char('a').Named("A")
                     from b in CharParsers.Char('b').Named("B")
                     select (a, b);
        
        var input = new StringInputStream("ab");
        parser.Named("AB").Parse(input, context);
        
        var entries = context.Trace!.Entries;
        Assert.Contains(entries, e => e.Depth > 0);
    }

    [Fact]
    public void Many_ShouldDetectInfiniteLoop()
    {
        // 空文字列を常に成功するパーサー（入力を消費しない）
        var parser = Parsers.Return<string, char>("").Many();
        var input = new StringInputStream("abc");
        var result = parser.Parse(input);
        
        // 無限ループを検出してエラーを返すべき
        Assert.False(result.IsSuccess);
        var failure = (FailureResult<IReadOnlyList<string>, char>)result;
        Assert.Contains(failure.Error.Messages, m => m.Text.Contains("入力を消費せず"));
    }
}
