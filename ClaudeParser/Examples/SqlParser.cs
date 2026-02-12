using ClaudeParser.Core;
using ClaudeParser.Combinators;
using System.Text;

namespace ClaudeParser.Examples;

/// <summary>
/// SQL SELECT文パーサー
/// サブクエリ、スカラサブクエリ、テーブル結合に対応
/// </summary>
public static class SqlParser
{
    #region AST定義

    /// <summary>
    /// SQLノードの基底クラス
    /// </summary>
    public abstract record SqlNode
    {
        public abstract string ToSql(int indent = 0);
    }

    /// <summary>
    /// SELECT文
    /// </summary>
    public record SelectStatement(
        bool Distinct,
        IReadOnlyList<SelectItem> Columns,
        FromClause? From,
        Expression? Where,
        IReadOnlyList<Expression>? GroupBy,
        Expression? Having,
        IReadOnlyList<OrderByItem>? OrderBy,
        int? Limit,
        int? Offset
    ) : SqlNode
    {
        public override string ToSql(int indent = 0)
        {
            var sb = new StringBuilder();
            var ind = new string(' ', indent);
            
            sb.Append(ind);
            sb.Append("SELECT");
            if (Distinct) sb.Append(" DISTINCT");
            sb.AppendLine();
            
            sb.Append(ind);
            sb.Append("  ");
            sb.AppendLine(string.Join(",\n" + ind + "  ", Columns.Select(c => c.ToSql())));
            
            if (From != null)
            {
                sb.Append(ind);
                sb.Append("FROM ");
                sb.AppendLine(From.ToSql(indent));
            }
            
            if (Where != null)
            {
                sb.Append(ind);
                sb.Append("WHERE ");
                sb.AppendLine(Where.ToSql());
            }
            
            if (GroupBy != null && GroupBy.Count > 0)
            {
                sb.Append(ind);
                sb.Append("GROUP BY ");
                sb.AppendLine(string.Join(", ", GroupBy.Select(g => g.ToSql())));
            }
            
            if (Having != null)
            {
                sb.Append(ind);
                sb.Append("HAVING ");
                sb.AppendLine(Having.ToSql());
            }
            
            if (OrderBy != null && OrderBy.Count > 0)
            {
                sb.Append(ind);
                sb.Append("ORDER BY ");
                sb.AppendLine(string.Join(", ", OrderBy.Select(o => o.ToSql())));
            }
            
            if (Limit.HasValue)
            {
                sb.Append(ind);
                sb.AppendLine($"LIMIT {Limit.Value}");
            }
            
            if (Offset.HasValue)
            {
                sb.Append(ind);
                sb.AppendLine($"OFFSET {Offset.Value}");
            }
            
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// SELECT句の項目
    /// </summary>
    public record SelectItem(Expression Expr, string? Alias) : SqlNode
    {
        public override string ToSql(int indent = 0) =>
            Alias != null ? $"{Expr.ToSql()} AS {Alias}" : Expr.ToSql();
    }

    /// <summary>
    /// ORDER BY句の項目
    /// </summary>
    public record OrderByItem(Expression Expr, bool Descending) : SqlNode
    {
        public override string ToSql(int indent = 0) =>
            Descending ? $"{Expr.ToSql()} DESC" : Expr.ToSql();
    }

    /// <summary>
    /// FROM句
    /// </summary>
    public record FromClause(TableReference Table) : SqlNode
    {
        public override string ToSql(int indent = 0) => Table.ToSql(indent);
    }

    /// <summary>
    /// テーブル参照の基底クラス
    /// </summary>
    public abstract record TableReference : SqlNode;

    /// <summary>
    /// 単一テーブル
    /// </summary>
    public record TableName(string Schema, string Name, string? Alias) : TableReference
    {
        public override string ToSql(int indent = 0)
        {
            var name = string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
            return Alias != null ? $"{name} AS {Alias}" : name;
        }
    }

    /// <summary>
    /// サブクエリ（FROM句内）
    /// </summary>
    public record SubqueryTable(SelectStatement Query, string Alias) : TableReference
    {
        public override string ToSql(int indent = 0) =>
            $"(\n{Query.ToSql(indent + 2)}\n{new string(' ', indent)}) AS {Alias}";
    }

    /// <summary>
    /// JOIN
    /// </summary>
    public record JoinedTable(TableReference Left, JoinType Type, TableReference Right, Expression? On) : TableReference
    {
        public override string ToSql(int indent = 0)
        {
            var joinStr = Type switch
            {
                JoinType.Inner => "INNER JOIN",
                JoinType.Left => "LEFT JOIN",
                JoinType.Right => "RIGHT JOIN",
                JoinType.Full => "FULL JOIN",
                JoinType.Cross => "CROSS JOIN",
                _ => "JOIN"
            };
            var result = $"{Left.ToSql(indent)}\n{new string(' ', indent)}{joinStr} {Right.ToSql(indent)}";
            if (On != null)
            {
                result += $" ON {On.ToSql()}";
            }
            return result;
        }
    }

    public enum JoinType
    {
        Inner,
        Left,
        Right,
        Full,
        Cross
    }

    /// <summary>
    /// 式の基底クラス
    /// </summary>
    public abstract record Expression : SqlNode;

    /// <summary>
    /// ワイルドカード（*）
    /// </summary>
    public record Wildcard(string? TableName) : Expression
    {
        public override string ToSql(int indent = 0) =>
            TableName != null ? $"{TableName}.*" : "*";
    }

    /// <summary>
    /// カラム参照
    /// </summary>
    public record ColumnRef(string? TableName, string ColumnName) : Expression
    {
        public override string ToSql(int indent = 0) =>
            TableName != null ? $"{TableName}.{ColumnName}" : ColumnName;
    }

    /// <summary>
    /// リテラル値
    /// </summary>
    public abstract record Literal : Expression;

    public record IntLiteral(long Value) : Literal
    {
        public override string ToSql(int indent = 0) => Value.ToString();
    }

    public record DoubleLiteral(double Value) : Literal
    {
        public override string ToSql(int indent = 0) => Value.ToString();
    }

    public record StringLiteral(string Value) : Literal
    {
        public override string ToSql(int indent = 0) => $"'{Value.Replace("'", "''")}'";
    }

    public record NullLiteral() : Literal
    {
        public override string ToSql(int indent = 0) => "NULL";
    }

    public record BoolLiteral(bool Value) : Literal
    {
        public override string ToSql(int indent = 0) => Value ? "TRUE" : "FALSE";
    }

    /// <summary>
    /// 二項演算
    /// </summary>
    public record BinaryOp(Expression Left, string Operator, Expression Right) : Expression
    {
        public override string ToSql(int indent = 0) =>
            $"({Left.ToSql()} {Operator} {Right.ToSql()})";
    }

    /// <summary>
    /// 単項演算
    /// </summary>
    public record UnaryOp(string Operator, Expression Operand) : Expression
    {
        public override string ToSql(int indent = 0) =>
            Operator.ToUpper() == "NOT" ? $"NOT {Operand.ToSql()}" : $"{Operator}{Operand.ToSql()}";
    }

    /// <summary>
    /// 関数呼び出し
    /// </summary>
    public record FunctionCall(string Name, IReadOnlyList<Expression> Args, bool Distinct) : Expression
    {
        public override string ToSql(int indent = 0)
        {
            var distinct = Distinct ? "DISTINCT " : "";
            return $"{Name.ToUpper()}({distinct}{string.Join(", ", Args.Select(a => a.ToSql()))})";
        }
    }

    /// <summary>
    /// CASE式
    /// </summary>
    public record CaseExpression(
        Expression? Operand,
        IReadOnlyList<(Expression When, Expression Then)> WhenClauses,
        Expression? Else
    ) : Expression
    {
        public override string ToSql(int indent = 0)
        {
            var sb = new StringBuilder("CASE");
            if (Operand != null)
            {
                sb.Append($" {Operand.ToSql()}");
            }
            foreach (var (when, then) in WhenClauses)
            {
                sb.Append($" WHEN {when.ToSql()} THEN {then.ToSql()}");
            }
            if (Else != null)
            {
                sb.Append($" ELSE {Else.ToSql()}");
            }
            sb.Append(" END");
            return sb.ToString();
        }
    }

    /// <summary>
    /// IN式
    /// </summary>
    public record InExpression(Expression Operand, IReadOnlyList<Expression>? Values, SelectStatement? Subquery, bool Not) : Expression
    {
        public override string ToSql(int indent = 0)
        {
            var notStr = Not ? "NOT " : "";
            if (Values != null)
            {
                return $"{Operand.ToSql()} {notStr}IN ({string.Join(", ", Values.Select(v => v.ToSql()))})";
            }
            return $"{Operand.ToSql()} {notStr}IN (\n{Subquery!.ToSql(indent + 2)}\n{new string(' ', indent)})";
        }
    }

    /// <summary>
    /// BETWEEN式
    /// </summary>
    public record BetweenExpression(Expression Operand, Expression Low, Expression High, bool Not) : Expression
    {
        public override string ToSql(int indent = 0)
        {
            var notStr = Not ? "NOT " : "";
            return $"{Operand.ToSql()} {notStr}BETWEEN {Low.ToSql()} AND {High.ToSql()}";
        }
    }

    /// <summary>
    /// EXISTS式
    /// </summary>
    public record ExistsExpression(SelectStatement Subquery, bool Not) : Expression
    {
        public override string ToSql(int indent = 0)
        {
            var notStr = Not ? "NOT " : "";
            return $"{notStr}EXISTS (\n{Subquery.ToSql(indent + 2)}\n{new string(' ', indent)})";
        }
    }

    /// <summary>
    /// スカラサブクエリ
    /// </summary>
    public record ScalarSubquery(SelectStatement Query) : Expression
    {
        public override string ToSql(int indent = 0) =>
            $"(\n{Query.ToSql(indent + 2)}\n{new string(' ', indent)})";
    }

    /// <summary>
    /// IS NULL / IS NOT NULL
    /// </summary>
    public record IsNullExpression(Expression Operand, bool Not) : Expression
    {
        public override string ToSql(int indent = 0) =>
            Not ? $"{Operand.ToSql()} IS NOT NULL" : $"{Operand.ToSql()} IS NULL";
    }

    /// <summary>
    /// LIKE式
    /// </summary>
    public record LikeExpression(Expression Operand, Expression Pattern, bool Not) : Expression
    {
        public override string ToSql(int indent = 0)
        {
            var notStr = Not ? "NOT " : "";
            return $"{Operand.ToSql()} {notStr}LIKE {Pattern.ToSql()}";
        }
    }

    #endregion

    #region パーサー定義

    /// <summary>
    /// 空白をスキップ
    /// </summary>
    private static readonly Parser<Unit, char> Ws =
        Parsers.Satisfy<char>(char.IsWhiteSpace, "whitespace").Many().Select(_ => Unit.Value);

    /// <summary>
    /// キーワードをパース（大文字小文字区別なし）
    /// </summary>
    private static Parser<string, char> Keyword(string keyword)
    {
        return new Parser<string, char>((input, context) =>
        {
            var result = new StringBuilder();
            var current = input;
            
            foreach (char c in keyword)
            {
                if (current.IsAtEnd)
                    return ParseResult<string, char>.Failure(
                        new ParseError(current.Position, ErrorMessage.Expected($"'{keyword}'")), current);
                
                if (char.ToUpperInvariant(current.Current) != char.ToUpperInvariant(c))
                    return ParseResult<string, char>.Failure(
                        new ParseError(current.Position, ErrorMessage.Expected($"'{keyword}'")), current);
                
                result.Append(current.Current);
                current = current.Advance();
            }
            
            // キーワードの後に識別子文字が続かないことを確認
            if (!current.IsAtEnd && (char.IsLetterOrDigit(current.Current) || current.Current == '_'))
                return ParseResult<string, char>.Failure(
                    new ParseError(current.Position, ErrorMessage.Expected($"keyword '{keyword}'")), current);
            
            return ParseResult<string, char>.Success(result.ToString(), current);
        }, keyword);
    }

    /// <summary>
    /// 予約語のセット
    /// </summary>
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "BETWEEN", "LIKE",
        "IS", "NULL", "TRUE", "FALSE", "AS", "ON", "JOIN", "INNER", "LEFT", "RIGHT",
        "FULL", "CROSS", "OUTER", "GROUP", "BY", "HAVING", "ORDER", "ASC", "DESC",
        "LIMIT", "OFFSET", "DISTINCT", "ALL", "CASE", "WHEN", "THEN", "ELSE", "END",
        "EXISTS", "UNION", "INTERSECT", "EXCEPT"
    };

    private static bool IsReservedWord(string name) => ReservedWords.Contains(name);

    /// <summary>
    /// 識別子
    /// </summary>
    private static readonly Parser<string, char> Identifier =
        from first in Parsers.Satisfy<char>(c => char.IsLetter(c) || c == '_', "letter or underscore")
        from rest in Parsers.Satisfy<char>(c => char.IsLetterOrDigit(c) || c == '_', "letter, digit or underscore").Many()
        let name = new string(rest.Prepend(first).ToArray())
        where !IsReservedWord(name)
        select name;

    /// <summary>
    /// クォート付き識別子
    /// </summary>
    private static readonly Parser<string, char> QuotedIdentifier =
        from open in CharParsers.Char('"')
        from chars in Parsers.Satisfy<char>(c => c != '"', "non-quote").Many1()
        from close in CharParsers.Char('"')
        select new string(chars.ToArray());

    /// <summary>
    /// 任意の識別子
    /// </summary>
    private static readonly Parser<string, char> AnyIdentifier =
        QuotedIdentifier.Try().Or(Identifier);

    /// <summary>
    /// 整数リテラル
    /// </summary>
    private static readonly Parser<Expression, char> IntegerLiteral =
        from sign in CharParsers.Char('-').Try().Select(c => "-").Or(Parsers.Return<string, char>(""))
        from digits in CharParsers.Digit.Many1()
        let value = long.Parse(sign + new string(digits.ToArray()))
        select (Expression)new IntLiteral(value);

    /// <summary>
    /// 浮動小数点リテラル
    /// </summary>
    private static readonly Parser<Expression, char> FloatLiteral =
        from sign in CharParsers.Char('-').Try().Select(c => "-").Or(Parsers.Return<string, char>(""))
        from intPart in CharParsers.Digit.Many()
        from dot in CharParsers.Char('.')
        from fracPart in CharParsers.Digit.Many1()
        let str = sign + new string(intPart.ToArray()) + "." + new string(fracPart.ToArray())
        let value = double.Parse(str)
        select (Expression)new DoubleLiteral(value);

    /// <summary>
    /// 文字列リテラル
    /// </summary>
    private static readonly Parser<Expression, char> StringLiteralParser =
        from open in CharParsers.Char('\'')
        from chars in CharParsers.String("''").Try().Select(_ => '\'')
            .Or(Parsers.Satisfy<char>(c => c != '\'', "non-quote"))
            .Many()
        from close in CharParsers.Char('\'')
        select (Expression)new StringLiteral(new string(chars.ToArray()));

    /// <summary>
    /// NULLリテラル
    /// </summary>
    private static readonly Parser<Expression, char> NullLiteralParser =
        Keyword("NULL").Select(_ => (Expression)new NullLiteral());

    /// <summary>
    /// BOOLリテラル
    /// </summary>
    private static readonly Parser<Expression, char> BoolLiteralParser =
        Keyword("TRUE").Select(_ => (Expression)new BoolLiteral(true))
        .Or(Keyword("FALSE").Select(_ => (Expression)new BoolLiteral(false)));

    /// <summary>
    /// ワイルドカード
    /// </summary>
    private static Parser<Expression, char> WildcardParser =>
        (
            from tableName in AnyIdentifier
            from _ in Ws
            from dot in CharParsers.Char('.')
            from __ in Ws
            from star in CharParsers.Char('*')
            select (Expression)new Wildcard(tableName)
        ).Try().Or(
            from star in CharParsers.Char('*')
            select (Expression)new Wildcard(null)
        );

    /// <summary>
    /// カラム参照
    /// </summary>
    private static Parser<Expression, char> ColumnRefParser =>
        (
            from tableName in AnyIdentifier
            from _ in Ws
            from dot in CharParsers.Char('.')
            from __ in Ws
            from colName in AnyIdentifier
            select (Expression)new ColumnRef(tableName, colName)
        ).Try().Or(
            from name in AnyIdentifier
            select (Expression)new ColumnRef(null, name)
        );

    /// <summary>
    /// 関数呼び出し
    /// </summary>
    private static Parser<Expression, char> FunctionCallParser =>
        from name in AnyIdentifier
        from _ in Ws
        from open in CharParsers.Char('(')
        from __ in Ws
        from distinct in Keyword("DISTINCT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
        from ___ in Ws
        from args in WildcardParser.Select(w => new List<Expression> { w } as IReadOnlyList<Expression>)
            .Try()
            .Or(ExpressionParser.Value.SepBy(
                from c in CharParsers.Char(',')
                from w in Ws
                select c
            ).Select(list => list as IReadOnlyList<Expression>))
        from ____ in Ws
        from close in CharParsers.Char(')')
        select (Expression)new FunctionCall(name, args, distinct);

    /// <summary>
    /// CASE式
    /// </summary>
    private static Parser<Expression, char> CaseParser =>
        from _ in Keyword("CASE")
        from __ in Ws
        from operand in ExpressionParser.Value.Try().Select(e => (Expression?)e).Or(Parsers.Return<Expression?, char>(null))
        from ___ in Ws
        from whens in (
            from w in Keyword("WHEN")
            from ____ in Ws
            from whenExpr in ExpressionParser.Value
            from _____ in Ws
            from t in Keyword("THEN")
            from ______ in Ws
            from thenExpr in ExpressionParser.Value
            from _______ in Ws
            select (whenExpr, thenExpr)
        ).Many1()
        from elseClause in (
            from e in Keyword("ELSE")
            from ________ in Ws
            from elseExpr in ExpressionParser.Value
            from _________ in Ws
            select elseExpr
        ).Try().Select(e => (Expression?)e).Or(Parsers.Return<Expression?, char>(null))
        from end in Keyword("END")
        select (Expression)new CaseExpression(operand, whens.ToList(), elseClause);

    /// <summary>
    /// スカラサブクエリ / 括弧付き式
    /// </summary>
    private static Parser<Expression, char> ParenOrSubqueryParser =>
        from open in CharParsers.Char('(')
        from _ in Ws
        from content in SelectStatementParser.Value.Try().Select(q => (Expression)new ScalarSubquery(q))
            .Or(ExpressionParser.Value)
        from __ in Ws
        from close in CharParsers.Char(')')
        select content;

    /// <summary>
    /// EXISTS式
    /// </summary>
    private static Parser<Expression, char> ExistsParser =>
        from notKw in Keyword("NOT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
        from _ in Ws
        from __ in Keyword("EXISTS")
        from ___ in Ws
        from open in CharParsers.Char('(')
        from ____ in Ws
        from query in SelectStatementParser.Value
        from _____ in Ws
        from close in CharParsers.Char(')')
        select (Expression)new ExistsExpression(query, notKw);

    /// <summary>
    /// プライマリ式
    /// </summary>
    private static Parser<Expression, char> PrimaryExpr =>
        Parsers.Choice(
            ExistsParser.Try(),
            CaseParser.Try(),
            FunctionCallParser.Try(),
            ParenOrSubqueryParser.Try(),
            NullLiteralParser.Try(),
            BoolLiteralParser.Try(),
            FloatLiteral.Try(),
            IntegerLiteral.Try(),
            StringLiteralParser.Try(),
            WildcardParser.Try(),
            ColumnRefParser
        );

    /// <summary>
    /// 単項式
    /// </summary>
    private static Parser<Expression, char> UnaryExpr =>
        (
            from op in Parsers.Choice(
                Keyword("NOT").Select(_ => "NOT"),
                CharParsers.Char('-').Select(_ => "-"),
                CharParsers.Char('+').Select(_ => "+")
            )
            from _ in Ws
            from expr in UnaryExprLazy
            select (Expression)new UnaryOp(op, expr)
        ).Try().Or(PrimaryExpr);

    private static Parser<Expression, char> UnaryExprLazy => UnaryExpr;

    /// <summary>
    /// IS NULL / IS NOT NULL / IN / BETWEEN / LIKE
    /// </summary>
    private static Parser<Expression, char> PostfixExpr =>
        from primary in UnaryExpr
        from _ in Ws
        from result in PostfixSuffix(primary)
        select result;

    private static Parser<Expression, char> PostfixSuffix(Expression primary)
    {
        return Parsers.Choice(
            // IS NULL / IS NOT NULL
            (
                from _ in Keyword("IS")
                from __ in Ws
                from notKw in Keyword("NOT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
                from ___ in Ws
                from ____ in Keyword("NULL")
                select (Expression)new IsNullExpression(primary, notKw)
            ).Try(),
            // NOT IN / IN
            (
                from notKw in Keyword("NOT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
                from _ in Ws
                from __ in Keyword("IN")
                from ___ in Ws
                from open in CharParsers.Char('(')
                from ____ in Ws
                from content in SelectStatementParser.Value.Try().Select(q => (object)q)
                    .Or(ExpressionParser.Value.SepBy1(from c in CharParsers.Char(',') from w in Ws select c).Select(l => (object)l))
                from _____ in Ws
                from close in CharParsers.Char(')')
                select content is SelectStatement sq
                    ? (Expression)new InExpression(primary, null, sq, notKw)
                    : (Expression)new InExpression(primary, (IReadOnlyList<Expression>)content, null, notKw)
            ).Try(),
            // NOT BETWEEN / BETWEEN
            (
                from notKw in Keyword("NOT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
                from _ in Ws
                from __ in Keyword("BETWEEN")
                from ___ in Ws
                from low in UnaryExpr
                from ____ in Ws
                from _____ in Keyword("AND")
                from ______ in Ws
                from high in UnaryExpr
                select (Expression)new BetweenExpression(primary, low, high, notKw)
            ).Try(),
            // NOT LIKE / LIKE
            (
                from notKw in Keyword("NOT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
                from _ in Ws
                from __ in Keyword("LIKE")
                from ___ in Ws
                from pattern in UnaryExpr
                select (Expression)new LikeExpression(primary, pattern, notKw)
            ).Try(),
            Parsers.Return<Expression, char>(primary)
        );
    }

    /// <summary>
    /// 比較式
    /// </summary>
    private static Parser<Expression, char> ComparisonExpr =>
        from left in PostfixExpr
        from _ in Ws
        from rest in (
            from op in Parsers.Choice(
                CharParsers.String("<=").Try().Select(_ => "<="),
                CharParsers.String(">=").Try().Select(_ => ">="),
                CharParsers.String("<>").Try().Select(_ => "<>"),
                CharParsers.String("!=").Try().Select(_ => "!="),
                CharParsers.Char('<').Select(_ => "<"),
                CharParsers.Char('>').Select(_ => ">"),
                CharParsers.Char('=').Select(_ => "=")
            )
            from __ in Ws
            from right in PostfixExpr
            select (op, right)
        ).Try().Select(t => ((string, Expression)?)t).Or(Parsers.Return<(string, Expression)?, char>(null))
        select rest.HasValue ? (Expression)new BinaryOp(left, rest.Value.Item1, rest.Value.Item2) : left;

    /// <summary>
    /// 乗除算
    /// </summary>
    private static Parser<Expression, char> MultiplicativeExpr =>
        from first in ComparisonExpr
        from _ in Ws
        from rest in (
            from op in Parsers.Choice(
                CharParsers.Char('*').Select(_ => "*"),
                CharParsers.Char('/').Select(_ => "/"),
                CharParsers.Char('%').Select(_ => "%")
            )
            from __ in Ws
            from right in ComparisonExpr
            from ___ in Ws
            select (op, right)
        ).Many()
        select rest.Aggregate(first, (acc, x) => (Expression)new BinaryOp(acc, x.op, x.right));

    /// <summary>
    /// 加減算
    /// </summary>
    private static Parser<Expression, char> AdditiveExpr =>
        from first in MultiplicativeExpr
        from _ in Ws
        from rest in (
            from op in Parsers.Choice(
                CharParsers.String("||").Select(_ => "||"),
                CharParsers.Char('+').Select(_ => "+"),
                CharParsers.Char('-').Select(_ => "-")
            )
            from __ in Ws
            from right in MultiplicativeExpr
            from ___ in Ws
            select (op, right)
        ).Many()
        select rest.Aggregate(first, (acc, x) => (Expression)new BinaryOp(acc, x.op, x.right));

    /// <summary>
    /// AND式
    /// </summary>
    private static Parser<Expression, char> AndExpr =>
        from first in AdditiveExpr
        from _ in Ws
        from rest in (
            from __ in Keyword("AND")
            from ___ in Ws
            from right in AdditiveExpr
            from ____ in Ws
            select right
        ).Try().Many()
        select rest.Aggregate(first, (acc, x) => (Expression)new BinaryOp(acc, "AND", x));

    /// <summary>
    /// OR式
    /// </summary>
    private static Parser<Expression, char> OrExpr =>
        from first in AndExpr
        from _ in Ws
        from rest in (
            from __ in Keyword("OR")
            from ___ in Ws
            from right in AndExpr
            from ____ in Ws
            select right
        ).Try().Many()
        select rest.Aggregate(first, (acc, x) => (Expression)new BinaryOp(acc, "OR", x));

    /// <summary>
    /// 式パーサー（遅延評価用のラッパー）
    /// </summary>
    private static class ExpressionParser
    {
        public static Parser<Expression, char> Value => OrExpr;
    }

    /// <summary>
    /// 式パーサー（公開用）
    /// </summary>
    public static Parser<Expression, char> ExpressionParserPublic => OrExpr;

    /// <summary>
    /// SELECT項目
    /// </summary>
    private static Parser<SelectItem, char> SelectItemParser =>
        from expr in ExpressionParser.Value
        from _ in Ws
        from alias in (
            from a in Keyword("AS")
            from w in Ws
            from name in AnyIdentifier
            select name
        ).Try().Or(
            Identifier.Try().Where(name => !IsReservedWord(name))
        ).Try().Select(s => (string?)s).Or(Parsers.Return<string?, char>(null))
        select new SelectItem(expr, alias);

    /// <summary>
    /// テーブル名
    /// </summary>
    private static Parser<TableReference, char> TableNameParser =>
        from first in AnyIdentifier
        from _ in Ws
        from second in (
            from dot in CharParsers.Char('.')
            from __ in Ws
            from name in AnyIdentifier
            select name
        ).Try().Select(s => (string?)s).Or(Parsers.Return<string?, char>(null))
        from ___ in Ws
        from alias in (
            from a in Keyword("AS").Try().Or(Parsers.Return<string, char>(""))
            from w in Ws
            from name in Identifier.Try().Where(n => !IsReservedWord(n))
            select name
        ).Try().Select(s => (string?)s).Or(Parsers.Return<string?, char>(null))
        select (TableReference)(second != null
            ? new TableName(first, second, alias)
            : new TableName("", first, alias));

    /// <summary>
    /// サブクエリテーブル
    /// </summary>
    private static Parser<TableReference, char> SubqueryTableParser =>
        from open in CharParsers.Char('(')
        from _ in Ws
        from query in SelectStatementParser.Value
        from __ in Ws
        from close in CharParsers.Char(')')
        from ___ in Ws
        from ____ in Keyword("AS").Try().Or(Parsers.Return<string, char>(""))
        from _____ in Ws
        from alias in AnyIdentifier
        select (TableReference)new SubqueryTable(query, alias);

    /// <summary>
    /// プライマリテーブル参照
    /// </summary>
    private static Parser<TableReference, char> PrimaryTableRef =>
        SubqueryTableParser.Try().Or(TableNameParser);

    /// <summary>
    /// JOIN種別
    /// </summary>
    private static Parser<JoinType, char> JoinTypeParser =>
        Parsers.Choice(
            (from _ in Keyword("INNER") from __ in Ws from ___ in Keyword("JOIN") select JoinType.Inner).Try(),
            (from _ in Keyword("LEFT") from __ in Ws from ___ in Keyword("OUTER").Try().Or(Parsers.Return<string, char>("")) from ____ in Ws from _____ in Keyword("JOIN") select JoinType.Left).Try(),
            (from _ in Keyword("RIGHT") from __ in Ws from ___ in Keyword("OUTER").Try().Or(Parsers.Return<string, char>("")) from ____ in Ws from _____ in Keyword("JOIN") select JoinType.Right).Try(),
            (from _ in Keyword("FULL") from __ in Ws from ___ in Keyword("OUTER").Try().Or(Parsers.Return<string, char>("")) from ____ in Ws from _____ in Keyword("JOIN") select JoinType.Full).Try(),
            (from _ in Keyword("CROSS") from __ in Ws from ___ in Keyword("JOIN") select JoinType.Cross).Try(),
            Keyword("JOIN").Select(_ => JoinType.Inner)
        );

    /// <summary>
    /// JOINパース結果
    /// </summary>
    private record JoinPart(JoinType Type, TableReference Table, Expression? OnCondition);

    /// <summary>
    /// JOIN
    /// </summary>
    private static Parser<JoinPart, char> JoinParser =>
        from type in JoinTypeParser
        from _ in Ws
        from table in PrimaryTableRef
        from __ in Ws
        from onCond in (
            from ___ in Keyword("ON")
            from ____ in Ws
            from expr in ExpressionParser.Value
            select expr
        ).Try().Select(e => (Expression?)e).Or(Parsers.Return<Expression?, char>(null))
        select new JoinPart(type, table, onCond);

    /// <summary>
    /// テーブル参照（JOIN含む）
    /// </summary>
    private static Parser<TableReference, char> TableRefParser =>
        from primary in PrimaryTableRef
        from _ in Ws
        from joins in JoinParser.Many()
        select joins.Aggregate(primary, (acc, j) => new JoinedTable(acc, j.Type, j.Table, j.OnCondition));

    /// <summary>
    /// FROM句
    /// </summary>
    private static Parser<FromClause?, char> FromClauseParser =>
        (
            from _ in Keyword("FROM")
            from __ in Ws
            from table in TableRefParser
            select (FromClause?)new FromClause(table)
        ).Try().Or(Parsers.Return<FromClause?, char>(null));

    /// <summary>
    /// WHERE句
    /// </summary>
    private static Parser<Expression?, char> WhereClauseParser =>
        (
            from _ in Keyword("WHERE")
            from __ in Ws
            from expr in ExpressionParser.Value
            select (Expression?)expr
        ).Try().Or(Parsers.Return<Expression?, char>(null));

    /// <summary>
    /// GROUP BY句
    /// </summary>
    private static Parser<IReadOnlyList<Expression>?, char> GroupByClauseParser =>
        (
            from _ in Keyword("GROUP")
            from __ in Ws
            from ___ in Keyword("BY")
            from ____ in Ws
            from exprs in ExpressionParser.Value.SepBy1(
                from c in CharParsers.Char(',')
                from w in Ws
                select c
            )
            select (IReadOnlyList<Expression>?)exprs.ToList()
        ).Try().Or(Parsers.Return<IReadOnlyList<Expression>?, char>(null));

    /// <summary>
    /// HAVING句
    /// </summary>
    private static Parser<Expression?, char> HavingClauseParser =>
        (
            from _ in Keyword("HAVING")
            from __ in Ws
            from expr in ExpressionParser.Value
            select (Expression?)expr
        ).Try().Or(Parsers.Return<Expression?, char>(null));

    /// <summary>
    /// ORDER BY項目
    /// </summary>
    private static Parser<OrderByItem, char> OrderByItemParser =>
        from expr in ExpressionParser.Value
        from _ in Ws
        from desc in (
            Keyword("DESC").Select(_ => true)
            .Or(Keyword("ASC").Select(_ => false))
        ).Try().Select(b => (bool?)b).Or(Parsers.Return<bool?, char>(null))
        select new OrderByItem(expr, desc ?? false);

    /// <summary>
    /// ORDER BY句
    /// </summary>
    private static Parser<IReadOnlyList<OrderByItem>?, char> OrderByClauseParser =>
        (
            from _ in Keyword("ORDER")
            from __ in Ws
            from ___ in Keyword("BY")
            from ____ in Ws
            from items in OrderByItemParser.SepBy1(
                from c in CharParsers.Char(',')
                from w in Ws
                select c
            )
            select (IReadOnlyList<OrderByItem>?)items.ToList()
        ).Try().Or(Parsers.Return<IReadOnlyList<OrderByItem>?, char>(null));

    /// <summary>
    /// LIMIT句
    /// </summary>
    private static Parser<int?, char> LimitClauseParser =>
        (
            from _ in Keyword("LIMIT")
            from __ in Ws
            from digits in CharParsers.Digit.Many1()
            select (int?)int.Parse(new string(digits.ToArray()))
        ).Try().Or(Parsers.Return<int?, char>(null));

    /// <summary>
    /// OFFSET句
    /// </summary>
    private static Parser<int?, char> OffsetClauseParser =>
        (
            from _ in Keyword("OFFSET")
            from __ in Ws
            from digits in CharParsers.Digit.Many1()
            select (int?)int.Parse(new string(digits.ToArray()))
        ).Try().Or(Parsers.Return<int?, char>(null));

    /// <summary>
    /// SELECT文パーサー（遅延評価用のラッパー）
    /// </summary>
    private static class SelectStatementParser
    {
        public static Parser<SelectStatement, char> Value => SelectParser;
    }

    /// <summary>
    /// SELECT文
    /// </summary>
    private static Parser<SelectStatement, char> SelectParser =>
        from _ in Keyword("SELECT")
        from __ in Ws
        from distinct in Keyword("DISTINCT").Try().Select(_ => true).Or(Parsers.Return<bool, char>(false))
        from ___ in Ws
        from columns in SelectItemParser.SepBy1(
            from c in CharParsers.Char(',')
            from w in Ws
            select c
        )
        from ____ in Ws
        from fromClause in FromClauseParser
        from _____ in Ws
        from whereClause in WhereClauseParser
        from ______ in Ws
        from groupBy in GroupByClauseParser
        from _______ in Ws
        from having in HavingClauseParser
        from ________ in Ws
        from orderBy in OrderByClauseParser
        from _________ in Ws
        from limit in LimitClauseParser
        from __________ in Ws
        from offset in OffsetClauseParser
        select new SelectStatement(
            distinct,
            columns.ToList(),
            fromClause,
            whereClause,
            groupBy,
            having,
            orderBy,
            limit,
            offset
        );

    #endregion

    #region 公開API

    /// <summary>
    /// SQL SELECT文をパースします
    /// </summary>
    public static SelectStatement Parse(string sql)
    {
        var parser =
            from _ in Ws
            from stmt in SelectParser
            from __ in Ws
            from ___ in Parsers.Eof<char>()
            select stmt;

        var result = parser.Parse(new StringInputStream(sql));
        return result.GetValueOrThrow();
    }

    /// <summary>
    /// SQL SELECT文をパースし、結果を返します（例外なし）
    /// </summary>
    public static ParseResult<SelectStatement, char> TryParse(string sql)
    {
        var parser =
            from _ in Ws
            from stmt in SelectParser
            from __ in Ws
            from ___ in Parsers.Eof<char>()
            select stmt;

        return parser.Parse(new StringInputStream(sql));
    }

    #endregion
}
