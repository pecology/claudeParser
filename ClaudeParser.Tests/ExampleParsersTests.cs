using ClaudeParser.Examples;

namespace ClaudeParser.Tests;

/// <summary>
/// サンプルパーサーのテスト
/// </summary>
public class ExampleParsersTests
{
    #region CSV Parser Tests

    [Fact]
    public void CsvParser_ShouldParseSimpleCsv()
    {
        var csv = "a,b,c\n1,2,3";
        var result = CsvParser.Parse(csv);
        
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b", "c" }, result[0]);
        Assert.Equal(new[] { "1", "2", "3" }, result[1]);
    }

    [Fact]
    public void CsvParser_ShouldHandleQuotedFields()
    {
        var csv = "name,description\n\"John Doe\",\"A person with, commas\"";
        var result = CsvParser.Parse(csv);
        
        Assert.Equal(2, result.Count);
        Assert.Equal("John Doe", result[1][0]);
        Assert.Equal("A person with, commas", result[1][1]);
    }

    [Fact]
    public void CsvParser_ShouldHandleEscapedQuotes()
    {
        var csv = "text\n\"He said \"\"Hello\"\"\"";
        var result = CsvParser.Parse(csv);
        
        Assert.Equal("He said \"Hello\"", result[1][0]);
    }

    [Fact]
    public void CsvParser_ShouldHandleNewlinesInQuotedFields()
    {
        var csv = "text\n\"Line1\nLine2\"";
        var result = CsvParser.Parse(csv);
        
        Assert.Equal("Line1\nLine2", result[1][0]);
    }

