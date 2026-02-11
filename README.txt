================================================================================
                         ClaudeParser - パーサーコンビネーターライブラリ
================================================================================

HaskellのParsecに触発されたC#向けパーサーコンビネーターライブラリです。
LINQ/クエリ式構文をサポートし、文字列・バイト列・任意のストリームに対応しています。

================================================================================
                                    目次
================================================================================

1. 概要
2. 基本的な使い方
3. コンビネーター一覧
4. LINQ/クエリ式構文
5. 演算子優先順位パーサー（Text.Parsec.Expr相当）
6. バイナリパーサー
7. エラー処理とトレース機能
8. サンプルパーサー
9. 陥りやすい罠と対処法
10. APIリファレンス

================================================================================
1. 概要
================================================================================

ClaudeParserは、宣言的にパーサーを組み立てることができるコンビネーターライブラリです。

主な特徴：
- HaskellのParsec互換のセマンティクス
- LINQ/クエリ式サポート
- 入力の抽象化（文字列、バイト配列、任意のリスト）
- Parsec Exprに相当する演算子優先順位パーサー
- 分かりやすいエラーメッセージ
- デバッグ用トレース機能

================================================================================
2. 基本的な使い方
================================================================================

■ 最も簡単な例

    using ClaudeParser.Core;
    using ClaudeParser.Combinators;

    // 整数をパースする
    var parser = CharParsers.Integer;
    var result = parser.Parse(new StringInputStream("123"));
    
    if (result.IsSuccess)
    {
        var success = (SuccessResult<long, char>)result;
        Console.WriteLine(success.Value);  // 123
    }

■ 複数のパーサーを組み合わせる

    // 「整数 + 整数」をパースして結果を計算する
    var parser =
        from a in CharParsers.Integer
        from _ in CharParsers.String("+")
        from b in CharParsers.Integer
        select a + b;
    
    var result = parser.Parse(new StringInputStream("10+20"));
    // 結果: 30

■ 選択と繰り返し

    // 「cat」または「dog」をパースする
    var animal = CharParsers.String("cat").Or(CharParsers.String("dog"));
    
    // 0回以上の繰り返し
    var digits = CharParsers.Digit.Many();
    
    // 1回以上の繰り返し
    var word = CharParsers.Letter.Many1();

================================================================================
3. コンビネーター一覧
================================================================================

■ 基本パーサー

    Parsers.Return(value)       - 常に成功し、指定した値を返す
    Parsers.Fail<T>(message)    - 常に失敗する
    Parsers.Satisfy(predicate)  - 条件を満たす1トークンをパース

■ 選択コンビネーター

    p1.Or(p2)                   - p1を試し、失敗（入力未消費）ならp2を試す
    p.Try()                     - 失敗時に入力位置を復元（バックトラック有効化）
    Parsers.Choice(p1, p2, ...) - 複数のパーサーから最初に成功したものを採用

■ シーケンスコンビネーター

    p1.Then(p2)                 - p1の後にp2を実行、p2の結果を返す
    p1.ThenSkip(p2)             - p1の後にp2を実行、p1の結果を返す
    Parsers.Between(open, close, p)  - openとcloseで囲まれたpをパース

■ 繰り返しコンビネーター

    p.Many()                    - 0回以上の繰り返し
    p.Many1()                   - 1回以上の繰り返し
    p.Count(n)                  - 正確にn回の繰り返し
    p.SepBy(sep)                - sepで区切られた0回以上の繰り返し
    p.SepBy1(sep)               - sepで区切られた1回以上の繰り返し
    p.EndBy(sep)                - 各要素の後にsepが来る形式
    p.ManyTill(end)             - endがマッチするまで繰り返す

■ チェインコンビネーター（式パーサー用）

    p.ChainLeft(op, default)    - 左結合の演算子チェイン
    p.ChainRight(op, default)   - 右結合の演算子チェイン

■ オプショナルコンビネーター

    p.Optional()                - 失敗してもOK (Option<T>を返す)
    p.OptionalOr(default)       - 失敗時にデフォルト値を返す
    p.OptionalValue()           - 失敗時にnullを返す (T? を返す)

