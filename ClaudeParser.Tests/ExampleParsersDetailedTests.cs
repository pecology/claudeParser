using ClaudeParser.Core;
using ClaudeParser.Examples;

namespace ClaudeParser.Tests;

/// <summary>
/// サンプルパーサーの詳細テスト（境界値・条件カバレッジ）
/// </summary>
public class ExampleParsersDetailedTests
{
    #region RegexParser AST Tests

    [Fact]
    public void RegexParser_ParsePattern_Literal_ShouldCreateLiteralNode()
    {
        var ast = RegexParser.ParsePattern("a");
        Assert.IsType<RegexParser.LiteralNode>(ast);
        Assert.Equal('a', ((RegexParser.LiteralNode)ast).Char);
    }

    [Fact]
    public void RegexParser_ParsePattern_AnyChar_ShouldCreateAnyCharNode()
    {
        var ast = RegexParser.ParsePattern(".");
        Assert.IsType<RegexParser.AnyCharNode>(ast);
    }

    [Fact]
    public void RegexParser_ParsePattern_Sequence_ShouldCreateSequenceNode()
    {
        var ast = RegexParser.ParsePattern("abc");
        Assert.IsType<RegexParser.SequenceNode>(ast);
        var seq = (RegexParser.SequenceNode)ast;
        Assert.Equal(3, seq.Nodes.Count);
    }

    [Fact]
    public void RegexParser_ParsePattern_Alternation_ShouldCreateAlternationNode()
    {
        var ast = RegexParser.ParsePattern("a|b|c");
        Assert.IsType<RegexParser.AlternationNode>(ast);
        var alt = (RegexParser.AlternationNode)ast;
        Assert.Equal(3, alt.Alternatives.Count);
    }

    [Fact]
    public void RegexParser_ParsePattern_Group_ShouldCreateGroupNode()
    {
        var ast = RegexParser.ParsePattern("(a)");
        Assert.IsType<RegexParser.GroupNode>(ast);
    }

    [Fact]
    public void RegexParser_ParsePattern_CharClass_Simple_ShouldCreateCharClassNode()
    {
        var ast = RegexParser.ParsePattern("[abc]");
        Assert.IsType<RegexParser.CharClassNode>(ast);
        var cc = (RegexParser.CharClassNode)ast;
        Assert.False(cc.Negated);
        Assert.Equal(3, cc.Items.Count);
    }

    [Fact]
    public void RegexParser_ParsePattern_CharClass_Negated_ShouldBeNegated()
    {
        var ast = RegexParser.ParsePattern("[^abc]");
        Assert.IsType<RegexParser.CharClassNode>(ast);
        var cc = (RegexParser.CharClassNode)ast;
        Assert.True(cc.Negated);
    }

    [Fact]
    public void RegexParser_ParsePattern_CharClass_Range_ShouldCreateCharRange()
    {
        var ast = RegexParser.ParsePattern("[a-z]");
        Assert.IsType<RegexParser.CharClassNode>(ast);
        var cc = (RegexParser.CharClassNode)ast;
        Assert.Single(cc.Items);
        Assert.IsType<RegexParser.CharRange>(cc.Items[0]);
        var range = (RegexParser.CharRange)cc.Items[0];
        Assert.Equal('a', range.Start);
        Assert.Equal('z', range.End);
    }

    [Fact]
    public void RegexParser_ParsePattern_Quantifier_Star_ShouldCreateRepeatNode()
    {
        var ast = RegexParser.ParsePattern("a*");
        Assert.IsType<RegexParser.RepeatNode>(ast);
        var rep = (RegexParser.RepeatNode)ast;
        Assert.Equal(0, rep.Min);
        Assert.Null(rep.Max);
    }

    [Fact]
    public void RegexParser_ParsePattern_Quantifier_Plus_ShouldCreateRepeatNode()
    {
        var ast = RegexParser.ParsePattern("a+");
        Assert.IsType<RegexParser.RepeatNode>(ast);
        var rep = (RegexParser.RepeatNode)ast;
        Assert.Equal(1, rep.Min);
        Assert.Null(rep.Max);
    }

    [Fact]
    public void RegexParser_ParsePattern_Quantifier_Question_ShouldCreateRepeatNode()
    {
        var ast = RegexParser.ParsePattern("a?");
        Assert.IsType<RegexParser.RepeatNode>(ast);
        var rep = (RegexParser.RepeatNode)ast;
        Assert.Equal(0, rep.Min);
        Assert.Equal(1, rep.Max);
    }

