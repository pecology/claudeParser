using System.Text;

namespace ClaudeParser.Core;

/// <summary>
/// 期待されたものを表すメッセージの種類
/// </summary>
public enum ExpectedKind
{
    /// <summary>期待されたトークンまたは文字</summary>
    Expected,
    /// <summary>予期しない入力</summary>
    Unexpected,
    /// <summary>カスタムメッセージ</summary>
    Message,
    /// <summary>入力の終端</summary>
    EndOfInput,
    /// <summary>ネストされたエラー（コンテキスト情報）</summary>
    Nested
}

/// <summary>
/// 単一のエラーメッセージを表します。
/// </summary>
public record ErrorMessage(ExpectedKind Kind, string Text)
{
    public static ErrorMessage Expected(string what) => new(ExpectedKind.Expected, what);
    public static ErrorMessage Unexpected(string what) => new(ExpectedKind.Unexpected, what);
    public static ErrorMessage Message(string msg) => new(ExpectedKind.Message, msg);
    public static ErrorMessage EndOfInput() => new(ExpectedKind.EndOfInput, "入力の終端");
    public static ErrorMessage Nested(string context) => new(ExpectedKind.Nested, context);
}

/// <summary>
/// パースエラーを表します。
/// 複数の期待値やエラーメッセージをマージすることが可能です。
/// </summary>
public class ParseError : IEquatable<ParseError>
{
    /// <summary>
    /// エラーが発生した位置
    /// </summary>
    public Position Position { get; }
    
    /// <summary>
    /// エラーメッセージのリスト
    /// </summary>
    public IReadOnlyList<ErrorMessage> Messages { get; }
    
    /// <summary>
    /// パーサーのコンテキストスタック（どのパーサーで失敗したか）
    /// </summary>
    public IReadOnlyList<string> ContextStack { get; }

    public ParseError(Position position, IEnumerable<ErrorMessage> messages, IEnumerable<string>? contextStack = null)
    {
        Position = position;
        Messages = messages.Distinct().ToList();
        ContextStack = (contextStack?.ToList() ?? new List<string>()).AsReadOnly();
    }

    public ParseError(Position position, ErrorMessage message, IEnumerable<string>? contextStack = null)
        : this(position, new[] { message }, contextStack) { }

    /// <summary>
    /// 位置が同じか後ろのエラーとマージします。
    /// パーサーコンビネーターでは、同じ位置で複数の選択肢が失敗した場合にエラーをマージします。
    /// </summary>
    public ParseError Merge(ParseError other)
    {
        if (other.Position > Position)
            return other;
        if (Position > other.Position)
            return this;
        
        // 同じ位置の場合、メッセージをマージ
        var mergedMessages = Messages.Concat(other.Messages).Distinct().ToList();
        var mergedContext = ContextStack.Concat(other.ContextStack).Distinct().ToList();
        return new ParseError(Position, mergedMessages, mergedContext);
    }

    /// <summary>
    /// コンテキスト情報を追加します。
    /// </summary>
    public ParseError WithContext(string context) =>
        new(Position, Messages, ContextStack.Prepend(context));

    /// <summary>
    /// 人間が読めるエラーメッセージを生成します。
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"パースエラー: {Position}");
        
        var expected = Messages.Where(m => m.Kind == ExpectedKind.Expected).Select(m => m.Text).ToList();
        var unexpected = Messages.Where(m => m.Kind == ExpectedKind.Unexpected).Select(m => m.Text).ToList();
        var custom = Messages.Where(m => m.Kind == ExpectedKind.Message).Select(m => m.Text).ToList();
        var endOfInput = Messages.Any(m => m.Kind == ExpectedKind.EndOfInput);

        if (unexpected.Count > 0)
        {
            sb.AppendLine($"  予期しない入力: {string.Join(", ", unexpected)}");
        }

        if (endOfInput)
        {
            sb.AppendLine("  予期しない入力の終端");
        }

        if (expected.Count > 0)
        {
            if (expected.Count == 1)
            {
                sb.AppendLine($"  期待: {expected[0]}");
            }
            else
            {
                var last = expected[^1];
                var rest = expected[..^1];
                sb.AppendLine($"  期待: {string.Join(", ", rest)} または {last}");
            }
        }

        foreach (var msg in custom)
        {
            sb.AppendLine($"  {msg}");
        }

        if (ContextStack.Count > 0)
        {
            sb.AppendLine("  コンテキスト:");
            foreach (var ctx in ContextStack)
            {
                sb.AppendLine($"    - {ctx}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// ソースコードの該当行を含む詳細なエラーメッセージを生成します。
    /// </summary>
    public string ToDetailedString(string source)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ToString());
        
        // ソースの該当行を取得
        var lines = source.Split('\n');
        if (Position.Line > 0 && Position.Line <= lines.Length)
        {
            var line = lines[Position.Line - 1].TrimEnd('\r');
            sb.AppendLine();
            sb.AppendLine($"  {Position.Line} | {line}");
            
            // キャレットで位置を指す
            var padding = new string(' ', Position.Line.ToString().Length + 3 + Position.Column - 1);
            sb.AppendLine($"{padding}^");
        }

        return sb.ToString().TrimEnd();
    }

    public bool Equals(ParseError? other)
    {
        if (other is null) return false;
        return Position == other.Position && 
               Messages.SequenceEqual(other.Messages);
    }

    public override bool Equals(object? obj) => Equals(obj as ParseError);
    
    public override int GetHashCode() => 
        HashCode.Combine(Position, Messages.Count);
}

/// <summary>
/// パースの例外クラス
/// </summary>
public class ParseException : Exception
{
    public ParseError Error { get; }
    public string? SourceText { get; }

    public ParseException(ParseError error, string? sourceText = null)
        : base(sourceText != null ? error.ToDetailedString(sourceText) : error.ToString())
    {
        Error = error;
        SourceText = sourceText;
    }
}