■ ユーティリティ

    p.Select(f)                 - 結果を変換
    p.Where(predicate)          - 結果をフィルタリング
    p.Named(name)               - パーサーに名前を付ける（エラーメッセージ用）
    p.WithExpected(msg)         - 期待値メッセージをカスタマイズ
    p.Lexeme()                  - 後続の空白をスキップ
    Parsers.Lazy(factory)       - 遅延評価パーサー（再帰定義用）

================================================================================
4. LINQ/クエリ式構文
================================================================================

ClaudeParserはLINQのSelect、SelectMany、Whereをサポートしています。
これにより、クエリ式構文で直感的にパーサーを記述できます。

■ シーケンシャルパース

    // 従来の書き方
    var parser1 = CharParsers.String("Name:")
        .Then(CharParsers.TakeWhile(c => c != '\n'))
        .Select(name => name.Trim());
    
    // クエリ式構文
    var parser2 =
        from _ in CharParsers.String("Name:")
        from name in CharParsers.TakeWhile(c => c != '\n')
        select name.Trim();

■ 複数の値を組み合わせる

    // 座標「(x, y)」をパースする
    var coordinate =
        from _ in CharParsers.Char('(')
        from x in CharParsers.Integer.Lexeme()  // 後続の空白をスキップ
        from __ in CharParsers.Char(',').Lexeme()
        from y in CharParsers.Integer.Lexeme()
        from ___ in CharParsers.Char(')')
        select (X: x, Y: y);

■ 条件フィルタリング（where句）

    // 1～100の範囲の整数のみ受け付ける
    var boundedInt =
        from n in CharParsers.Integer
        where n >= 1 && n <= 100
        select n;

================================================================================
5. 演算子優先順位パーサー（Text.Parsec.Expr相当）
================================================================================

HaskellのText.Parsec.Exprモジュールに相当する機能を提供しています。
演算子の優先順位と結合規則を宣言的に定義できます。

■ 基本的な使い方

    using ClaudeParser.Combinators;

    // 項（数値または括弧で囲まれた式）を定義
    Parser<double, char>? exprRef = null;
    var term = CharParsers.Double.Lexeme()
        .Or(Parsers.Lazy(() => CharParsers.Parens(exprRef!)));

    // 演算子テーブルを定義（優先順位の高い順）
    var table = new OperatorTable<double, char>
    {
        // 最高優先順位: 単項マイナス
        { PrefixOperator.Create(CharParsers.Symbol("-"), x => -x) },
        
        // 次の優先順位: 乗算・除算（左結合）
        { InfixOperator.CreateLeft(CharParsers.Symbol("*"), (a, b) => a * b),
          InfixOperator.CreateLeft(CharParsers.Symbol("/"), (a, b) => a / b) },
        
        // 最低優先順位: 加算・減算（左結合）
        { InfixOperator.CreateLeft(CharParsers.Symbol("+"), (a, b) => a + b),
          InfixOperator.CreateLeft(CharParsers.Symbol("-"), (a, b) => a - b) }
    };

    // 式パーサーを構築
    var expr = ExpressionParser.Build(table, term);
    exprRef = expr;

    // 使用例
    expr.Parse(new StringInputStream("1 + 2 * 3"));     // 7.0
    expr.Parse(new StringInputStream("(1 + 2) * 3"));   // 9.0
    expr.Parse(new StringInputStream("-5 + 3"));        // -2.0

■ 演算子の種類

    InfixOperator.CreateLeft(op, func)   - 左結合の二項演算子
    InfixOperator.CreateRight(op, func)  - 右結合の二項演算子
    InfixOperator.CreateNone(op, func)   - 非結合の二項演算子
    PrefixOperator.Create(op, func)      - 前置単項演算子
    PostfixOperator.Create(op, func)     - 後置単項演算子

■ 右結合演算子の例（べき乗）

    var powerOp = InfixOperator.CreateRight(
        CharParsers.Symbol("^"),
        (a, b) => Math.Pow(a, b));
    
    // 2^3^4 は 2^(3^4) = 2^81 として評価される