    [Fact]
    public void RegexParser_ParsePattern_Quantifier_ExactCount_ShouldCreateRepeatNode()
    {
        var ast = RegexParser.ParsePattern("a{3}");
        Assert.IsType<RegexParser.RepeatNode>(ast);
        var rep = (RegexParser.RepeatNode)ast;
        Assert.Equal(3, rep.Min);
        Assert.Equal(3, rep.Max);
    }

    [Fact]
    public void RegexParser_ParsePattern_Quantifier_Range_ShouldCreateRepeatNode()
    {
        var ast = RegexParser.ParsePattern("a{2,5}");
        Assert.IsType<RegexParser.RepeatNode>(ast);
        var rep = (RegexParser.RepeatNode)ast;
        Assert.Equal(2, rep.Min);
        Assert.Equal(5, rep.Max);
    }

    [Fact]
    public void RegexParser_ParsePattern_Quantifier_AtLeast_ShouldCreateRepeatNode()
    {
        var ast = RegexParser.ParsePattern("a{2,}");
        Assert.IsType<RegexParser.RepeatNode>(ast);
        var rep = (RegexParser.RepeatNode)ast;
        Assert.Equal(2, rep.Min);
        Assert.Null(rep.Max);
    }

    [Fact]
    public void RegexParser_ParsePattern_StartAnchor_ShouldCreateStartAnchorNode()
    {
        var ast = RegexParser.ParsePattern("^a");
        Assert.IsType<RegexParser.SequenceNode>(ast);
        var seq = (RegexParser.SequenceNode)ast;
        Assert.IsType<RegexParser.StartAnchorNode>(seq.Nodes[0]);
    }

    [Fact]
    public void RegexParser_ParsePattern_EndAnchor_ShouldCreateEndAnchorNode()
    {
        var ast = RegexParser.ParsePattern("a$");
        Assert.IsType<RegexParser.SequenceNode>(ast);
        var seq = (RegexParser.SequenceNode)ast;
        Assert.IsType<RegexParser.EndAnchorNode>(seq.Nodes[1]);
    }

    [Fact]
    public void RegexParser_ParsePattern_SpecialClass_d_ShouldCreateSpecialClassNode()
    {
        var ast = RegexParser.ParsePattern(@"\d");
        Assert.IsType<RegexParser.SpecialClassNode>(ast);
        Assert.Equal('d', ((RegexParser.SpecialClassNode)ast).Type);
    }

    [Fact]
    public void RegexParser_ParsePattern_SpecialClass_w_ShouldCreateSpecialClassNode()
    {
        var ast = RegexParser.ParsePattern(@"\w");
        Assert.IsType<RegexParser.SpecialClassNode>(ast);
        Assert.Equal('w', ((RegexParser.SpecialClassNode)ast).Type);
    }

    [Fact]
    public void RegexParser_ParsePattern_SpecialClass_s_ShouldCreateSpecialClassNode()
    {
        var ast = RegexParser.ParsePattern(@"\s");
        Assert.IsType<RegexParser.SpecialClassNode>(ast);
        Assert.Equal('s', ((RegexParser.SpecialClassNode)ast).Type);
    }

    [Fact]
    public void RegexParser_ParsePattern_SpecialClass_D_ShouldCreateSpecialClassNode()
    {
        var ast = RegexParser.ParsePattern(@"\D");
        Assert.IsType<RegexParser.SpecialClassNode>(ast);
        Assert.Equal('D', ((RegexParser.SpecialClassNode)ast).Type);
    }

    [Fact]
    public void RegexParser_ParsePattern_SpecialClass_W_ShouldCreateSpecialClassNode()
    {
        var ast = RegexParser.ParsePattern(@"\W");
        Assert.IsType<RegexParser.SpecialClassNode>(ast);
        Assert.Equal('W', ((RegexParser.SpecialClassNode)ast).Type);
    }

    [Fact]
    public void RegexParser_ParsePattern_SpecialClass_S_ShouldCreateSpecialClassNode()
    {
        var ast = RegexParser.ParsePattern(@"\S");
        Assert.IsType<RegexParser.SpecialClassNode>(ast);
        Assert.Equal('S', ((RegexParser.SpecialClassNode)ast).Type);
    }

    [Fact]
    public void RegexParser_ParsePattern_EscapedMetaChar_ShouldCreateLiteral()
    {
        var ast = RegexParser.ParsePattern(@"\.");
        Assert.IsType<RegexParser.LiteralNode>(ast);
        Assert.Equal('.', ((RegexParser.LiteralNode)ast).Char);
    }

