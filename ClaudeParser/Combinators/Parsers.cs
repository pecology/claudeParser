namespace ClaudeParser.Core;

/// <summary>
/// 基本的なパーサーコンビネーター
/// </summary>
public static class Parsers
{
    #region 基本パーサー

    /// <summary>
    /// 常に成功し、指定した値を返すパーサー（Parsecのpure/return）
    /// </summary>
    public static Parser<T, TToken> Return<T, TToken>(T value) =>
        new((input, _) => ParseResult<T, TToken>.Success(value, input), $"Return({value})");

    /// <summary>
    /// 常に失敗するパーサー
    /// </summary>
    public static Parser<T, TToken> Fail<T, TToken>(string message) =>
        new((input, _) => ParseResult<T, TToken>.Failure(
            new ParseError(input.Position, ErrorMessage.Message(message)), input), 
            $"Fail({message})");

    /// <summary>
    /// 入力を消費せずに現在位置を返すパーサー
    /// </summary>
    public static Parser<Position, TToken> GetPosition<TToken>() =>
        new((input, _) => ParseResult<Position, TToken>.Success(input.Position, input), "GetPosition");

    /// <summary>
    /// 入力の終端であることを確認するパーサー
    /// </summary>
    public static Parser<Unit, TToken> Eof<TToken>() =>
        new((input, _) =>
        {
            if (input.IsAtEnd)
                return ParseResult<Unit, TToken>.Success(Unit.Value, input);
            return ParseResult<Unit, TToken>.Failure(
                new ParseError(input.Position, ErrorMessage.Expected("入力の終端")), input);
        }, "Eof");

    /// <summary>
    /// 任意の1トークンを読み取るパーサー
    /// </summary>
    public static Parser<TToken, TToken> AnyToken<TToken>() =>
        new((input, _) =>
        {
            if (input.IsAtEnd)
                return ParseResult<TToken, TToken>.Failure(
                    new ParseError(input.Position, ErrorMessage.EndOfInput()), input);
            return ParseResult<TToken, TToken>.Success(input.Current!, input.Advance());
        }, "AnyToken");

    /// <summary>
    /// 条件を満たす1トークンを読み取るパーサー
    /// </summary>
    public static Parser<TToken, TToken> Satisfy<TToken>(Func<TToken, bool> predicate, string expected = "条件を満たすトークン") =>
        new((input, _) =>
        {
            if (input.IsAtEnd)
                return ParseResult<TToken, TToken>.Failure(
                    new ParseError(input.Position, ErrorMessage.EndOfInput()), input);
            
            var token = input.Current!;
            if (predicate(token))
                return ParseResult<TToken, TToken>.Success(token, input.Advance());
            
            return ParseResult<TToken, TToken>.Failure(
                new ParseError(input.Position, 
                    new[] { ErrorMessage.Expected(expected), ErrorMessage.Unexpected(token.ToString() ?? "null") }),
                input);
        }, $"Satisfy({expected})");

    #endregion

    #region 選択コンビネーター

    /// <summary>
    /// 最初のパーサーを試し、失敗したら（入力を消費していなければ）次のパーサーを試します。
    /// これはParsecの <|> に相当します。
    /// </summary>
    public static Parser<T, TToken> Or<T, TToken>(this Parser<T, TToken> first, Parser<T, TToken> second) =>
        new((input, ctx) =>
        {
            var result1 = first.Parse(input, ctx);
            if (result1.IsSuccess)
                return result1;

            var failure1 = (FailureResult<T, TToken>)result1;
            
            // 入力を消費していたら、バックトラックしない
            if (!failure1.Remaining.Equals(input))
                return result1;

            var result2 = second.Parse(input, ctx);
            if (result2.IsSuccess)
            {
                // 成功時も、最初の失敗のエラー情報を保持（より良いエラーメッセージのため）
                var success = (SuccessResult<T, TToken>)result2;
                var mergedError = success.Error != null 
                    ? failure1.Error.Merge(success.Error) 
                    : failure1.Error;
                return ParseResult<T, TToken>.Success(success.Value, success.Remaining, mergedError);
            }

            var failure2 = (FailureResult<T, TToken>)result2;
            // 両方失敗した場合、エラーをマージ
            return ParseResult<T, TToken>.Failure(
                failure1.Error.Merge(failure2.Error), failure2.Remaining);
        }, $"({first.Name} | {second.Name})");

