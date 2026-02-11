namespace ClaudeParser.Core;

/// <summary>
/// 基本的なパーサーコンビネーター。
/// 
/// このクラスはHaskellのParsecに相当するパーサーコンビネーターを提供します。
/// パーサーコンビネーターは、小さなパーサーを組み合わせて複雑なパーサーを構築する
/// 関数型プログラミングのデザインパターンです。
/// </summary>
public static class Parsers
{
    #region 基本パーサー

    /// <summary>
    /// 常に成功し、指定した値を返すパーサー（Parsecのpure/return）。
    /// 
    /// <para><b>成功条件:</b> 常に成功します。</para>
    /// <para><b>失敗条件:</b> なし（失敗しません）。</para>
    /// <para><b>入力消費:</b> 入力を一切消費しません。</para>
    /// 
    /// <example>
    /// <code>
    /// var parser = Parsers.Return&lt;int, char&gt;(42);
    /// var result = parser.Parse(new StringInputStream("anything"));
    /// // result.IsSuccess == true, Value == 42, 入力位置は変わらない
    /// </code>
    /// </example>
    /// </summary>
    public static Parser<T, TToken> Return<T, TToken>(T value) =>
        new((input, _) => ParseResult<T, TToken>.Success(value, input), $"Return({value})");

    /// <summary>
    /// 常に失敗するパーサー。
    /// 
    /// <para><b>成功条件:</b> なし（成功しません）。</para>
    /// <para><b>失敗条件:</b> 常に失敗します。</para>
    /// <para><b>入力消費:</b> 入力を一切消費しません。</para>
    /// </summary>
    public static Parser<T, TToken> Fail<T, TToken>(string message) =>
        new((input, _) => ParseResult<T, TToken>.Failure(
            new ParseError(input.Position, ErrorMessage.Message(message)), input), 
            $"Fail({message})");

    /// <summary>
    /// 入力を消費せずに現在位置を返すパーサー。
    /// 
    /// <para><b>成功条件:</b> 常に成功します。</para>
    /// <para><b>失敗条件:</b> なし。</para>
    /// <para><b>入力消費:</b> 入力を一切消費しません。</para>
    /// </summary>
    public static Parser<Position, TToken> GetPosition<TToken>() =>
        new((input, _) => ParseResult<Position, TToken>.Success(input.Position, input), "GetPosition");