    [Fact]
    public void CsvParser_WithHeader_ShouldReturnDictionaries()
    {
        var csv = "name,age,city\nAlice,30,Tokyo\nBob,25,Osaka";
        var result = CsvParser.ParseWithHeader(csv);
        
        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0]["name"]);
        Assert.Equal("30", result[0]["age"]);
        Assert.Equal("Tokyo", result[0]["city"]);
    }

    [Fact]
    public void CsvParser_ShouldHandleEmptyFields()
    {
        var csv = "a,,c\n1,,3";
        var result = CsvParser.Parse(csv);
        
        Assert.Equal("", result[0][1]);
        Assert.Equal("", result[1][1]);
    }

    #endregion

    #region Calculator Parser Tests

    [Fact]
    public void Calculator_ShouldEvaluateSimpleAddition()
    {
        Assert.Equal(5, CalculatorParser.Evaluate("2 + 3"));
    }

    [Fact]
    public void Calculator_ShouldEvaluateMultiplication()
    {
        Assert.Equal(6, CalculatorParser.Evaluate("2 * 3"));
    }

    [Fact]
    public void Calculator_ShouldRespectPrecedence()
    {
        Assert.Equal(7, CalculatorParser.Evaluate("1 + 2 * 3"));
        Assert.Equal(9, CalculatorParser.Evaluate("(1 + 2) * 3"));
    }

    [Fact]
    public void Calculator_ShouldEvaluateSubtraction()
    {
        Assert.Equal(2, CalculatorParser.Evaluate("5 - 3"));
    }

    [Fact]
    public void Calculator_ShouldEvaluateDivision()
    {
        Assert.Equal(2, CalculatorParser.Evaluate("6 / 3"));
    }

    [Fact]
    public void Calculator_ShouldHandleNegativeNumbers()
    {
        Assert.Equal(-5, CalculatorParser.Evaluate("-5"));
        Assert.Equal(-8, CalculatorParser.Evaluate("-3 - 5"));
    }

    [Fact]
    public void Calculator_ShouldHandleExponentiation()
    {
        Assert.Equal(8, CalculatorParser.Evaluate("2 ^ 3"));
    }

    [Fact]
    public void Calculator_ExponentiationShouldBeRightAssociative()
    {
        // 2^3^2 = 2^(3^2) = 2^9 = 512
        Assert.Equal(512, CalculatorParser.Evaluate("2 ^ 3 ^ 2"));
    }

    [Fact]
    public void Calculator_SubtractionShouldBeLeftAssociative()
    {
        // 10-5-2 = (10-5)-2 = 3
        Assert.Equal(3, CalculatorParser.Evaluate("10 - 5 - 2"));
    }

    [Fact]
    public void Calculator_ShouldHandleFloatingPoint()
    {
        Assert.Equal(5.5, CalculatorParser.Evaluate("2.5 + 3"), 0.001);
    }

    [Fact]
    public void Calculator_ShouldHandleComplexExpression()
    {
        Assert.Equal(25, CalculatorParser.Evaluate("(1 + 2) * (3 + 4) + 4"));
    }

    [Fact]
    public void Calculator_ShouldHandleModulo()
    {
        Assert.Equal(1, CalculatorParser.Evaluate("10 % 3"));
    }

    [Fact]
    public void CalculatorAst_ShouldParseExpression()
    {
        var ast = ExpressionAstParser.Parse("1 + 2 * 3");
        Assert.Equal(7, ast.Evaluate());
    }

    #endregion

    #region JSON Parser Tests

    [Fact]
    public void JsonParser_ShouldParseNull()
    {
        var result = JsonParser.Parse("null");
        Assert.IsType<JsonParser.JsonNull>(result);
    }

    [Fact]
    public void JsonParser_ShouldParseBooleans()
    {
        var trueResult = JsonParser.Parse("true");
        Assert.IsType<JsonParser.JsonBool>(trueResult);
        Assert.True(((JsonParser.JsonBool)trueResult).Value);

        var falseResult = JsonParser.Parse("false");
        Assert.False(((JsonParser.JsonBool)falseResult).Value);
    }

    [Fact]
    public void JsonParser_ShouldParseNumbers()
    {
        var result = (JsonParser.JsonNumber)JsonParser.Parse("42");
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void JsonParser_ShouldParseNegativeNumbers()
    {
        var result = (JsonParser.JsonNumber)JsonParser.Parse("-3.14");
        Assert.Equal(-3.14, result.Value, 2);
    }

    [Fact]
    public void JsonParser_ShouldParseScientificNotation()
    {
        var result = (JsonParser.JsonNumber)JsonParser.Parse("1.5e10");
        Assert.Equal(1.5e10, result.Value, 0);
    }

    [Fact]
    public void JsonParser_ShouldParseStrings()
    {
        var result = (JsonParser.JsonString)JsonParser.Parse("\"hello\"");
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void JsonParser_ShouldParseEscapedStrings()
    {
        var result = (JsonParser.JsonString)JsonParser.Parse("\"hello\\nworld\"");
        Assert.Equal("hello\nworld", result.Value);
    }

    [Fact]
    public void JsonParser_ShouldParseUnicodeEscapes()
    {
        var result = (JsonParser.JsonString)JsonParser.Parse("\"\\u0041\"");
        Assert.Equal("A", result.Value);
    }

    [Fact]
    public void JsonParser_ShouldParseEmptyArray()
    {
        var result = (JsonParser.JsonArray)JsonParser.Parse("[]");
        Assert.Empty(result.Elements);
    }

    [Fact]
    public void JsonParser_ShouldParseArray()
    {
        var result = (JsonParser.JsonArray)JsonParser.Parse("[1, 2, 3]");
        Assert.Equal(3, result.Elements.Count);
    }

    [Fact]
    public void JsonParser_ShouldParseEmptyObject()
    {
        var result = (JsonParser.JsonObject)JsonParser.Parse("{}");
        Assert.Empty(result.Properties);
    }

    [Fact]
    public void JsonParser_ShouldParseObject()
    {
        var result = (JsonParser.JsonObject)JsonParser.Parse("{\"name\": \"John\", \"age\": 30}");
        Assert.Equal(2, result.Properties.Count);
        Assert.Equal("John", ((JsonParser.JsonString)result.Properties["name"]).Value);
        Assert.Equal(30, ((JsonParser.JsonNumber)result.Properties["age"]).Value);
    }

    [Fact]
    public void JsonParser_ShouldParseNestedJson()
    {
        var json = @"{
            ""person"": {
                ""name"": ""Alice"",
                ""hobbies"": [""reading"", ""gaming""]
            }
        }";
        var result = (JsonParser.JsonObject)JsonParser.Parse(json);
        var person = (JsonParser.JsonObject)result.Properties["person"];
        var hobbies = (JsonParser.JsonArray)person.Properties["hobbies"];
        
        Assert.Equal("Alice", ((JsonParser.JsonString)person.Properties["name"]).Value);
        Assert.Equal(2, hobbies.Elements.Count);
    }

    [Fact]
    public void JsonParser_ShouldRejectInvalidJson()
    {
        Assert.Null(JsonParser.TryParse("{invalid}"));
        Assert.Null(JsonParser.TryParse("['single quotes']"));
    }

    #endregion

    #region Regex Parser Tests

    [Theory]
    [InlineData("a", "a", "a")]
    [InlineData("abc", "abc", "abc")]
    [InlineData("a*", "aaa", "aaa")]
    [InlineData("a+", "aaa", "aaa")]
    [InlineData("a?", "a", "a")]
    [InlineData(".", "x", "x")]
    [InlineData(".+", "xyz", "xyz")]
    public void RegexParser_ShouldMatchPatterns(string pattern, string input, string expected)
    {
        var match = RegexParser.Match(pattern, input);
        Assert.Equal(expected, match);
    }

    [Fact]
    public void RegexParser_ShouldMatchDigitClass()
    {
        Assert.Equal("123", RegexParser.Match("\\d+", "123abc"));
    }

    [Fact]
    public void RegexParser_ShouldMatchWordClass()
    {
        Assert.Equal("hello_123", RegexParser.Match("\\w+", "hello_123!"));
    }

    [Fact]
    public void RegexParser_ShouldMatchCharacterClass()
    {
        Assert.Equal("aeiou", RegexParser.Match("[aeiou]+", "aeiou xyz"));
    }

    [Fact]
    public void RegexParser_ShouldMatchCharacterRange()
    {
        Assert.Equal("abc", RegexParser.Match("[a-c]+", "abcdef"));
    }

    [Fact]
    public void RegexParser_ShouldMatchNegatedClass()
    {
        // [^aeiou]+は母音以外の連続をマッチ（スペースも母音ではない）
        Assert.Equal("xyz ", RegexParser.Match("[^aeiou]+", "xyz aeiou"));
    }

    [Fact]
    public void RegexParser_ShouldMatchAlternation()
    {
        Assert.Equal("cat", RegexParser.Match("cat|dog", "catdog"));
        Assert.Equal("dog", RegexParser.Match("cat|dog", "dog"));
    }

    [Fact]
    public void RegexParser_ShouldMatchGroups()
    {
        // (ab)+は「ab」の1回以上の繰り返しにマッチするため、ababではなくab2回分
        Assert.Equal("abab", RegexParser.Match("(ab)+", "abab"));
    }

    [Fact]
    public void RegexParser_IsMatch_ShouldMatchEntireString()
    {
        Assert.True(RegexParser.IsMatch("\\d+", "123"));
        Assert.False(RegexParser.IsMatch("\\d+", "123abc"));
    }

    [Fact]
    public void RegexParser_FindAll_ShouldFindAllMatches()
    {
        var matches = RegexParser.FindAll("\\d+", "abc123def456ghi").ToList();
        Assert.Equal(2, matches.Count);
        Assert.Equal("123", matches[0].match);
        Assert.Equal("456", matches[1].match);
    }

    [Fact]
    public void RegexParser_ShouldMatchQuantifiers()
    {
        Assert.Equal("aaa", RegexParser.Match("a{3}", "aaaaa"));
        Assert.Equal("aa", RegexParser.Match("a{2,3}", "aa"));
        Assert.Equal("aaa", RegexParser.Match("a{2,3}", "aaaa"));
    }

    [Fact]
    public void RegexParser_ShouldMatchEscapedCharacters()
    {
        Assert.Equal(".", RegexParser.Match("\\.", ".abc"));
        Assert.Equal("*", RegexParser.Match("\\*", "*+?"));
    }

    #endregion
}
