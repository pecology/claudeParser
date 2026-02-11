using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Tests;

/// <summary>
/// 文字パーサーのテスト
/// </summary>
public class CharParsersTests
{
    [Fact]
    public void Char_ShouldMatchSpecificCharacter()
    {
        var result = CharParsers.Char('a').Parse(new StringInputStream("abc"));
        Assert.True(result.IsSuccess);
        Assert.Equal('a', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Char_ShouldFailOnMismatch()
    {
        var result = CharParsers.Char('a').Parse(new StringInputStream("xyz"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void String_ShouldMatchExactString()
    {
        var result = CharParsers.String("hello").Parse(new StringInputStream("hello world"));
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void String_ShouldFailOnPartialMatch()
    {
        var result = CharParsers.String("hello").Parse(new StringInputStream("help"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Digit_ShouldMatchDigits()
    {
        var result = CharParsers.Digit.Parse(new StringInputStream("5"));
        Assert.True(result.IsSuccess);
        Assert.Equal('5', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Digit_ShouldNotMatchLetters()
    {
        var result = CharParsers.Digit.Parse(new StringInputStream("a"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Letter_ShouldMatchLetters()
    {
        var result = CharParsers.Letter.Parse(new StringInputStream("x"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Letter_ShouldMatchUnicode()
    {
        var result = CharParsers.Letter.Parse(new StringInputStream("あ"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void HexDigit_ShouldMatchHexCharacters()
    {
        foreach (var c in "0123456789abcdefABCDEF")
        {
            var result = CharParsers.HexDigit.Parse(new StringInputStream(c.ToString()));
            Assert.True(result.IsSuccess, $"Expected '{c}' to match hex digit");
        }
    }

    [Fact]
    public void Space_ShouldMatchWhitespace()
    {
        var result = CharParsers.Space.Parse(new StringInputStream(" "));
        Assert.True(result.IsSuccess);
        
        result = CharParsers.Space.Parse(new StringInputStream("\t"));
        Assert.True(result.IsSuccess);
        
        result = CharParsers.Space.Parse(new StringInputStream("\n"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Spaces_ShouldSkipMultipleSpaces()
    {
        var parser = from _ in CharParsers.Spaces
                     from c in CharParsers.Letter
                     select c;
        
        var result = parser.Parse(new StringInputStream("   x"));
        Assert.True(result.IsSuccess);
        Assert.Equal('x', ((SuccessResult<char, char>)result).Value);
    }

    [Fact]
    public void Identifier_ShouldMatchValidIdentifiers()
    {
        var result = CharParsers.Identifier.Parse(new StringInputStream("myVar123"));
        Assert.True(result.IsSuccess);
        Assert.Equal("myVar123", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Identifier_ShouldStartWithLetterOrUnderscore()
    {
        var result1 = CharParsers.Identifier.Parse(new StringInputStream("_private"));
        Assert.True(result1.IsSuccess);
        
        var result2 = CharParsers.Identifier.Parse(new StringInputStream("123abc"));
        Assert.False(result2.IsSuccess);
    }

    [Fact]
    public void Integer_ShouldParseSignedIntegers()
    {
        var result1 = CharParsers.Integer.Parse(new StringInputStream("123"));
        Assert.True(result1.IsSuccess);
        Assert.Equal(123L, ((SuccessResult<long, char>)result1).Value);
        
        var result2 = CharParsers.Integer.Parse(new StringInputStream("-456"));
        Assert.True(result2.IsSuccess);
        Assert.Equal(-456L, ((SuccessResult<long, char>)result2).Value);
    }

    [Fact]
    public void Double_ShouldParseFloatingPointNumbers()
    {
        var result1 = CharParsers.Double.Parse(new StringInputStream("3.14"));
        Assert.True(result1.IsSuccess);
        Assert.Equal(3.14, ((SuccessResult<double, char>)result1).Value, 2);
        
        var result2 = CharParsers.Double.Parse(new StringInputStream("-2.5e10"));
        Assert.True(result2.IsSuccess);
        Assert.Equal(-2.5e10, ((SuccessResult<double, char>)result2).Value, 0);
    }

    [Fact]
    public void QuotedString_ShouldParseEscapedStrings()
    {
        var result = CharParsers.QuotedString().Parse(new StringInputStream("\"hello\\nworld\""));
        Assert.True(result.IsSuccess);
        Assert.Equal("hello\nworld", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void QuotedString_ShouldHandleEscapedQuotes()
    {
        var result = CharParsers.QuotedString().Parse(new StringInputStream("\"say \\\"hello\\\"\""));
        Assert.True(result.IsSuccess);
        Assert.Equal("say \"hello\"", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void OneOf_ShouldMatchAnyCharacterInSet()
    {
        var parser = CharParsers.OneOf("aeiou");
        
        Assert.True(parser.Parse(new StringInputStream("a")).IsSuccess);
        Assert.True(parser.Parse(new StringInputStream("e")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream("x")).IsSuccess);
    }

    [Fact]
    public void NoneOf_ShouldMatchAnyCharacterNotInSet()
    {
        var parser = CharParsers.NoneOf("aeiou");
        
        Assert.False(parser.Parse(new StringInputStream("a")).IsSuccess);
        Assert.True(parser.Parse(new StringInputStream("x")).IsSuccess);
    }

    [Fact]
    public void TakeWhile_ShouldCollectMatchingCharacters()
    {
        var result = CharParsers.TakeWhile(char.IsDigit).Parse(new StringInputStream("123abc"));
        Assert.True(result.IsSuccess);
        Assert.Equal("123", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Symbol_ShouldSkipTrailingWhitespace()
    {
        var parser = from a in CharParsers.Symbol("hello")
                     from b in CharParsers.Symbol("world")
                     select a + " " + b;
        
        var result = parser.Parse(new StringInputStream("hello   world"));
        Assert.True(result.IsSuccess);
        Assert.Equal("hello world", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void Parens_ShouldParseBetweenParentheses()
    {
        // Parensはトークナイザーで、前後の空白を自動的に処理する
        var parser = CharParsers.Parens(CharParsers.Integer.Lexeme());
        var result = parser.Parse(new StringInputStream("( 42 )"));
        Assert.True(result.IsSuccess);
        Assert.Equal(42L, ((SuccessResult<long, char>)result).Value);
    }

    [Fact]
    public void CommaSeparated_ShouldParseCommaSeparatedList()
    {
        var parser = CharParsers.CommaSeparated(CharParsers.Integer.Lexeme());
        var result = parser.Parse(new StringInputStream("1, 2, 3"));
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1L, 2L, 3L }, ((SuccessResult<IReadOnlyList<long>, char>)result).Value);
    }
}