    /// <summary>
    /// 複数のパーサーから最初に成功するものを選択します。
    /// </summary>
    public static Parser<T, TToken> Choice<T, TToken>(params Parser<T, TToken>[] parsers) =>
        Choice((IEnumerable<Parser<T, TToken>>)parsers);

    public static Parser<T, TToken> Choice<T, TToken>(IEnumerable<Parser<T, TToken>> parsers) =>
        parsers.Aggregate((a, b) => a.Or(b)).Named("Choice");

    /// <summary>
    /// バックトラックを許可するパーサー（try）。
    /// 失敗しても入力を消費しないようにします。
    /// </summary>
    public static Parser<T, TToken> Try<T, TToken>(this Parser<T, TToken> parser) =>
        new((input, ctx) =>
        {
            var result = parser.Parse(input, ctx);
            if (result is FailureResult<T, TToken> failure)
            {
                // 失敗した場合、入力位置を戻す
                return ParseResult<T, TToken>.Failure(failure.Error, input);
            }
            return result;
        }, $"Try({parser.Name})");

    /// <summary>
    /// パーサーが成功するかどうかを先読みします（入力を消費しない）。
    /// </summary>
    public static Parser<T, TToken> LookAhead<T, TToken>(this Parser<T, TToken> parser) =>
        new((input, ctx) =>
        {
            var result = parser.Parse(input, ctx);
            if (result is SuccessResult<T, TToken> success)
            {
                // 成功しても入力位置を戻す
                return ParseResult<T, TToken>.Success(success.Value, input, success.Error);
            }
            return result;
        }, $"LookAhead({parser.Name})");

    /// <summary>
    /// パーサーが失敗することを確認します（否定先読み）。
    /// </summary>
    public static Parser<Unit, TToken> NotFollowedBy<T, TToken>(this Parser<T, TToken> parser) =>
        new((input, ctx) =>
        {
            var result = parser.Parse(input, ctx);
            if (result.IsSuccess)
            {
                return ParseResult<Unit, TToken>.Failure(
                    new ParseError(input.Position, ErrorMessage.Unexpected($"{parser.Name}が成功")), input);
            }
            return ParseResult<Unit, TToken>.Success(Unit.Value, input);
        }, $"NotFollowedBy({parser.Name})");

    #endregion

    #region 連結コンビネーター

    /// <summary>
    /// 2つのパーサーを連結し、両方の結果をタプルで返します。
    /// </summary>
    public static Parser<(T1, T2), TToken> Then<T1, T2, TToken>(
        this Parser<T1, TToken> first,
        Parser<T2, TToken> second) =>
        from a in first
        from b in second
        select (a, b);

    /// <summary>
    /// 2つのパーサーを連結し、左側の結果のみを返します。
    /// </summary>
    public static Parser<T1, TToken> ThenSkip<T1, T2, TToken>(
        this Parser<T1, TToken> first,
        Parser<T2, TToken> second) =>
        from a in first
        from _ in second
        select a;

    /// <summary>
    /// 2つのパーサーを連結し、右側の結果のみを返します。
    /// </summary>
    public static Parser<T2, TToken> SkipThen<T1, T2, TToken>(
        this Parser<T1, TToken> first,
        Parser<T2, TToken> second) =>
        from _ in first
        from b in second
        select b;

    /// <summary>
    /// 左右の区切りで囲まれたパーサー
    /// </summary>
    public static Parser<T, TToken> Between<TL, TR, T, TToken>(
        Parser<TL, TToken> left,
        Parser<TR, TToken> right,
        Parser<T, TToken> content) =>
        from _ in left
        from c in content
        from __ in right
        select c;

    #endregion

    #region 繰り返しコンビネーター