    /// <summary>
    /// 入力の終端であることを確認するパーサー。
    /// 
    /// <para><b>成功条件:</b> 入力が終端（EOF）に達している場合。</para>
    /// <para><b>失敗条件:</b> まだ読み取れるトークンがある場合。</para>
    /// <para><b>入力消費:</b> 入力を一切消費しません。</para>
    /// 
    /// <example>
    /// <code>
    /// var parser = CharParsers.Integer.ThenSkip(Parsers.Eof&lt;char&gt;());
    /// parser.Parse(new StringInputStream("123"));    // 成功
    /// parser.Parse(new StringInputStream("123abc")); // 失敗（"abc"が残っている）
    /// </code>
    /// </example>
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
    /// 任意の1トークンを読み取るパーサー。
    /// 
    /// <para><b>成功条件:</b> 入力に少なくとも1トークンが残っている場合。</para>
    /// <para><b>失敗条件:</b> 入力が終端（EOF）の場合。</para>
    /// <para><b>入力消費:</b> 成功時に1トークンを消費します。失敗時は消費しません。</para>
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
    /// 条件を満たす1トークンを読み取るパーサー。
    /// 
    /// <para><b>成功条件:</b> 
    /// 1. 入力に少なくとも1トークンが残っている
    /// 2. そのトークンが predicate を満たす（trueを返す）
    /// </para>
    /// <para><b>失敗条件:</b>
    /// 1. 入力が終端（EOF）である
    /// 2. 現在のトークンが predicate を満たさない
    /// </para>
    /// <para><b>入力消費:</b> 成功時に1トークンを消費。失敗時は消費しません。</para>
    /// 
    /// <example>
    /// <code>
    /// var digit = Parsers.Satisfy&lt;char&gt;(char.IsDigit, "数字");
    /// digit.Parse(new StringInputStream("5abc")); // 成功: '5', 残り"abc"
    /// digit.Parse(new StringInputStream("abc"));  // 失敗: 入力位置は変わらない
    /// </code>
    /// </example>
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
    /// HaskellのParsecにおける <c>&lt;|&gt;</c> 演算子に相当します。
    /// </summary>
    /// <remarks>
    /// <para><b>成功条件:</b></para>
    /// <list type="bullet">
    ///   <item><description><paramref name="first"/> が成功した場合、その結果を返します</description></item>
    ///   <item><description><paramref name="first"/> が失敗し、かつ入力を消費していない場合、
    ///   <paramref name="second"/> を試し、その結果を返します</description></item>
    /// </list>
    /// 
    /// <para><b>失敗条件:</b></para>
    /// <list type="bullet">
    ///   <item><description><paramref name="first"/> が入力を消費してから失敗した場合、
    ///   <paramref name="second"/> は試されず、<paramref name="first"/> の失敗がそのまま返されます</description></item>
    ///   <item><description>両方のパーサーが失敗した場合、両方のエラーがマージされて返されます</description></item>
    /// </list>
    /// 
    /// <para><b>入力消費:</b>
    /// 成功したパーサーが消費した分だけ入力が進みます。
    /// <paramref name="first"/> が入力を消費せずに失敗した場合のみ <paramref name="second"/> が試されます（バックトラック）。
    /// </para>
    /// 
    /// <para><b>使用例:</b></para>
    /// <code>
    /// // "true" または "false" をパース
    /// var boolParser = String("true").Or(String("false"));
    /// 
    /// // 注意: "true" で始まるが途中で失敗した場合
    /// // String("truthy").Or(String("false"))
    /// // "true" の4文字を消費後に失敗するため、"false" は試されない
    /// // この場合は Try を使用: Try(String("truthy")).Or(String("false"))
    /// </code>
    /// 
    /// <para><b>エラーメッセージ:</b>
    /// 両方失敗した場合、「'true' または 'false' が期待されます」のように
    /// 両方の期待値がマージされたエラーメッセージが生成されます。
    /// </para>
    /// </remarks>
    /// <typeparam name="T">パース結果の型</typeparam>
    /// <typeparam name="TToken">トークンの型</typeparam>
    /// <param name="first">最初に試すパーサー</param>
    /// <param name="second">最初が失敗した場合に試すパーサー</param>
    /// <returns>いずれかのパーサーが成功した結果</returns>
    /// <seealso cref="Try{T, TToken}"/>
    /// <seealso cref="Choice{T, TToken}(Parser{T, TToken}[])"/>
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
                    ? failure1.ErrorValue.Merge(success.Error) 
                    : failure1.ErrorValue;
                return ParseResult<T, TToken>.Success(success.Value, success.Remaining, mergedError);
            }

            var failure2 = (FailureResult<T, TToken>)result2;
            // 両方失敗した場合、エラーをマージ
            return ParseResult<T, TToken>.Failure(
                failure1.ErrorValue.Merge(failure2.ErrorValue), failure2.Remaining);
        }, $"({first.Name} | {second.Name})");

    /// <summary>
    /// 複数のパーサーから最初に成功するものを選択します。
    /// </summary>
    /// <remarks>
    /// <para>内部的には <see cref="Or{T, TToken}"/> を左結合で適用します。
    /// つまり <c>Choice(a, b, c)</c> は <c>a.Or(b).Or(c)</c> と等価です。</para>
    /// <para>Or と同様、途中のパーサーが入力を消費して失敗した場合、
    /// それ以降のパーサーは試されません。</para>
    /// </remarks>
    public static Parser<T, TToken> Choice<T, TToken>(params Parser<T, TToken>[] parsers) =>
        Choice((IEnumerable<Parser<T, TToken>>)parsers);

    public static Parser<T, TToken> Choice<T, TToken>(IEnumerable<Parser<T, TToken>> parsers) =>
        parsers.Aggregate((a, b) => a.Or(b)).Named("Choice");

    /// <summary>
    /// バックトラックを許可するパーサー（Parsecの try/attempt）。
    /// 失敗しても入力を消費しないようにします。
    /// </summary>
    /// <remarks>
    /// <para><b>成功条件:</b>
    /// 内部パーサーが成功した場合、その結果をそのまま返します。
    /// </para>
    /// 
    /// <para><b>失敗条件:</b>
    /// 内部パーサーが失敗した場合、エラー情報は保持しつつ入力位置を元に戻します。
    /// </para>
    /// 
    /// <para><b>入力消費:</b>
    /// 成功時: 内部パーサーが消費した分だけ進みます。<br/>
    /// 失敗時: 入力を消費しません（開始位置に戻ります）。
    /// </para>
    /// 
    /// <para><b>使用場面:</b>
    /// Or と組み合わせて、入力を消費した後でも別の選択肢を試したい場合に使用します。
    /// </para>
    /// 
    /// <para><b>使用例:</b></para>
    /// <code>
    /// // "class" キーワードと "classname" 識別子を区別
    /// // 単純な Or だと "class" が "classname" の先頭にマッチして失敗する
    /// var parser = Try(String("classname")).Or(String("class"));
    /// // → "class" で始まる入力でも、"classname" 全体がマッチしなければ
    /// //   バックトラックして "class" を試す
    /// </code>
    /// 
    /// <para><b>注意:</b>
    /// 過度な Try の使用はパフォーマンスに影響を与える可能性があります。
    /// 可能な限り、共通のプレフィックスを括り出すなどの工夫をしてください。
    /// </para>
    /// </remarks>
    /// <typeparam name="T">パース結果の型</typeparam>
    /// <typeparam name="TToken">トークンの型</typeparam>
    /// <param name="parser">バックトラック可能にするパーサー</param>
    /// <returns>失敗時にバックトラックするパーサー</returns>
    /// <seealso cref="Or{T, TToken}"/>
    /// <seealso cref="LookAhead{T, TToken}"/>
    public static Parser<T, TToken> Try<T, TToken>(this Parser<T, TToken> parser) =>
        new((input, ctx) =>
        {
            var result = parser.Parse(input, ctx);
            if (result is FailureResult<T, TToken> failure)
            {
                // 失敗した場合、入力位置を戻す
                return ParseResult<T, TToken>.Failure(failure.ErrorValue, input);
            }
            return result;
        }, $"Try({parser.Name})");

    /// <summary>
    /// パーサーが成功するかどうかを先読みします（正の先読み / positive lookahead）。
    /// 成功しても入力を消費しません。
    /// </summary>
    /// <remarks>
    /// <para><b>成功条件:</b>
    /// 内部パーサーが成功した場合、その値を返しますが入力位置は進めません。
    /// </para>
    /// 
    /// <para><b>失敗条件:</b>
    /// 内部パーサーが失敗した場合、その失敗をそのまま返します。
    /// </para>
    /// 
    /// <para><b>入力消費:</b>
    /// 成功時: 入力を消費しません。結果の値は取得できますが、位置は元のままです。<br/>
    /// 失敗時: 内部パーサーの失敗に依存します（通常は消費しない）。
    /// </para>
    /// 
    /// <para><b>使用場面:</b>
    /// 次に何が来るかを確認してから、実際のパースを行いたい場合に使用します。
    /// 条件分岐やバリデーションに有用です。
    /// </para>
    /// 
    /// <para><b>使用例:</b></para>
    /// <code>
    /// // 次の文字が数字かどうか確認してから処理を分岐
    /// var peekDigit = LookAhead(Digit);
    /// // → 数字があればその値を返すが、入力位置は進まない
    /// 
    /// // 終端の確認
    /// var endCheck = LookAhead(Eof&lt;char&gt;());
    /// // → 入力の終端かどうかを確認（消費はしない）
    /// </code>
    /// </remarks>
    /// <typeparam name="T">パース結果の型</typeparam>
    /// <typeparam name="TToken">トークンの型</typeparam>
    /// <param name="parser">先読みに使用するパーサー</param>
    /// <returns>入力を消費しない先読みパーサー</returns>
    /// <seealso cref="NotFollowedBy{T, TToken}"/>
    /// <seealso cref="Try{T, TToken}"/>
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
    /// パーサーが失敗することを確認します（否定先読み / negative lookahead）。
    /// 指定したパーサーが成功した場合は失敗し、失敗した場合は成功します。
    /// </summary>
    /// <remarks>
    /// <para><b>成功条件:</b>
    /// 内部パーサーが失敗した場合、<see cref="Unit.Value"/> を返して成功します。
    /// </para>
    /// 
    /// <para><b>失敗条件:</b>
    /// 内部パーサーが成功した場合、「{パーサー名}が成功」という予期しない入力として失敗します。
    /// </para>
    /// 
    /// <para><b>入力消費:</b>
    /// 入力を一切消費しません。成功時も失敗時も入力位置は元のままです。
    /// </para>
    /// 
    /// <para><b>使用場面:</b>
    /// 特定のパターンが続かないことを確認したい場合に使用します。
    /// 予約語と識別子の区別や、特定の文字列で終わらないことの確認などに有用です。
    /// </para>
    /// 
    /// <para><b>使用例:</b></para>
    /// <code>
    /// // 予約語 "if" と識別子 "iffy" を区別
    /// // "if" の後に英数字が続かないことを確認
    /// var ifKeyword = String("if").ThenSkip(NotFollowedBy(LetterOrDigit));
    /// // → "if" にマッチするが、"if(" は成功、"iffy" は失敗
    /// 
    /// // コメント終端でないことの確認
    /// var notCommentEnd = NotFollowedBy(String("*/"));
    /// // → "*/" が来るまで任意の文字を読み続ける際に使用
    /// </code>
    /// </remarks>
    /// <typeparam name="T">内部パーサーの結果の型（使用されない）</typeparam>
    /// <typeparam name="TToken">トークンの型</typeparam>
    /// <param name="parser">失敗することを期待するパーサー</param>
    /// <returns>否定先読みパーサー（成功時は <see cref="Unit"/> を返す）</returns>
    /// <seealso cref="LookAhead{T, TToken}"/>
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
    /// パーサーを0回以上繰り返します（Parsecの many に相当）。
    /// </summary>
    /// <remarks>
    /// <para><b>成功条件:</b>
    /// このパーサーは常に成功します。内部パーサーが1回も成功しなかった場合でも、
    /// 空のリストを返して成功します。
    /// </para>
    /// 
    /// <para><b>失敗条件:</b></para>
    /// <list type="bullet">
    ///   <item><description>内部パーサーが入力を消費した後に失敗した場合のみ失敗します</description></item>
    ///   <item><description>内部パーサーが入力を消費せずに成功した場合、無限ループを防ぐためにエラーで失敗します</description></item>
    /// </list>
    /// 
    /// <para><b>入力消費:</b>
    /// 内部パーサーが成功した回数分だけ入力を消費します。
    /// 0回成功（空リスト返却）の場合は入力を消費しません。
    /// </para>
    /// 
    /// <para><b>動作の詳細:</b></para>
    /// <list type="number">
    ///   <item><description>内部パーサーを実行</description></item>
    ///   <item><description>成功した場合、結果をリストに追加して 1. に戻る</description></item>
    ///   <item><description>失敗した場合:
    ///     <list type="bullet">
    ///       <item><description>入力を消費していない → 現在のリストを返して成功</description></item>
    ///       <item><description>入力を消費している → 失敗</description></item>
    ///     </list>
    ///   </description></item>
    /// </list>
    /// 
    /// <para><b>使用例:</b></para>
    /// <code>
    /// // 0個以上の空白
    /// var spaces = Char(' ').Many();  // 「0個の空白」も成功
    /// spaces.Parse("abc")  // → Success([], "abc") - 空リストで成功
    /// spaces.Parse("  x")  // → Success([' ', ' '], "x")
    /// 
    /// // 0個以上の数字
    /// var digits = Digit.Many();
    /// digits.Parse("abc")  // → Success([], "abc") - 数字がなくても成功
    /// digits.Parse("123x") // → Success(['1', '2', '3'], "x")
    /// </code>
    /// 
    /// <para><b>注意: 無限ループ検出</b></para>
    /// <para>
    /// 入力を消費せずに成功するパーサーを Many に渡すと、無限ループになる可能性があります。
    /// これを防ぐため、内部パーサーが入力を消費せずに成功した場合はエラーとなります。
    /// </para>
    /// <code>
    /// // 危険な例（Return は常に入力を消費せずに成功）
    /// Return(1).Many()  // → 無限ループ検出エラー
    /// 
    /// // 危険な例（Optional は常に成功）
    /// Char('a').Optional().Many()  // → 無限ループ検出エラー
    /// </code>
    /// </remarks>
    /// <typeparam name="T">パース結果の要素の型</typeparam>
    /// <typeparam name="TToken">トークンの型</typeparam>
    /// <param name="parser">繰り返すパーサー</param>
    /// <returns>0回以上の繰り返しの結果を格納したリスト</returns>
    /// <seealso cref="Many1{T, TToken}"/>
    /// <seealso cref="Optional{T, TToken}"/>
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
                    lastError = lastError != null ? lastError.Merge(failure.ErrorValue) : failure.ErrorValue;
                    break;
                }
            }

            return ParseResult<IReadOnlyList<T>, TToken>.Success(results, current, lastError);
        }, $"Many({parser.Name})");

    /// <summary>
    /// パーサーを1回以上繰り返します（Parsecの many1 に相当）。
    /// </summary>
    /// <remarks>
    /// <para><b>成功条件:</b>
    /// 内部パーサーが少なくとも1回成功した場合に成功します。
    /// </para>
    /// 
    /// <para><b>失敗条件:</b>
    /// 内部パーサーが1回も成功しなかった場合に失敗します。
    /// これが <see cref="Many{T, TToken}"/> との最大の違いです。
    /// </para>
    /// 
    /// <para><b>入力消費:</b>
    /// 成功時は少なくとも内部パーサー1回分の入力を消費します。
    /// 失敗時は内部パーサーの失敗に依存します。
    /// </para>
    /// 
    /// <para><b>使用例:</b></para>
    /// <code>
    /// // 1個以上の数字（整数の基本パターン）
    /// var digits = Digit.Many1();
    /// digits.Parse("123x") // → Success(['1', '2', '3'], "x")
    /// digits.Parse("abc")  // → Failure（Many と異なり失敗する）
    /// 
    /// // 1個以上の空白（必須の空白）
    /// var spaces1 = Char(' ').Many1();
    /// spaces1.Parse(" x")  // → Success([' '], "x")
    /// spaces1.Parse("x")   // → Failure（空白が必須）
    /// </code>
    /// 
    /// <para><b>Many vs Many1:</b></para>
    /// <code>
    /// // Many: 「あれば繰り返す、なくてもOK」（オプショナル）
    /// Digit.Many().Parse("abc")   // → Success([], "abc")
    /// 
    /// // Many1: 「最低1回は必要、あれば繰り返す」（必須）
    /// Digit.Many1().Parse("abc")  // → Failure
    /// </code>
    /// </remarks>
    /// <typeparam name="T">パース結果の要素の型</typeparam>
    /// <typeparam name="TToken">トークンの型</typeparam>
    /// <param name="parser">繰り返すパーサー</param>
    /// <returns>1回以上の繰り返しの結果を格納したリスト</returns>
    /// <seealso cref="Many{T, TToken}"/>
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