================================================================================
6. バイナリパーサー
================================================================================

バイト配列やバイナリストリームをパースするための機能を提供しています。

■ 基本的なバイナリパーサー

    using ClaudeParser.Combinators;

    // 符号なし整数
    BinaryParsers.UInt8              // 1バイト
    BinaryParsers.UInt16BE           // 2バイト、ビッグエンディアン
    BinaryParsers.UInt16LE           // 2バイト、リトルエンディアン
    BinaryParsers.UInt32BE           // 4バイト、ビッグエンディアン
    BinaryParsers.UInt32LE           // 4バイト、リトルエンディアン

    // 符号付き整数
    BinaryParsers.Int8
    BinaryParsers.Int16BE / Int16LE
    BinaryParsers.Int32BE / Int32LE

    // 可変長数量（MIDIで使用される形式）
    BinaryParsers.VariableLengthQuantity

    // バイト列
    BinaryParsers.Take(n)            // n バイトを読み取る
    BinaryParsers.Bytes(pattern)     // 特定のバイト列にマッチ

■ 使用例：簡単なバイナリフォーマット

    // ヘッダー: マジック(4バイト) + バージョン(2バイト) + レコード数(4バイト)
    var header =
        from magic in BinaryParsers.Bytes(new byte[] { 0x89, 0x50, 0x4E, 0x47 })
        from version in BinaryParsers.UInt16BE
        from count in BinaryParsers.UInt32BE
        select (Version: version, Count: count);
    
    var input = new ByteInputStream(data);
    var result = header.Parse(input);

================================================================================
7. エラー処理とトレース機能
================================================================================

■ エラーメッセージ

パース失敗時には、位置情報と期待されるトークンを含む詳細なエラーメッセージが
生成されます。

    var parser = CharParsers.Integer;
    var result = parser.Parse(new StringInputStream("abc"));
    
    if (!result.IsSuccess)
    {
        var failure = (FailureResult<long, char>)result;
        Console.WriteLine(failure.Error.FormatVerbose());
        // 出力例:
        // パースエラー at line 1, col 1
        //   期待: 数字
        //   検出: 'a'
    }

■ カスタムエラーメッセージ

    var positiveInt =
        from n in CharParsers.Integer
        where n > 0
        select n;
    
    // エラーメッセージをカスタマイズ
    var betterParser = positiveInt.WithExpected("正の整数");

■ トレース機能

デバッグ時にパーサーの実行過程を追跡できます。

    // トレースを有効にしてパース
    var context = ParseContext.WithTracing();
    var parser = CharParsers.String("hello").Named("HelloParser");
    
    parser.Parse(new StringInputStream("hello"), context);
    
    // トレースを出力
    Console.WriteLine(context.Trace.ToReport());
    // 出力例:
    // ✓ HelloParser @ <input>:1:1 -> <input>:1:6 (0.02ms)

■ ParseException

GetValueOrThrow()を使うと、失敗時に例外をスローできます。

    try
    {
        var value = parser.Parse(input).GetValueOrThrow();
    }
    catch (ParseException ex)
    {
        Console.WriteLine(ex.Message);  // パースエラーの詳細
    }

================================================================================
8. サンプルパーサー
================================================================================

■ CSVパーサー（ClaudeParser.Examples.CsvParser）

    var csv = CsvParser.Parse("name,age\nAlice,30\nBob,25");
    // csv.Headers: ["name", "age"]
    // csv.Rows: [["Alice", "30"], ["Bob", "25"]]

■ 四則演算パーサー（ClaudeParser.Examples.CalculatorParser）

    var result = CalculatorParser.Evaluate("(1 + 2) * 3 + 4 / 2");
    // result: 11.0

■ JSONパーサー（ClaudeParser.Examples.JsonParser）

    var json = JsonParser.Parse("{\"name\":\"Alice\",\"age\":30}");
    // json: JsonObject containing name and age

■ 正規表現パーサー（ClaudeParser.Examples.RegexParser）

    // 正規表現からパーサーを動的に生成
    var match = RegexParser.Match("[a-z]+", "hello world");
    // match: "hello"
    
    var allMatches = RegexParser.FindAll("\\d+", "a1b23c456");
    // allMatches: ["1", "23", "456"]

