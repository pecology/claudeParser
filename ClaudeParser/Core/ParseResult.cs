namespace ClaudeParser.Core;

/// <summary>
/// トレース情報を表します。
/// </summary>
public record TraceEntry(
    string ParserName,
    Position StartPosition,
    Position? EndPosition,
    bool Success,
    string? Result,
    string? ErrorMessage,
    int Depth,
    TimeSpan? Elapsed = null)
{
    public override string ToString()
    {
        var indent = new string(' ', Depth * 2);
        var status = Success ? "✓" : "✗";
        var result = Success ? $" => {Result}" : $" [エラー: {ErrorMessage}]";
        var pos = EndPosition.HasValue 
            ? $"{StartPosition} -> {EndPosition.Value}"
            : $"{StartPosition}";
        var time = Elapsed.HasValue ? $" ({Elapsed.Value.TotalMilliseconds:F2}ms)" : "";
        return $"{indent}{status} {ParserName} @ {pos}{result}{time}";
    }
}

/// <summary>
/// トレースコレクター。パースの過程を記録します。
/// </summary>
public class TraceCollector
{
    private readonly List<TraceEntry> _entries = new();
    private int _currentDepth;
    
    public IReadOnlyList<TraceEntry> Entries => _entries;
    
    public bool IsEnabled { get; set; } = true;
    
    public int MaxEntries { get; set; } = 10000;

    public void Enter(string parserName, Position position)
    {
        if (!IsEnabled) return;
        if (_entries.Count >= MaxEntries) return;
        
        _entries.Add(new TraceEntry(parserName, position, null, false, null, null, _currentDepth));
        _currentDepth++;
    }

    public void Exit(string parserName, Position startPos, Position endPos, bool success, string? result = null, string? error = null, TimeSpan? elapsed = null)
    {
        if (!IsEnabled) return;
        if (_currentDepth > 0) _currentDepth--;
        if (_entries.Count >= MaxEntries) return;
        
        _entries.Add(new TraceEntry(parserName, startPos, endPos, success, result, error, _currentDepth, elapsed));
    }

    public void Clear()
    {
        _entries.Clear();
        _currentDepth = 0;
    }

    public string ToReport()
    {
        if (_entries.Count == 0)
            return "トレースエントリなし";
        
        var lines = _entries.Select(e => e.ToString());
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// パース結果を表します。
/// </summary>
/// <typeparam name="T">パース結果の値の型</typeparam>
/// <typeparam name="TToken">入力トークンの型</typeparam>
public abstract class ParseResult<T, TToken>
{
    /// <summary>
    /// 成功したかどうか
    /// </summary>
    public abstract bool IsSuccess { get; }
    
    /// <summary>
    /// パース後の残りの入力
    /// </summary>
    public abstract IInputStream<TToken> Remaining { get; }
    
    /// <summary>
    /// エラー情報（成功時もエラー情報を持つことがある：バックトラック時のマージ用）
    /// </summary>
    public abstract ParseError? Error { get; }

    /// <summary>
    /// 成功結果を作成します。
    /// </summary>
    public static ParseResult<T, TToken> Success(T value, IInputStream<TToken> remaining, ParseError? error = null) =>
        new SuccessResult<T, TToken>(value, remaining, error);

    /// <summary>
    /// 失敗結果を作成します。
    /// </summary>
    public static ParseResult<T, TToken> Failure(ParseError error, IInputStream<TToken> remaining) =>
        new FailureResult<T, TToken>(error, remaining);
}

/// <summary>
/// パース成功を表します。
/// </summary>
public sealed class SuccessResult<T, TToken> : ParseResult<T, TToken>
{
    public T Value { get; }
    public override IInputStream<TToken> Remaining { get; }
    public override ParseError? Error { get; }
    public override bool IsSuccess => true;

    public SuccessResult(T value, IInputStream<TToken> remaining, ParseError? error = null)
    {
        Value = value;
        Remaining = remaining;
        Error = error;
    }

    /// <summary>
    /// 値を新しい型に変換します。
    /// </summary>
    public ParseResult<TNew, TToken> Map<TNew>(Func<T, TNew> f) =>
        ParseResult<TNew, TToken>.Success(f(Value), Remaining, Error);
}

/// <summary>
/// パース失敗を表します。
/// </summary>
public sealed class FailureResult<T, TToken> : ParseResult<T, TToken>
{
    private readonly ParseError _error;
    
    /// <summary>
    /// エラー情報。FailureResultでは常に存在します。
    /// </summary>
    public override ParseError? Error => _error;
    
    /// <summary>
    /// エラー情報（non-nullableアクセス）。
    /// </summary>
    public ParseError ErrorValue => _error;
    
    public override IInputStream<TToken> Remaining { get; }
    public override bool IsSuccess => false;

    public FailureResult(ParseError error, IInputStream<TToken> remaining)
    {
        _error = error ?? throw new ArgumentNullException(nameof(error));
        Remaining = remaining;
    }

    /// <summary>
    /// 型を変換します（失敗なので値は変わらない）。
    /// </summary>
    public ParseResult<TNew, TToken> Cast<TNew>() =>
        ParseResult<TNew, TToken>.Failure(_error, Remaining);
}

/// <summary>
/// ParseResultの拡張メソッド
/// </summary>
public static class ParseResultExtensions
{
    /// <summary>
    /// 成功時の値を取得します。失敗時は例外をスローします。
    /// </summary>
    public static T GetValueOrThrow<T, TToken>(this ParseResult<T, TToken> result, string? source = null)
    {
        return result switch
        {
            SuccessResult<T, TToken> s => s.Value,
            FailureResult<T, TToken> f => throw new ParseException(f.ErrorValue, source),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>
    /// 成功時の値をOptionとして取得します。
    /// </summary>
    public static T? GetValueOrDefault<T, TToken>(this ParseResult<T, TToken> result, T? defaultValue = default)
    {
        return result switch
        {
            SuccessResult<T, TToken> s => s.Value,
            _ => defaultValue
        };
    }

    /// <summary>
    /// 結果をパターンマッチします。
    /// </summary>
    public static TResult Match<T, TToken, TResult>(
        this ParseResult<T, TToken> result,
        Func<T, IInputStream<TToken>, TResult> onSuccess,
        Func<ParseError, IInputStream<TToken>, TResult> onFailure)
    {
        return result switch
        {
            SuccessResult<T, TToken> s => onSuccess(s.Value, s.Remaining),
            FailureResult<T, TToken> f => onFailure(f.ErrorValue, f.Remaining),
            _ => throw new InvalidOperationException()
        };
    }
}