    /// <summary>
    /// パーサーを0回以上繰り返します（Parsecのmany）
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> Many<T, TToken>(this Parser<T, TToken> parser) =>
        new((input, ctx) =>
        {
            var results = new List<T>();
            var current = input;
            ParseError? lastError = null;

            while (true)
            {
                var result = parser.Parse(current, ctx);
                
                if (result is SuccessResult<T, TToken> success)
                {
                    // 入力を消費していない場合は無限ループを防ぐ
                    if (success.Remaining.Equals(current))
                    {
                        return ParseResult<IReadOnlyList<T>, TToken>.Failure(
                            new ParseError(current.Position, 
                                ErrorMessage.Message($"パーサー '{parser.Name}' が入力を消費せずに成功しました。無限ループを防ぐため中断します。")),
                            current);
                    }
                    results.Add(success.Value);
                    current = success.Remaining;
                    lastError = success.Error != null && lastError != null 
                        ? lastError.Merge(success.Error) 
                        : success.Error ?? lastError;
                }
                else
                {
                    var failure = (FailureResult<T, TToken>)result;
                    // 入力を消費していたら失敗
                    if (!failure.Remaining.Equals(current))
                    {
                        return failure.Cast<IReadOnlyList<T>>();
                    }
                    // 入力を消費していなければ、これまでの結果を返す
                    lastError = lastError != null ? lastError.Merge(failure.Error) : failure.Error;
                    break;
                }
            }

            return ParseResult<IReadOnlyList<T>, TToken>.Success(results, current, lastError);
        }, $"Many({parser.Name})");

    /// <summary>
    /// パーサーを1回以上繰り返します（Parsecのmany1）
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> Many1<T, TToken>(this Parser<T, TToken> parser) =>
        from first in parser
        from rest in parser.Many()
        select (IReadOnlyList<T>)new[] { first }.Concat(rest).ToList();

    /// <summary>
    /// パーサーを指定回数繰り返します
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> Count<T, TToken>(this Parser<T, TToken> parser, int n) =>
        new((input, ctx) =>
        {
            var results = new List<T>(n);
            var current = input;

            for (int i = 0; i < n; i++)
            {
                var result = parser.Parse(current, ctx);
                if (result is not SuccessResult<T, TToken> success)
                {
                    return ((FailureResult<T, TToken>)result).Cast<IReadOnlyList<T>>();
                }
                results.Add(success.Value);
                current = success.Remaining;
            }

            return ParseResult<IReadOnlyList<T>, TToken>.Success(results, current);
        }, $"Count({parser.Name}, {n})");

    /// <summary>
    /// パーサーを0回または1回実行します（Parsecのoptional）
    /// </summary>
    public static Parser<T?, TToken> Optional<T, TToken>(this Parser<T, TToken> parser) where T : class =>
        parser.Select(x => (T?)x).Or(Return<T?, TToken>(null));

    /// <summary>
    /// パーサーを0回または1回実行します（値型用）
    /// </summary>
    public static Parser<T?, TToken> OptionalValue<T, TToken>(this Parser<T, TToken> parser) where T : struct =>
        parser.Select(x => (T?)x).Or(Return<T?, TToken>(null));

    /// <summary>
    /// パーサーを0回または1回実行し、デフォルト値を返します
    /// </summary>
    public static Parser<T, TToken> OptionalOr<T, TToken>(this Parser<T, TToken> parser, T defaultValue) =>
        parser.Or(Return<T, TToken>(defaultValue));

    /// <summary>
    /// 区切り文字で区切られた要素を0回以上パースします（Parsecのsepby）
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> SepBy<T, TSep, TToken>(
        this Parser<T, TToken> parser,
        Parser<TSep, TToken> separator) =>
        parser.SepBy1(separator)
            .Or(Return<IReadOnlyList<T>, TToken>(Array.Empty<T>()));

    /// <summary>
    /// 区切り文字で区切られた要素を1回以上パースします（Parsecのsepby1）
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> SepBy1<T, TSep, TToken>(
        this Parser<T, TToken> parser,
        Parser<TSep, TToken> separator) =>
        from first in parser
        from rest in separator.SkipThen(parser).Many()
        select (IReadOnlyList<T>)new[] { first }.Concat(rest).ToList();