    [Fact]
    public void RegexParser_ParsePattern_EscapedNewline_ShouldCreateLiteral()
    {
        var ast = RegexParser.ParsePattern(@"\n");
        Assert.IsType<RegexParser.LiteralNode>(ast);
        Assert.Equal('\n', ((RegexParser.LiteralNode)ast).Char);
    }

    [Fact]
    public void RegexParser_ParsePattern_EscapedTab_ShouldCreateLiteral()
    {
        var ast = RegexParser.ParsePattern(@"\t");
        Assert.IsType<RegexParser.LiteralNode>(ast);
        Assert.Equal('\t', ((RegexParser.LiteralNode)ast).Char);
    }

    [Fact]
    public void RegexParser_ParsePattern_EscapedReturn_ShouldCreateLiteral()
    {
        var ast = RegexParser.ParsePattern(@"\r");
        Assert.IsType<RegexParser.LiteralNode>(ast);
        Assert.Equal('\r', ((RegexParser.LiteralNode)ast).Char);
    }

    #endregion

    #region RegexParser ToParser Tests

    [Fact]
    public void RegexParser_LiteralNode_ToParser_ShouldMatchChar()
    {
        var node = new RegexParser.LiteralNode('x');
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("xyz"));
        Assert.True(result.IsSuccess);
        Assert.Equal("x", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_AnyCharNode_ToParser_ShouldMatchAnyExceptNewline()
    {
        var node = new RegexParser.AnyCharNode();
        var parser = node.ToParser();
        
        // Should match any char
        var result = parser.Parse(new StringInputStream("x"));
        Assert.True(result.IsSuccess);
        
        // Should not match newline
        result = parser.Parse(new StringInputStream("\n"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_CharClassNode_ToParser_Negated_ShouldWork()
    {
        var node = new RegexParser.CharClassNode(
            new[] { new RegexParser.SingleChar('a'), new RegexParser.SingleChar('b') },
            true);
        var parser = node.ToParser();
        
        // Should match 'c' (not in a, b)
        var result = parser.Parse(new StringInputStream("c"));
        Assert.True(result.IsSuccess);
        
        // Should not match 'a'
        result = parser.Parse(new StringInputStream("a"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_CharRange_Matches_ShouldWorkCorrectly()
    {
        var range = new RegexParser.CharRange('a', 'z');
        Assert.True(range.Matches('a'));
        Assert.True(range.Matches('m'));
        Assert.True(range.Matches('z'));
        Assert.False(range.Matches('A'));
        Assert.False(range.Matches('0'));
    }

    [Fact]
    public void RegexParser_SingleChar_Matches_ShouldWorkCorrectly()
    {
        var single = new RegexParser.SingleChar('x');
        Assert.True(single.Matches('x'));
        Assert.False(single.Matches('y'));
    }

    [Fact]
    public void RegexParser_SequenceNode_Empty_ShouldReturnEmptyString()
    {
        var node = new RegexParser.SequenceNode(Array.Empty<RegexParser.RegexNode>());
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("test"));
        Assert.True(result.IsSuccess);
        Assert.Equal("", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_AlternationNode_Empty_ShouldReturnEmptyString()
    {
        var node = new RegexParser.AlternationNode(Array.Empty<RegexParser.RegexNode>());
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("test"));
        Assert.True(result.IsSuccess);
        Assert.Equal("", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_AlternationNode_Single_ShouldBehaveAsInner()
    {
        var node = new RegexParser.AlternationNode(new[] { new RegexParser.LiteralNode('x') });
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("x"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_RepeatNode_QuestionMark_ShouldMatchZeroOrOne()
    {
        var node = new RegexParser.RepeatNode(new RegexParser.LiteralNode('a'), 0, 1, true);
        var parser = node.ToParser();
        
        // Should match 0
        var result = parser.Parse(new StringInputStream("b"));
        Assert.True(result.IsSuccess);
        Assert.Equal("", ((SuccessResult<string, char>)result).Value);
        
        // Should match 1
        result = parser.Parse(new StringInputStream("ab"));
        Assert.True(result.IsSuccess);
        Assert.Equal("a", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_RepeatNode_Star_ShouldMatchZeroOrMore()
    {
        var node = new RegexParser.RepeatNode(new RegexParser.LiteralNode('a'), 0, null, true);
        var parser = node.ToParser();
        
        // Should match 0
        var result = parser.Parse(new StringInputStream("b"));
        Assert.True(result.IsSuccess);
        Assert.Equal("", ((SuccessResult<string, char>)result).Value);
        
        // Should match multiple
        result = parser.Parse(new StringInputStream("aaab"));
        Assert.True(result.IsSuccess);
        Assert.Equal("aaa", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_RepeatNode_Plus_ShouldMatchOneOrMore()
    {
        var node = new RegexParser.RepeatNode(new RegexParser.LiteralNode('a'), 1, null, true);
        var parser = node.ToParser();
        
        // Should fail on 0
        var result = parser.Parse(new StringInputStream("b"));
        Assert.False(result.IsSuccess);
        
        // Should match 1
        result = parser.Parse(new StringInputStream("ab"));
        Assert.True(result.IsSuccess);
        Assert.Equal("a", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_RepeatNode_General_ShouldRespectBounds()
    {
        var node = new RegexParser.RepeatNode(new RegexParser.LiteralNode('a'), 2, 4, true);
        var parser = node.ToParser();
        
        // Should fail on 1
        var result = parser.Parse(new StringInputStream("a"));
        Assert.False(result.IsSuccess);
        
        // Should match 2
        result = parser.Parse(new StringInputStream("aa"));
        Assert.True(result.IsSuccess);
        Assert.Equal("aa", ((SuccessResult<string, char>)result).Value);
        
        // Should match up to 4
        result = parser.Parse(new StringInputStream("aaaaa"));
        Assert.True(result.IsSuccess);
        Assert.Equal("aaaa", ((SuccessResult<string, char>)result).Value);
    }

    [Fact]
    public void RegexParser_StartAnchorNode_AtStart_ShouldSucceed()
    {
        var node = new RegexParser.StartAnchorNode();
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("test"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_StartAnchorNode_NotAtStart_ShouldFail()
    {
        var node = new RegexParser.StartAnchorNode();
        var parser = node.ToParser();
        
        var input = new StringInputStream("test").Advance(); // Move past first char
        var result = parser.Parse(input);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_EndAnchorNode_AtEnd_ShouldSucceed()
    {
        var node = new RegexParser.EndAnchorNode();
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream(""));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_EndAnchorNode_AtNewline_ShouldSucceed()
    {
        var node = new RegexParser.EndAnchorNode();
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("\n"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_EndAnchorNode_NotAtEnd_ShouldFail()
    {
        var node = new RegexParser.EndAnchorNode();
        var parser = node.ToParser();
        
        var result = parser.Parse(new StringInputStream("test"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RegexParser_SpecialClass_d_ShouldMatchDigits()
    {
        var node = new RegexParser.SpecialClassNode('d');
        var parser = node.ToParser();
        
        Assert.True(parser.Parse(new StringInputStream("5")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream("a")).IsSuccess);
    }

    [Fact]
    public void RegexParser_SpecialClass_D_ShouldMatchNonDigits()
    {
        var node = new RegexParser.SpecialClassNode('D');
        var parser = node.ToParser();
        
        Assert.True(parser.Parse(new StringInputStream("a")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream("5")).IsSuccess);
    }

    [Fact]
    public void RegexParser_SpecialClass_w_ShouldMatchWordChars()
    {
        var node = new RegexParser.SpecialClassNode('w');
        var parser = node.ToParser();
        
        Assert.True(parser.Parse(new StringInputStream("a")).IsSuccess);
        Assert.True(parser.Parse(new StringInputStream("5")).IsSuccess);
        Assert.True(parser.Parse(new StringInputStream("_")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream("-")).IsSuccess);
    }

    [Fact]
    public void RegexParser_SpecialClass_W_ShouldMatchNonWordChars()
    {
        var node = new RegexParser.SpecialClassNode('W');
        var parser = node.ToParser();
        
        Assert.True(parser.Parse(new StringInputStream("-")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream("a")).IsSuccess);
    }

    [Fact]
    public void RegexParser_SpecialClass_s_ShouldMatchWhitespace()
    {
        var node = new RegexParser.SpecialClassNode('s');
        var parser = node.ToParser();
        
        Assert.True(parser.Parse(new StringInputStream(" ")).IsSuccess);
        Assert.True(parser.Parse(new StringInputStream("\t")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream("a")).IsSuccess);
    }

    [Fact]
    public void RegexParser_SpecialClass_S_ShouldMatchNonWhitespace()
    {
        var node = new RegexParser.SpecialClassNode('S');
        var parser = node.ToParser();
        
        Assert.True(parser.Parse(new StringInputStream("a")).IsSuccess);
        Assert.False(parser.Parse(new StringInputStream(" ")).IsSuccess);
    }

    #endregion

    #region RegexParser Integration Tests

    [Fact]
    public void RegexParser_Match_EmptyPattern_ShouldReturnEmptyString()
    {
        var match = RegexParser.Match("", "test");
        Assert.Equal("", match);
    }

    [Fact]
    public void RegexParser_Match_NoMatch_ShouldReturnNull()
    {
        var match = RegexParser.Match("[0-9]+", "abc");
        Assert.Null(match);
    }

    [Fact]
    public void RegexParser_Match_ComplexPattern_ShouldWork()
    {
        var match = RegexParser.Match(@"\d{2,4}-\d{2,4}-\d{4}", "03-1234-5678 is a phone");
        Assert.Equal("03-1234-5678", match);
    }

    [Fact]
    public void RegexParser_IsMatch_FullMatch_ShouldReturnTrue()
    {
        Assert.True(RegexParser.IsMatch(@"\d+", "12345"));
    }

    [Fact]
    public void RegexParser_IsMatch_PartialMatch_ShouldReturnFalse()
    {
        Assert.False(RegexParser.IsMatch(@"\d+", "123abc"));
    }

    [Fact]
    public void RegexParser_FindAll_MultipleMatches_ShouldFindAll()
    {
        var matches = RegexParser.FindAll(@"\d+", "a1b22c333d").ToList();
        Assert.Equal(3, matches.Count);
        Assert.Equal("1", matches[0].match);
        Assert.Equal("22", matches[1].match);
        Assert.Equal("333", matches[2].match);
    }

    [Fact]
    public void RegexParser_FindAll_NoMatches_ShouldReturnEmpty()
    {
        var matches = RegexParser.FindAll(@"\d+", "abc").ToList();
        Assert.Empty(matches);
    }

    [Fact]
    public void RegexParser_FindAll_EmptyInput_ShouldReturnEmpty()
    {
        var matches = RegexParser.FindAll(@"\d+", "").ToList();
        Assert.Empty(matches);
    }

    #endregion

    #region Calculator Parser Edge Cases

    [Fact]
    public void Calculator_TryEvaluate_ValidExpression_ShouldReturnValue()
    {
        var result = CalculatorParser.TryEvaluate("1 + 2");
        Assert.NotNull(result);
        Assert.Equal(3.0, result.Value);
    }

    [Fact]
    public void Calculator_TryEvaluate_InvalidExpression_ShouldReturnNull()
    {
        // "1 + + 2" actually parses as "1 + (+2)" = 3, so use a clearly invalid expression
        var result = CalculatorParser.TryEvaluate("1 + * 2");
        Assert.Null(result);
    }

    [Fact]
    public void Calculator_DoubleNegation_ShouldWork()
    {
        Assert.Equal(5, CalculatorParser.Evaluate("--5"));
    }

    [Fact]
    public void Calculator_MixedUnaryAndBinary_ShouldWork()
    {
        Assert.Equal(-2, CalculatorParser.Evaluate("-5 + 3"));
        Assert.Equal(-8, CalculatorParser.Evaluate("-5 - 3"));
    }

    [Fact]
    public void Calculator_ComplexNested_ShouldWork()
    {
        Assert.Equal(10, CalculatorParser.Evaluate("((2 + 3) * 2)"));
    }

    [Fact]
    public void Calculator_ModuloWithNegative_ShouldWork()
    {
        Assert.Equal(-1, CalculatorParser.Evaluate("-10 % 3"));
    }

    [Fact]
    public void Calculator_DivisionByZero_ShouldReturnInfinity()
    {
        var result = CalculatorParser.Evaluate("1 / 0");
        Assert.True(double.IsInfinity(result));
    }

    #endregion

    #region JSON Parser Edge Cases

    [Fact]
    public void JsonParser_NumberWithLeadingZero_ShouldParse()
    {
        var result = JsonParser.Parse("0");
        Assert.IsType<JsonParser.JsonNumber>(result);
        Assert.Equal(0, ((JsonParser.JsonNumber)result).Value);
    }

    [Fact]
    public void JsonParser_StringWithAllEscapes_ShouldParse()
    {
        var result = JsonParser.Parse(@"""\""\\\/\b\f\n\r\t""");
        Assert.IsType<JsonParser.JsonString>(result);
        var str = ((JsonParser.JsonString)result).Value;
        Assert.Contains("\"", str);
        Assert.Contains("\\", str);
        Assert.Contains("/", str);
    }

    [Fact]
    public void JsonParser_NestedArray_ShouldParse()
    {
        var result = JsonParser.Parse("[[1, 2], [3, 4]]");
        Assert.IsType<JsonParser.JsonArray>(result);
        var arr = (JsonParser.JsonArray)result;
        Assert.Equal(2, arr.Elements.Count);
    }

    [Fact]
    public void JsonParser_EmptyObject_ShouldParse()
    {
        var result = JsonParser.Parse("{}");
        Assert.IsType<JsonParser.JsonObject>(result);
    }

    [Fact]
    public void JsonParser_EmptyArray_ShouldParse()
    {
        var result = JsonParser.Parse("[]");
        Assert.IsType<JsonParser.JsonArray>(result);
    }

    [Fact]
    public void JsonParser_ToPrettyString_Null_ShouldWork()
    {
        var json = new JsonParser.JsonNull();
        Assert.Equal("null", json.ToPrettyString());
    }

    [Fact]
    public void JsonParser_ToPrettyString_Bool_ShouldWork()
    {
        var jsonTrue = new JsonParser.JsonBool(true);
        var jsonFalse = new JsonParser.JsonBool(false);
        Assert.Equal("true", jsonTrue.ToPrettyString());
        Assert.Equal("false", jsonFalse.ToPrettyString());
    }

    [Fact]
    public void JsonParser_ToPrettyString_Number_ShouldWork()
    {
        var json = new JsonParser.JsonNumber(42);
        Assert.Equal("42", json.ToPrettyString());
    }

    [Fact]
    public void JsonParser_ToPrettyString_String_ShouldEscape()
    {
        var json = new JsonParser.JsonString("hello\nworld");
        var pretty = json.ToPrettyString();
        Assert.Contains("\\n", pretty);
    }

    [Fact]
    public void JsonParser_ToPrettyString_EmptyArray_ShouldWork()
    {
        var json = new JsonParser.JsonArray(Array.Empty<JsonParser.JsonValue>());
        Assert.Equal("[]", json.ToPrettyString());
    }

    [Fact]
    public void JsonParser_ToPrettyString_EmptyObject_ShouldWork()
    {
        var json = new JsonParser.JsonObject(new Dictionary<string, JsonParser.JsonValue>());
        Assert.Equal("{}", json.ToPrettyString());
    }

    #endregion

    #region CSV Parser Edge Cases

    [Fact]
    public void CsvParser_EmptyInput_ShouldReturnSingleEmptyRow()
    {
        // Empty input is treated as a single row with one empty field
        var result = CsvParser.Parse("");
        Assert.Single(result);
        Assert.Single(result[0]);
        Assert.Equal("", result[0][0]);
    }

    [Fact]
    public void CsvParser_SingleValue_ShouldParse()
    {
        var result = CsvParser.Parse("value");
        Assert.Single(result);
        Assert.Single(result[0]);
    }

    [Fact]
    public void CsvParser_TrailingNewline_ShouldParse()
    {
        // Trailing newline creates an extra empty row
        var result = CsvParser.Parse("a,b\n");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b" }, result[0]);
    }

    [Fact]
    public void CsvParser_WithHeader_EmptyData_ShouldReturnEmpty()
    {
        var result = CsvParser.ParseWithHeader("name,age");
        Assert.Empty(result);
    }

    [Fact]
    public void CsvParser_WithHeader_MissingFields_ShouldUseEmptyString()
    {
        var result = CsvParser.ParseWithHeader("a,b,c\n1,2");
        Assert.Single(result);
        Assert.Equal("", result[0]["c"]);
    }

    [Fact]
    public void CsvParser_QuotedField_WithNewline_ShouldParse()
    {
        var result = CsvParser.Parse("\"line1\nline2\"");
        Assert.Equal("line1\nline2", result[0][0]);
    }

    [Fact]
    public void CsvParser_QuotedField_WithComma_ShouldParse()
    {
        var result = CsvParser.Parse("\"a,b,c\"");
        Assert.Equal("a,b,c", result[0][0]);
    }

    [Fact]
    public void CsvParser_TabDelimiter_ShouldWork()
    {
        var result = CsvParser.Parse("a\tb\tc", '\t');
        Assert.Equal(3, result[0].Count);
    }

    [Fact]
    public void CsvParser_SemicolonDelimiter_ShouldWork()
    {
        var result = CsvParser.Parse("a;b;c", ';');
        Assert.Equal(3, result[0].Count);
    }

    #endregion
}
