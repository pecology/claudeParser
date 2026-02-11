namespace ClaudeParser.Core;

/// <summary>
/// パースのコンテキスト情報（トレース設定など）
/// </summary>
public class ParseContext
{
    /// <summary>
    /// トレースコレクター（nullの場合トレースは無効）
    /// </summary>
    public TraceCollector? Trace { get; set; }
    
    /// <summary>
    /// トレースが有効かどうか
    /// </summary>
    public bool IsTraceEnabled => Trace?.IsEnabled ?? false;
    
    /// <summary>
    /// デフォルトコンテキスト（トレース無効）
    /// </summary>
    public static ParseContext Default { get; } = new();
    
    /// <summary>
    /// トレース有効のコンテキストを作成します。
    /// </summary>
    public static ParseContext WithTracing(TraceCollector? collector = null)
    {
        return new ParseContext { Trace = collector ?? new TraceCollector() };
    }
}

/// <summary>
/// パーサーを表すデリゲート。
/// </summary>
/// <typeparam name="T">パース結果の型</typeparam>
/// <typeparam name="TToken">入力トークンの型</typeparam>
public delegate ParseResult<T, TToken> ParserFunc<T, TToken>(IInputStream<TToken> input, ParseContext context);

/// <summary>
/// パーサー本体。イミュータブルです。
/// クエリ式（LINQ）をサポートします。
/// </summary>
/// <typeparam name="T">パース結果の型</typeparam>
/// <typeparam name="TToken">入力トークンの型</typeparam>
public class Parser<T, TToken>
{
    private readonly ParserFunc<T, TToken> _parse;
    private readonly string _name;

    public Parser(ParserFunc<T, TToken> parse, string name = "anonymous")
    {
        _parse = parse;
        _name = name;
    }

    /// <summary>
    /// パーサーの名前（デバッグ・トレース用）
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// パースを実行します。
    /// </summary>
    public ParseResult<T, TToken> Parse(IInputStream<TToken> input, ParseContext? context = null)
    {
        context ??= ParseContext.Default;
        
        if (context.IsTraceEnabled)
        {
            var startTime = DateTime.Now;
            var startPos = input.Position;
            context.Trace!.Enter(_name, startPos);
            
            var result = _parse(input, context);
            
            var elapsed = DateTime.Now - startTime;
            if (result is SuccessResult<T, TToken> success)
            {
                context.Trace.Exit(_name, startPos, success.Remaining.Position, true, 
                    success.Value?.ToString()?.Truncate(50), elapsed: elapsed);
            }
            else if (result is FailureResult<T, TToken> failure)
            {
                context.Trace.Exit(_name, startPos, failure.Remaining.Position, false,
                    error: failure.ErrorValue.Messages.FirstOrDefault()?.Text, elapsed: elapsed);
            }
            
            return result;
        }
        
        return _parse(input, context);
    }

    /// <summary>
    /// パーサーに名前を付けます（トレース・エラーメッセージ用）
    /// </summary>
    public Parser<T, TToken> Named(string name) =>
        new(_parse, name);

    /// <summary>
    /// パースに失敗した場合のエラーメッセージをカスタマイズします。
    /// </summary>
    public Parser<T, TToken> WithExpected(string expected) =>
        new((input, ctx) =>
        {
            var result = _parse(input, ctx);
            if (result is FailureResult<T, TToken> failure)
            {
                var newError = new ParseError(
                    failure.ErrorValue.Position,
                    ErrorMessage.Expected(expected),
                    failure.ErrorValue.ContextStack);
                return ParseResult<T, TToken>.Failure(newError, failure.Remaining);
            }
            return result;
        }, _name);

    /// <summary>
    /// エラーにコンテキスト情報を追加します。
    /// </summary>
    public Parser<T, TToken> WithContext(string context) =>
        new((input, ctx) =>
        {
            var result = _parse(input, ctx);
            if (result is FailureResult<T, TToken> failure)
            {
                return ParseResult<T, TToken>.Failure(
                    failure.ErrorValue.WithContext(context),
                    failure.Remaining);
            }
            return result;
        }, _name);

    #region クエリ式サポート (LINQ)

    /// <summary>
    /// Select (map) - 結果を変換します。
    /// </summary>
    public Parser<TNew, TToken> Select<TNew>(Func<T, TNew> selector) =>
        new((input, ctx) =>
        {
            var result = _parse(input, ctx);
            return result switch
            {
                SuccessResult<T, TToken> s => ParseResult<TNew, TToken>.Success(selector(s.Value), s.Remaining, s.Error),
                FailureResult<T, TToken> f => f.Cast<TNew>(),
                _ => throw new InvalidOperationException()
            };
        }, $"{_name}.Select");

    /// <summary>
    /// SelectMany (flatMap/bind) - パーサーを連結します。
    /// クエリ式の from ... from ... select をサポートします。
    /// </summary>
    public Parser<TResult, TToken> SelectMany<TNext, TResult>(
        Func<T, Parser<TNext, TToken>> selector,
        Func<T, TNext, TResult> resultSelector) =>
        new((input, ctx) =>
        {
            var result1 = _parse(input, ctx);
            if (result1 is not SuccessResult<T, TToken> success1)
                return ((FailureResult<T, TToken>)result1).Cast<TResult>();

            var parser2 = selector(success1.Value);
            var result2 = parser2.Parse(success1.Remaining, ctx);
            
            if (result2 is not SuccessResult<TNext, TToken> success2)
            {
                var failure = (FailureResult<TNext, TToken>)result2;
                // エラーをマージ（バックトラック情報の保持）
                var mergedError = success1.Error != null 
                    ? success1.Error.Merge(failure.ErrorValue) 
                    : failure.ErrorValue;
                return ParseResult<TResult, TToken>.Failure(mergedError, failure.Remaining);
            }

            var finalResult = resultSelector(success1.Value, success2.Value);
            var finalError = success1.Error != null && success2.Error != null
                ? success1.Error.Merge(success2.Error)
                : success2.Error ?? success1.Error;
            
            return ParseResult<TResult, TToken>.Success(finalResult, success2.Remaining, finalError);
        }, $"{_name}.SelectMany");

    /// <summary>
    /// Where (filter) - 条件を満たさない場合は失敗します。
    /// </summary>
    public Parser<T, TToken> Where(Func<T, bool> predicate, string? expected = null) =>
        new((input, ctx) =>
        {
            var result = _parse(input, ctx);
            if (result is not SuccessResult<T, TToken> success)
                return result;

            if (predicate(success.Value))
                return result;

            var error = new ParseError(
                input.Position,
                ErrorMessage.Expected(expected ?? "条件を満たす値"));
            return ParseResult<T, TToken>.Failure(error, input);
        }, $"{_name}.Where");

    #endregion
}

/// <summary>
/// 文字列ヘルパー
/// </summary>
internal static class StringExtensions
{
    public static string Truncate(this string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.Length <= maxLength ? str : str[..maxLength] + "...";
    }
}