■ MIDIパーサー（ClaudeParser.Examples.MidiParser）

    var midi = MidiParser.Parse(midiFileBytes);
    // MIDIファイルの構造を解析

================================================================================
9. 陥りやすい罠と対処法
================================================================================

★★★ 重要: パーサーコンビネーターを使う際に特に注意が必要な点 ★★★

--------------------------------------------------------------------------------
■ 罠1: 左再帰による無限ループ
--------------------------------------------------------------------------------

【問題】
以下のような左再帰的な文法を直接実装すると、無限ループに陥ります：

    // 危険! 無限ループ!
    Parser<int, char>? expr = null;
    expr = (
        from e in Parsers.Lazy(() => expr!)      // 自分自身を最初に呼ぶ
        from _ in CharParsers.Char('+')
        from n in CharParsers.Integer
        select e + n
    ).Or(CharParsers.Integer);

この定義では、exprをパースしようとすると、まずexprをパースしようとし、
さらにexprをパースしようとし...という無限再帰が発生します。

【解決策1】ChainLeftを使う

    // 安全: ChainLeftで左結合の演算子チェインを処理
    var addOp = CharParsers.Char('+').Select<char, Func<long, long, long>>(_ => (a, b) => a + b);
    var expr = CharParsers.Integer.ChainLeft(addOp, 0);

【解決策2】ExpressionParserを使う

    var table = new OperatorTable<long, char>
    {
        { InfixOperator.CreateLeft(CharParsers.Char('+'), (a, b) => a + b) }
    };
    var expr = ExpressionParser.Build(table, CharParsers.Integer);

【解決策3】文法を右再帰に書き換える

    // 右再帰に変換（E → n (+ E)?）
    Parser<long, char>? expr = null;
    expr = 
        from n in CharParsers.Integer
        from rest in (
            from _ in CharParsers.Char('+')
            from e in Parsers.Lazy(() => expr!)
            select e
        ).OptionalOr(0)
        select n + rest;

--------------------------------------------------------------------------------
■ 罠2: Tryなしのバックトラック失敗
--------------------------------------------------------------------------------

【問題】
Or演算子は、最初のパーサーが入力を消費してから失敗した場合、
バックトラック（2番目のパーサーを試すこと）をしません。

    // 問題のあるコード
    var parser = CharParsers.String("abc").Or(CharParsers.String("abd"));
    
    var result = parser.Parse(new StringInputStream("abd"));
    // 失敗! "abc"が"ab"を消費してから'd'で失敗するが、
    // バックトラックしないため"abd"を試さない

【解決策】Tryを使ってバックトラックを有効にする

    var parser = CharParsers.String("abc").Try().Or(CharParsers.String("abd"));
    
    var result = parser.Parse(new StringInputStream("abd"));
    // 成功! Try()により"abc"が失敗したら入力位置が復元される

【When to use Try】
- 複数の選択肢が共通のプレフィックスを持つ場合
- 先読みが必要な場合

【注意】
Try()はパフォーマンスに影響するため、必要な場所でのみ使用してください。
可能であれば、共通プレフィックスをファクタリングしましょう：

    // より効率的な書き方
    var parser = 
        from prefix in CharParsers.String("ab")
        from suffix in CharParsers.Char('c').Or(CharParsers.Char('d'))
        select prefix + suffix;

--------------------------------------------------------------------------------
■ 罠3: ManyとSatisfyの危険な組み合わせ
--------------------------------------------------------------------------------

【問題】
Manyは「0回以上」の繰り返しを表すため、内部パーサーが何も消費せずに
成功する場合、無限ループになります。

    // 危険! 無限ループ!
    var parser = CharParsers.Spaces.Many();  // Spacesは0個の空白でも成功する

【解決策】入力を必ず消費するパーサーを使う

    // 安全: Many1は1回以上の空白を要求
    var parser = CharParsers.Char(' ').Many1();
    
    // または、条件を厳しくする
    var parser = Parsers.Satisfy<char>(char.IsWhiteSpace).Many();

