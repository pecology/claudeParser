using ClaudeParser.Core;
using ClaudeParser.Combinators;

namespace ClaudeParser.Examples;

/// <summary>
/// CSV (Comma-Separated Values) パーサー
/// RFC 4180に準拠したCSVをパースします。
/// </summary>
public static class CsvParser
{
    /// <summary>
    /// CSVをパースし、行のリスト（各行はフィールドのリスト）を返します。
    /// </summary>
    public static Parser<IReadOnlyList<IReadOnlyList<string>>, char> Csv(char delimiter = ',')
    {
        var field = QuotedField.Or(UnquotedField(delimiter));
        var record = field.SepBy(CharParsers.Char(delimiter));
        var eol = CharParsers.EndOfLine.Or(CharParsers.String(""));
        
        return from rows in record.SepBy(CharParsers.EndOfLine)
               from _ in Parsers.Eof<char>().Or(CharParsers.EndOfLine.Ignore())
               select (IReadOnlyList<IReadOnlyList<string>>)rows.Where(r => r.Count > 0 || r.Any(f => f.Length > 0)).ToList();
    }

    /// <summary>
    /// ダブルクォートで囲まれたフィールド
    /// 内部のダブルクォートは "" でエスケープ
    /// </summary>
    private static Parser<string, char> QuotedField =>
        (from _ in CharParsers.Char('"')
         from content in QuotedContent
         from __ in CharParsers.Char('"')
         select content).Named("QuotedField").WithExpected("クォートされたフィールド");

    private static Parser<string, char> QuotedContent =>
        new Parser<string, char>((input, ctx) =>
        {
            var result = new List<char>();
            var current = input;

            while (!current.IsAtEnd)
            {
                if (current.Current == '"')
                {
                    var next = current.Advance();
                    if (!next.IsAtEnd && next.Current == '"')
                    {
                        // エスケープされたダブルクォート
                        result.Add('"');
                        current = next.Advance();
                    }
                    else
                    {
                        // フィールド終端
                        break;
                    }
                }
                else
                {
                    result.Add(current.Current);
                    current = current.Advance();
                }
            }

            return ParseResult<string, char>.Success(new string(result.ToArray()), current);
        }, "QuotedContent");

    /// <summary>
    /// クォートされていないフィールド
    /// </summary>
    private static Parser<string, char> UnquotedField(char delimiter) =>
        CharParsers.TakeWhile(c => c != delimiter && c != '\r' && c != '\n', "フィールド文字")
                   .Named("UnquotedField");

    /// <summary>
    /// CSVをパースして、辞書のリストとして返します（ヘッダー行あり）
    /// </summary>
    public static Parser<IReadOnlyList<Dictionary<string, string>>, char> CsvWithHeader(char delimiter = ',')
    {
        return new Parser<IReadOnlyList<Dictionary<string, string>>, char>((input, ctx) =>
        {
            var csvResult = Csv(delimiter).Parse(input, ctx);
            if (csvResult is not SuccessResult<IReadOnlyList<IReadOnlyList<string>>, char> success)
                return ((FailureResult<IReadOnlyList<IReadOnlyList<string>>, char>)csvResult)
                    .Cast<IReadOnlyList<Dictionary<string, string>>>();

            var rows = success.Value;
            if (rows.Count == 0)
                return ParseResult<IReadOnlyList<Dictionary<string, string>>, char>.Success(
                    Array.Empty<Dictionary<string, string>>(), success.Remaining);

            var headers = rows[0];
            var records = new List<Dictionary<string, string>>();

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var dict = new Dictionary<string, string>();
                
                for (int j = 0; j < headers.Count; j++)
                {
                    var value = j < row.Count ? row[j] : "";
                    dict[headers[j]] = value;
                }
                
                records.Add(dict);
            }

            return ParseResult<IReadOnlyList<Dictionary<string, string>>, char>.Success(
                records, success.Remaining);
        }, "CsvWithHeader");
    }

    /// <summary>
    /// CSVファイルをパースします（ユーティリティメソッド）
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string csv, char delimiter = ',')
    {
        var input = new StringInputStream(csv);
        var result = Csv(delimiter).Parse(input);
        return result.GetValueOrThrow(csv);
    }

    /// <summary>
    /// ヘッダー付きCSVファイルをパースします（ユーティリティメソッド）
    /// </summary>
    public static IReadOnlyList<Dictionary<string, string>> ParseWithHeader(string csv, char delimiter = ',')
    {
        var input = new StringInputStream(csv);
        var result = CsvWithHeader(delimiter).Parse(input);
        return result.GetValueOrThrow(csv);
    }
}