    /// <summary>
    /// 区切り文字で区切られた要素をパースし、末尾の区切り文字も許可します
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> SepEndBy<T, TSep, TToken>(
        this Parser<T, TToken> parser,
        Parser<TSep, TToken> separator) =>
        from items in parser.SepBy(separator)
        from _ in separator.OptionalOr(default(TSep)!)
        select items;

    /// <summary>
    /// 終端記号で終わる要素を0回以上パースします（Parsecのendby）
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> EndBy<T, TEnd, TToken>(
        this Parser<T, TToken> parser,
        Parser<TEnd, TToken> end) =>
        parser.ThenSkip(end).Many();

    /// <summary>
    /// 終端記号で終わる要素を1回以上パースします
    /// </summary>
    public static Parser<IReadOnlyList<T>, TToken> EndBy1<T, TEnd, TToken>(
        this Parser<T, TToken> parser,
        Parser<TEnd, TToken> end) =>
        parser.ThenSkip(end).Many1();

    /// <summary>
    /// 左結合の二項演算子のパース（1+2+3 → ((1+2)+3)）
    /// </summary>
    public static Parser<T, TToken> ChainLeft<T, TToken>(
        this Parser<T, TToken> term,
        Parser<Func<T, T, T>, TToken> op) =>
        from first in term
        from rest in (from f in op from t in term select (f, t)).Many()
        select rest.Aggregate(first, (acc, pair) => pair.f(acc, pair.t));

    /// <summary>
    /// 右結合の二項演算子のパース（1^2^3 → (1^(2^3))）
    /// </summary>
    public static Parser<T, TToken> ChainRight<T, TToken>(
        this Parser<T, TToken> term,
        Parser<Func<T, T, T>, TToken> op)
    {
        Parser<T, TToken>? self = null;
        self = new Parser<T, TToken>((input, ctx) =>
        {
            var termResult = term.Parse(input, ctx);
            if (termResult is not SuccessResult<T, TToken> first)
                return termResult;

            var opResult = op.Parse(first.Remaining, ctx);
            if (opResult is not SuccessResult<Func<T, T, T>, TToken> opSuccess)
            {
                // 演算子がないので、項だけを返す
                return termResult;
            }

            var rightResult = self!.Parse(opSuccess.Remaining, ctx);
            if (rightResult is not SuccessResult<T, TToken> rightSuccess)
                return rightResult;

            return ParseResult<T, TToken>.Success(
                opSuccess.Value(first.Value, rightSuccess.Value),
                rightSuccess.Remaining,
                rightSuccess.Error);
        }, $"ChainRight({term.Name})");
        
        return self;
    }

    #endregion

    #region ユーティリティ

    /// <summary>
    /// 遅延評価パーサー（相互再帰を可能にする）
    /// </summary>
    public static Parser<T, TToken> Lazy<T, TToken>(Func<Parser<T, TToken>> parserFactory) =>
        new((input, ctx) => parserFactory().Parse(input, ctx), "Lazy");

    /// <summary>
    /// パーサーの結果を無視してUnitを返します
    /// </summary>
    public static Parser<Unit, TToken> Ignore<T, TToken>(this Parser<T, TToken> parser) =>
        parser.Select(_ => Unit.Value);

    /// <summary>
    /// デバッグ用：パーサーの実行前後に処理を挟みます
    /// </summary>
    public static Parser<T, TToken> Debug<T, TToken>(
        this Parser<T, TToken> parser,
        Action<IInputStream<TToken>>? before = null,
        Action<ParseResult<T, TToken>>? after = null) =>
        new((input, ctx) =>
        {
            before?.Invoke(input);
            var result = parser.Parse(input, ctx);
            after?.Invoke(result);
            return result;
        }, parser.Name);

    #endregion
}

/// <summary>
/// ユニット型（値を持たない型）
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static Unit Value => default;
    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";
}
