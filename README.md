# ClaudeParser - パーサーコンビネーターライブラリ

HaskellのParsecに触発されたC#向けパーサーコンビネーターライブラリです。
LINQ/クエリ式構文をサポートし、文字列・バイト列・任意のストリームに対応しています。

## 🌐 ウェブデモ

[GitHub Pagesでデモを試す](https://pecology.github.io/claudeParser/)

各種パーサー（電卓、CSV、JSON、正規表現）をブラウザ上で試せます。

## 目次

1. [概要](#1-概要)
2. [基本的な使い方](#2-基本的な使い方)
3. [コンビネーター一覧](#3-コンビネーター一覧)
4. [LINQ/クエリ式構文](#4-linqクエリ式構文)
5. [演算子優先順位パーサー](#5-演算子優先順位パーサー-textparsecexpr相当)
6. [バイナリパーサー](#6-バイナリパーサー)
7. [エラー処理とトレース機能](#7-エラー処理とトレース機能)
8. [サンプルパーサー](#8-サンプルパーサー)
9. [陥りやすい罠と対処法](#9-陥りやすい罠と対処法)
10. [APIリファレンス](#10-apiリファレンス)
11. [GitHub Pagesへのデプロイ](#11-github-pagesへのデプロイ)

---

## 1. 概要

ClaudeParserは、宣言的にパーサーを組み立てることができるコンビネーターライブラリです。

### 主な特徴

- HaskellのParsec互換のセマンティクス
- LINQ/クエリ式サポート
- 入力の抽象化（文字列、バイト配列、任意のリスト）
- Parsec Exprに相当する演算子優先順位パーサー
- 分かりやすいエラーメッセージ
- デバッグ用トレース機能

---

## 2. 基本的な使い方

### 最も簡単な例

```csharp
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
```

### 複数のパーサーを組み合わせる

```csharp
// 「整数 + 整数」をパースして結果を計算する
var parser =
    from a in CharParsers.Integer
    from _ in CharParsers.String("+")
    from b in CharParsers.Integer
    select a + b;

var result = parser.Parse(new StringInputStream("10+20"));
// 結果: 30
```

### 選択と繰り返し

```csharp
// 「cat」または「dog」をパースする
var animal = CharParsers.String("cat").Or(CharParsers.String("dog"));

// 0回以上の繰り返し
var digits = CharParsers.Digit.Many();

// 1回以上の繰り返し
var word = CharParsers.Letter.Many1();
```

---

## 3. コンビネーター一覧

### 基本パーサー

| パーサー | 説明 |
|---------|------|
| `Parsers.Return(value)` | 常に成功し、指定した値を返す |
| `Parsers.Fail<T>(message)` | 常に失敗する |
| `Parsers.Satisfy(predicate)` | 条件を満たす1トークンをパース |

### 選択コンビネーター

| パーサー | 説明 |
|---------|------|
| `p1.Or(p2)` | p1を試し、失敗（入力未消費）ならp2を試す |
| `p.Try()` | 失敗時に入力位置を復元（バックトラック有効化） |
| `Parsers.Choice(p1, p2, ...)` | 複数のパーサーから最初に成功したものを採用 |

### シーケンスコンビネーター

| パーサー | 説明 |
|---------|------|
| `p1.Then(p2)` | p1の後にp2を実行、タプルで返す |
| `p1.ThenSkip(p2)` | p1の後にp2を実行、p1の結果を返す |
| `p1.SkipThen(p2)` | p1の後にp2を実行、p2の結果を返す |
| `Parsers.Between(open, close, p)` | openとcloseで囲まれたpをパース |

### 繰り返しコンビネーター

| パーサー | 説明 |
|---------|------|
| `p.Many()` | 0回以上の繰り返し |
| `p.Many1()` | 1回以上の繰り返し |
| `p.Count(n)` | 正確にn回の繰り返し |
| `p.SepBy(sep)` | sepで区切られた0回以上の繰り返し |
| `p.SepBy1(sep)` | sepで区切られた1回以上の繰り返し |
| `p.EndBy(sep)` | 各要素の後にsepが来る形式 |
| `p.ManyTill(end)` | endがマッチするまで繰り返す |

### チェインコンビネーター（式パーサー用）

| パーサー | 説明 |
|---------|------|
| `p.ChainLeft(op, default)` | 左結合の演算子チェイン |
| `p.ChainRight(op, default)` | 右結合の演算子チェイン |

### オプショナルコンビネーター

| パーサー | 説明 |
|---------|------|
| `p.Optional()` | 失敗してもOK (`Option<T>`を返す) |
| `p.OptionalOr(default)` | 失敗時にデフォルト値を返す |
| `p.OptionalValue()` | 失敗時にnullを返す (`T?` を返す) |

### ユーティリティ

| パーサー | 説明 |
|---------|------|
| `p.Select(f)` | 結果を変換 |
| `p.Where(predicate)` | 結果をフィルタリング |
| `p.Named(name)` | パーサーに名前を付ける（エラーメッセージ用） |
| `p.WithExpected(msg)` | 期待値メッセージをカスタマイズ |
| `p.Lexeme()` | 後続の空白をスキップ |
| `Parsers.Lazy(factory)` | 遅延評価パーサー（再帰定義用） |

---

## 4. LINQ/クエリ式構文

ClaudeParserはLINQの`Select`、`SelectMany`、`Where`をサポートしています。
これにより、クエリ式構文で直感的にパーサーを記述できます。

### シーケンシャルパース

```csharp
// 従来の書き方
var parser1 = CharParsers.String("Name:")
    .Then(CharParsers.TakeWhile(c => c != '\n'))
    .Select(name => name.Trim());

// クエリ式構文
var parser2 =
    from _ in CharParsers.String("Name:")
    from name in CharParsers.TakeWhile(c => c != '\n')
    select name.Trim();
```

### 複数の値を組み合わせる

```csharp
// 座標「(x, y)」をパースする
var coordinate =
    from _ in CharParsers.Char('(')
    from x in CharParsers.Integer.Lexeme()  // 後続の空白をスキップ
    from __ in CharParsers.Char(',').Lexeme()
    from y in CharParsers.Integer.Lexeme()
    from ___ in CharParsers.Char(')')
    select (X: x, Y: y);
```

### 条件フィルタリング（where句）

```csharp
// 1～100の範囲の整数のみ受け付ける
var boundedInt =
    from n in CharParsers.Integer
    where n >= 1 && n <= 100
    select n;
```

---

### クエリ式の展開（SelectManyの仕組み）

C#のクエリ式（`from`を使った構文）は、コンパイラによって`SelectMany`メソッド呼び出しに**脱糖（デシュガー）** されます。ClaudeParserがクエリ式をサポートできるのは、`Parser<T, TToken>`クラスに適切な`Select`、`SelectMany`、`Where`メソッドが実装されているからです。

#### 1. 基本原則：fromはSelectManyになる

複数の`from`句を持つクエリ式は、`SelectMany`のチェーンに変換されます。

**クエリ式:**
```csharp
var parser =
    from a in parserA
    from b in parserB
    select a + b;
```

**コンパイラによる展開後:**
```csharp
var parser = parserA.SelectMany(
    a => parserB,                    // 次のパーサーを返す
    (a, b) => a + b                  // 両方の結果を合成
);
```

#### 2. 3つ以上のfrom句の場合

`from`句が3つ以上ある場合、`SelectMany`がネストします。

**クエリ式:**
```csharp
var parser =
    from a in parserA
    from b in parserB
    from c in parserC
    select (a, b, c);
```

**コンパイラによる展開後:**
```csharp
var parser = parserA.SelectMany(
    a => parserB.SelectMany(
        b => parserC,
        (b, c) => new { b, c }       // 中間結果を匿名型で保持
    ),
    (a, bc) => (a, bc.b, bc.c)       // 最終結果を構築
);
```

#### 3. SelectManyのシグネチャ

ClaudeParserの`SelectMany`メソッドは以下のシグネチャを持ちます：

```csharp
public Parser<TResult, TToken> SelectMany<TNext, TResult>(
    Func<T, Parser<TNext, TToken>> selector,      // 最初の結果から次のパーサーを生成
    Func<T, TNext, TResult> resultSelector        // 両方の結果を合成
)
```

- **`selector`**: 最初のパーサーの結果（型`T`）を受け取り、次に実行するパーサーを返す関数
- **`resultSelector`**: 最初の結果と2番目の結果を受け取り、最終結果を生成する関数

#### 4. SelectManyの内部実装

```csharp
public Parser<TResult, TToken> SelectMany<TNext, TResult>(
    Func<T, Parser<TNext, TToken>> selector,
    Func<T, TNext, TResult> resultSelector) =>
    new((input, ctx) =>
    {
        // ステップ1: 最初のパーサーを実行
        var result1 = _parse(input, ctx);
        if (result1 is not SuccessResult<T, TToken> success1)
            return ((FailureResult<T, TToken>)result1).Cast<TResult>();

        // ステップ2: 最初の結果から次のパーサーを生成して実行
        var parser2 = selector(success1.Value);
        var result2 = parser2.Parse(success1.Remaining, ctx);  // ← 残りの入力を渡す
        
        if (result2 is not SuccessResult<TNext, TToken> success2)
        {
            // 失敗時はエラー情報をマージ
            var failure = (FailureResult<TNext, TToken>)result2;
            var mergedError = success1.Error != null 
                ? success1.Error.Merge(failure.ErrorValue) 
                : failure.ErrorValue;
            return ParseResult<TResult, TToken>.Failure(mergedError, failure.Remaining);
        }

        // ステップ3: 両方の結果を合成
        var finalResult = resultSelector(success1.Value, success2.Value);
        return ParseResult<TResult, TToken>.Success(finalResult, success2.Remaining, ...);
    }, ...);
```

**重要なポイント:**

1. **逐次実行**: 最初のパーサーが成功した後、その**残りの入力**に対して2番目のパーサーが実行される
2. **早期終了**: 途中で失敗したら、そこでパース全体が失敗する
3. **エラーマージ**: 失敗時は、それまでに蓄積したエラー情報がマージされて保持される

#### 5. 具体例：座標パーサーの展開

```csharp
// クエリ式
var coordinate =
    from _  in CharParsers.Char('(')
    from x  in CharParsers.Integer
    from __ in CharParsers.Char(',')
    from y  in CharParsers.Integer
    from ___ in CharParsers.Char(')')
    select (x, y);
```

**展開後（簡略化）:**
```csharp
var coordinate = 
    CharParsers.Char('(').SelectMany(
        _ => CharParsers.Integer.SelectMany(
            x => CharParsers.Char(',').SelectMany(
                __ => CharParsers.Integer.SelectMany(
                    y => CharParsers.Char(')'),
                    (y, ___) => (y, ___)
                ),
                (__, result) => (__, result.y, result.___)
            ),
            (x, result) => (x, result.y)
        ),
        (_, result) => (result.x, result.y)  // 最終的に (x, y) を構築
    );
```

#### 6. なぜSelectManyという名前か

LINQの`SelectMany`は元々「1つの要素を複数の要素に展開し、結果をフラットにする」操作です。
パーサーコンビネーターでは「1つのパース結果から次のパーサーを生成し、結果を合成する」という意味になります。

これは関数型プログラミングの**モナド**における`bind`（`>>=`）操作に相当します：

| 概念 | Haskell | LINQ | ClaudeParser |
|------|---------|------|--------------|
| 成功のラップ | `return` | `Return` | `Parsers.Return` |
| 連結 | `>>=` (bind) | `SelectMany` | `SelectMany` |
| 変換 | `fmap` | `Select` | `Select` |

#### 7. where句の展開

`where`句は`Where`メソッドに展開されます：

```csharp
// クエリ式
var positiveInt =
    from n in CharParsers.Integer
    where n > 0
    select n;

// 展開後
var positiveInt = CharParsers.Integer
    .Where(n => n > 0)
    .Select(n => n);
```

---

## 5. 演算子優先順位パーサー（Text.Parsec.Expr相当）

HaskellのText.Parsec.Exprモジュールに相当する機能を提供しています。
演算子の優先順位と結合規則を宣言的に定義できます。

### 基本的な使い方

```csharp
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
```

### 演算子の種類

| 演算子 | 説明 |
|--------|------|
| `InfixOperator.CreateLeft(op, func)` | 左結合の二項演算子 |
| `InfixOperator.CreateRight(op, func)` | 右結合の二項演算子 |
| `InfixOperator.CreateNone(op, func)` | 非結合の二項演算子 |
| `PrefixOperator.Create(op, func)` | 前置単項演算子 |
| `PostfixOperator.Create(op, func)` | 後置単項演算子 |

### 右結合演算子の例（べき乗）

```csharp
var powerOp = InfixOperator.CreateRight(
    CharParsers.Symbol("^"),
    (a, b) => Math.Pow(a, b));

// 2^3^4 は 2^(3^4) = 2^81 として評価される
```

---

## 6. バイナリパーサー

バイト配列やバイナリストリームをパースするための機能を提供しています。

### 基本的なバイナリパーサー

```csharp
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
```

### 使用例：簡単なバイナリフォーマット

```csharp
// ヘッダー: マジック(4バイト) + バージョン(2バイト) + レコード数(4バイト)
var header =
    from magic in BinaryParsers.Bytes(new byte[] { 0x89, 0x50, 0x4E, 0x47 })
    from version in BinaryParsers.UInt16BE
    from count in BinaryParsers.UInt32BE
    select (Version: version, Count: count);

var input = new ByteInputStream(data);
var result = header.Parse(input);
```

---

## 7. エラー処理とトレース機能

### エラーメッセージ

パース失敗時には、位置情報と期待されるトークンを含む詳細なエラーメッセージが生成されます。

```csharp
var parser = CharParsers.Integer;
var result = parser.Parse(new StringInputStream("abc"));

if (!result.IsSuccess)
{
    var failure = (FailureResult<long, char>)result;
    Console.WriteLine(failure.ErrorValue.FormatVerbose());
    // 出力例:
    // パースエラー at line 1, col 1
    //   期待: 数字
    //   検出: 'a'
}
```

### カスタムエラーメッセージ

```csharp
var positiveInt =
    from n in CharParsers.Integer
    where n > 0
    select n;

// エラーメッセージをカスタマイズ
var betterParser = positiveInt.WithExpected("正の整数");
```

### トレース機能

デバッグ時にパーサーの実行過程を追跡できます。

```csharp
// トレースを有効にしてパース
var context = ParseContext.WithTracing();
var parser = CharParsers.String("hello").Named("HelloParser");

parser.Parse(new StringInputStream("hello"), context);

// トレースを出力
Console.WriteLine(context.Trace.ToReport());
// 出力例:
// ✓ HelloParser @ <input>:1:1 -> <input>:1:6 (0.02ms)
```

### ParseException

`GetValueOrThrow()`を使うと、失敗時に例外をスローできます。

```csharp
try
{
    var value = parser.Parse(input).GetValueOrThrow();
}
catch (ParseException ex)
{
    Console.WriteLine(ex.Message);  // パースエラーの詳細
}
```

---

## 8. サンプルパーサー

### CSVパーサー（`ClaudeParser.Examples.CsvParser`）

```csharp
var csv = CsvParser.Parse("name,age\nAlice,30\nBob,25");
// csv.Headers: ["name", "age"]
// csv.Rows: [["Alice", "30"], ["Bob", "25"]]
```

### 四則演算パーサー（`ClaudeParser.Examples.CalculatorParser`）

```csharp
var result = CalculatorParser.Evaluate("(1 + 2) * 3 + 4 / 2");
// result: 11.0
```

### JSONパーサー（`ClaudeParser.Examples.JsonParser`）

```csharp
var json = JsonParser.Parse("{\"name\":\"Alice\",\"age\":30}");
// json: JsonObject containing name and age
```

### 正規表現パーサー（`ClaudeParser.Examples.RegexParser`）

```csharp
// 正規表現からパーサーを動的に生成
var match = RegexParser.Match("[a-z]+", "hello world");
// match: "hello"

var allMatches = RegexParser.FindAll("\\d+", "a1b23c456");
// allMatches: ["1", "23", "456"]
```

### MIDIパーサー（`ClaudeParser.Examples.MidiParser`）

```csharp
var midi = MidiParser.Parse(midiFileBytes);
// MIDIファイルの構造を解析
```

---

## 9. 陥りやすい罠と対処法

> **重要**: パーサーコンビネーターを使う際に特に注意が必要な点

### 罠1: 左再帰による無限ループ

#### 問題

以下のような左再帰的な文法を直接実装すると、無限ループに陥ります：

```csharp
// 危険! 無限ループ!
Parser<int, char>? expr = null;
expr = (
    from e in Parsers.Lazy(() => expr!)      // 自分自身を最初に呼ぶ
    from _ in CharParsers.Char('+')
    from n in CharParsers.Integer
    select e + n
).Or(CharParsers.Integer);
```

この定義では、exprをパースしようとすると、まずexprをパースしようとし、
さらにexprをパースしようとし...という無限再帰が発生します。

#### 解決策1: ChainLeftを使う

```csharp
// 安全: ChainLeftで左結合の演算子チェインを処理
var addOp = CharParsers.Char('+').Select<char, Func<long, long, long>>(_ => (a, b) => a + b);
var expr = CharParsers.Integer.ChainLeft(addOp, 0);
```

#### 解決策2: ExpressionParserを使う

```csharp
var table = new OperatorTable<long, char>
{
    { InfixOperator.CreateLeft(CharParsers.Char('+'), (a, b) => a + b) }
};
var expr = ExpressionParser.Build(table, CharParsers.Integer);
```

#### 解決策3: 文法を右再帰に書き換える

```csharp
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
```

---

### 罠2: Tryなしのバックトラック失敗

#### 問題

Or演算子は、最初のパーサーが入力を消費してから失敗した場合、
バックトラック（2番目のパーサーを試すこと）をしません。

```csharp
// 問題のあるコード
var parser = CharParsers.String("abc").Or(CharParsers.String("abd"));

var result = parser.Parse(new StringInputStream("abd"));
// 失敗! "abc"が"ab"を消費してから'd'で失敗するが、
// バックトラックしないため"abd"を試さない
```

#### 解決策: Tryを使ってバックトラックを有効にする

```csharp
var parser = CharParsers.String("abc").Try().Or(CharParsers.String("abd"));

var result = parser.Parse(new StringInputStream("abd"));
// 成功! Try()により"abc"が失敗したら入力位置が復元される
```

#### When to use Try

- 複数の選択肢が共通のプレフィックスを持つ場合
- 先読みが必要な場合

#### 注意

`Try()`はパフォーマンスに影響するため、必要な場所でのみ使用してください。
可能であれば、共通プレフィックスをファクタリングしましょう：

```csharp
// より効率的な書き方
var parser = 
    from prefix in CharParsers.String("ab")
    from suffix in CharParsers.Char('c').Or(CharParsers.Char('d'))
    select prefix + suffix;
```

---

### 罠3: ManyとSatisfyの危険な組み合わせ

#### 問題

`Many`は「0回以上」の繰り返しを表すため、内部パーサーが何も消費せずに成功する場合、無限ループになります。

```csharp
// 危険! 無限ループ!
var parser = CharParsers.Spaces.Many();  // Spacesは0個の空白でも成功する
```

#### 解決策: 入力を必ず消費するパーサーを使う

```csharp
// 安全: Many1は1回以上の空白を要求
var parser = CharParsers.Char(' ').Many1();

// または、条件を厳しくする
var parser = Parsers.Satisfy<char>(char.IsWhiteSpace).Many();
```

---

### 罠4: Lazyを使わない再帰定義

#### 問題

再帰的なパーサーを定義する際、`Lazy`を使わないとパーサー構築時点でnull参照エラーが発生します。

```csharp
// エラー! expr はまだ null
Parser<long, char>? expr = null;
var term = CharParsers.Integer.Or(CharParsers.Parens(expr!));  // NullReferenceException
expr = term;
```

#### 解決策: Parsers.Lazyで遅延評価する

```csharp
Parser<long, char>? expr = null;
var term = CharParsers.Integer.Or(
    Parsers.Lazy(() => CharParsers.Parens(expr!))  // ラムダにより遅延評価
);
expr = term;
```

---

### 罠5: 空白処理の忘れ

#### 問題

トークン間の空白を適切に処理しないと、パースが失敗します。

```csharp
// 失敗する可能性がある
var parser = CharParsers.String("var").Then(CharParsers.Identifier);
parser.Parse(new StringInputStream("var x"));  // 失敗! "var"と"x"の間の空白
```

#### 解決策: Lexeme()を使う

```csharp
// Lexeme()は後続の空白を自動的にスキップ
var parser = CharParsers.String("var").Lexeme().Then(CharParsers.Identifier);
parser.Parse(new StringInputStream("var x"));  // 成功: "x"
```

#### トークナイザースタイル

```csharp
// Symbol関数（文字列 + 空白スキップ）
var keyword = CharParsers.Symbol("var");
var parser = keyword.Then(CharParsers.Identifier.Lexeme());
```

---

### 罠6: パーサーの再利用による予期しない動作

#### 問題

状態を持つパーサーを複数回使用すると、予期しない動作が発生する可能性があります
（ClaudeParserでは基本的に問題ないが、注意が必要）。

#### ベストプラクティス

パーサーは常に純粋（副作用なし）であるべきです。
パース結果の処理は、Selectの中か、パース完了後に行いましょう。

```csharp
// 良い例: Selectでの変換
var parser = CharParsers.Integer.Select(n => n * 2);

// 避けるべき例: 外部状態の変更
var count = 0;
var parser = CharParsers.Integer.Select(n => {
    count++;  // 副作用!
    return n;
});
```

---

### 罠7: EndOfInputのチェック忘れ

#### 問題

パーサーが入力を部分的にしか消費せず、残りを無視してしまう場合があります。

```csharp
var parser = CharParsers.Integer;
var result = parser.Parse(new StringInputStream("123abc"));
// 成功するが、"abc"が残っている
```

#### 解決策: EndOfInputでチェック

```csharp
var parser = CharParsers.Integer.ThenSkip(CharParsers.EndOfInput);
var result = parser.Parse(new StringInputStream("123abc"));
// 失敗: 期待=EOF, 検出='a'
```

---

## 10. APIリファレンス

### 名前空間

| 名前空間 | 説明 |
|---------|------|
| `ClaudeParser.Core` | 基本型（Parser, ParseResult, IInputStream等） |
| `ClaudeParser.Combinators` | パーサーコンビネーター |

### 主要な型

| 型 | 説明 |
|----|------|
| `Parser<T, TToken>` | パーサーの中心型 |
| `IInputStream<TToken>` | 入力ストリームのインターフェース |
| `StringInputStream` | 文字列入力 |
| `ByteInputStream` | バイト配列入力 |
| `ListInputStream<T>` | 任意のリスト入力 |
| `ParseResult<T, TToken>` | パース結果（Success/Failure） |
| `ParseError` | エラー情報 |
| `ParseContext` | パースコンテキスト（トレース等） |
| `OperatorTable<T, TToken>` | 演算子優先順位テーブル |

### 主要なメソッド

| メソッド | 説明 |
|---------|------|
| `parser.Parse(input)` | パースを実行 |
| `parser.Parse(input, context)` | コンテキスト付きでパースを実行 |
| `result.IsSuccess` | 成功かどうか |
| `result.GetValueOrThrow()` | 値を取得、失敗時は例外 |
| `result.Match(onSuccess, onFailure)` | パターンマッチング |

---

## 11. GitHub Pagesへのデプロイ

### 自動デプロイ（GitHub Actions）

このリポジトリには、mainブランチへのpush時に自動でGitHub Pagesにデプロイするワークフローが含まれています。

#### セットアップ手順

1. **GitHub Pagesを有効化**:
   - リポジトリの Settings → Pages
   - Source を "GitHub Actions" に設定

2. **mainブランチにpush**:
   - ワークフローが自動実行され、デプロイされます

#### ローカルで実行

```bash
cd ClaudeParser.Web
dotnet run
```

ブラウザで `https://localhost:5001` または表示されるURLにアクセスしてください。

### フレームワークファイルについて

Blazor WebAssemblyのフレームワークファイル（`_framework/`フォルダ）は`dotnet publish`時に自動生成されます。
ソースコードには含まれず、GitHub Actionsでビルド時に生成されるため、gitにコミットする必要はありません。

### ワークフローの内容

`.github/workflows/deploy.yml`:
- .NET 10のセットアップ
- テスト実行
- Blazor WASMのpublish
- base-tagの書き換え（リポジトリ名に合わせる）
- GitHub Pagesへのデプロイ

---

*以上*