--------------------------------------------------------------------------------
■ 罠4: Lazyを使わない再帰定義
--------------------------------------------------------------------------------

【問題】
再帰的なパーサーを定義する際、Lazyを使わないと
パーサー構築時点でnull参照エラーが発生します。

    // エラー! expr はまだ null
    Parser<long, char>? expr = null;
    var term = CharParsers.Integer.Or(CharParsers.Parens(expr!));  // NullReferenceException
    expr = term;

【解決策】Parsers.Lazyで遅延評価する

    Parser<long, char>? expr = null;
    var term = CharParsers.Integer.Or(
        Parsers.Lazy(() => CharParsers.Parens(expr!))  // ラムダにより遅延評価
    );
    expr = term;

--------------------------------------------------------------------------------
■ 罠5: 空白処理の忘れ
--------------------------------------------------------------------------------

【問題】
トークン間の空白を適切に処理しないと、パースが失敗します。

    // 失敗する可能性がある
    var parser = CharParsers.String("var").Then(CharParsers.Identifier);
    parser.Parse(new StringInputStream("var x"));  // 失敗! "var"と"x"の間の空白

【解決策】Lexeme()を使う

    // Lexeme()は後続の空白を自動的にスキップ
    var parser = CharParsers.String("var").Lexeme().Then(CharParsers.Identifier);
    parser.Parse(new StringInputStream("var x"));  // 成功: "x"

【トークナイザースタイル】

    // Symbol関数（文字列 + 空白スキップ）
    var keyword = CharParsers.Symbol("var");
    var parser = keyword.Then(CharParsers.Identifier.Lexeme());

--------------------------------------------------------------------------------
■ 罠6: パーサーの再利用による予期しない動作
--------------------------------------------------------------------------------

【問題】
状態を持つパーサーを複数回使用すると、予期しない動作が発生する
可能性があります（ClaudeParserでは基本的に問題ないが、注意が必要）。

【ベストプラクティス】
パーサーは常に純粋（副作用なし）であるべきです。
パース結果の処理は、Selectの中か、パース完了後に行いましょう。

    // 良い例: Selectでの変換
    var parser = CharParsers.Integer.Select(n => n * 2);
    
    // 避けるべき例: 外部状態の変更
    var count = 0;
    var parser = CharParsers.Integer.Select(n => {
        count++;  // 副作用!
        return n;
    });

--------------------------------------------------------------------------------
■ 罠7: EndOfInputのチェック忘れ
--------------------------------------------------------------------------------

【問題】
パーサーが入力を部分的にしか消費せず、残りを無視してしまう場合があります。

    var parser = CharParsers.Integer;
    var result = parser.Parse(new StringInputStream("123abc"));
    // 成功するが、"abc"が残っている

【解決策】EndOfInputでチェック

    var parser = CharParsers.Integer.ThenSkip(CharParsers.EndOfInput);
    var result = parser.Parse(new StringInputStream("123abc"));
    // 失敗: 期待=EOF, 検出='a'

================================================================================
10. APIリファレンス
================================================================================

■ 名前空間

    ClaudeParser.Core        - 基本型（Parser, ParseResult, IInputStream等）
    ClaudeParser.Combinators - パーサーコンビネーター

■ 主要な型

    Parser<T, TToken>           - パーサーの中心型
    IInputStream<TToken>        - 入力ストリームのインターフェース
    StringInputStream           - 文字列入力
    ByteInputStream             - バイト配列入力
    ListInputStream<T>          - 任意のリスト入力
    ParseResult<T, TToken>      - パース結果（Success/Failure）
    ParseError                  - エラー情報
    ParseContext                - パースコンテキスト（トレース等）
    OperatorTable<T, TToken>    - 演算子優先順位テーブル

■ 主要なメソッド

    parser.Parse(input)                    - パースを実行
    parser.Parse(input, context)           - コンテキスト付きでパースを実行
    result.IsSuccess                       - 成功かどうか
    result.GetValueOrThrow()               - 値を取得、失敗時は例外
    result.Match(onSuccess, onFailure)     - パターンマッチング

================================================================================
                                    以上
================================================================================
