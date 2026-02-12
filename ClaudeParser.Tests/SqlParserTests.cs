using ClaudeParser.Examples;
using ClaudeParser.Core;

namespace ClaudeParser.Tests;

/// <summary>
/// SQLパーサーのテスト
/// </summary>
public class SqlParserTests
{
    #region 基本的なSELECT文

    [Fact]
    public void SqlParser_ShouldParseSimpleSelectStar()
    {
        var sql = "SELECT * FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.Single(result.Columns);
        Assert.IsType<SqlParser.Wildcard>(result.Columns[0].Expr);
        Assert.NotNull(result.From);
        Assert.IsType<SqlParser.TableName>(result.From!.Table);
        Assert.Equal("users", ((SqlParser.TableName)result.From.Table).Name);
    }

    [Fact]
    public void SqlParser_ShouldParseSelectWithColumns()
    {
        var sql = "SELECT id, name, age FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.Equal(3, result.Columns.Count);
        Assert.IsType<SqlParser.ColumnRef>(result.Columns[0].Expr);
        Assert.Equal("id", ((SqlParser.ColumnRef)result.Columns[0].Expr).ColumnName);
    }

    [Fact]
    public void SqlParser_ShouldParseSelectDistinct()
    {
        var sql = "SELECT DISTINCT name FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.True(result.Distinct);
    }

    [Fact]
    public void SqlParser_ShouldParseSelectWithAlias()
    {
        var sql = "SELECT name AS user_name FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.Equal("user_name", result.Columns[0].Alias);
    }

    [Fact]
    public void SqlParser_ShouldParseSelectWithTableAlias()
    {
        var sql = "SELECT * FROM users u";
        var result = SqlParser.Parse(sql);
        
        var table = (SqlParser.TableName)result.From!.Table;
        Assert.Equal("users", table.Name);
        Assert.Equal("u", table.Alias);
    }

    #endregion

    #region WHERE句

    [Fact]
    public void SqlParser_ShouldParseSimpleWhereClause()
    {
        var sql = "SELECT * FROM users WHERE age > 18";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.Where);
        Assert.IsType<SqlParser.BinaryOp>(result.Where);
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.Equal(">", binOp.Operator);
    }

    [Fact]
    public void SqlParser_ShouldParseWhereWithAnd()
    {
        var sql = "SELECT * FROM users WHERE age > 18 AND status = 'active'";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.Where);
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.Equal("AND", binOp.Operator);
    }

    [Fact]
    public void SqlParser_ShouldParseWhereWithOr()
    {
        var sql = "SELECT * FROM users WHERE status = 'active' OR status = 'pending'";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.Where);
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.Equal("OR", binOp.Operator);
    }

    [Fact]
    public void SqlParser_ShouldParseWhereWithNot()
    {
        var sql = "SELECT * FROM users WHERE NOT deleted";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.Where);
        Assert.IsType<SqlParser.UnaryOp>(result.Where);
        var unaryOp = (SqlParser.UnaryOp)result.Where!;
        Assert.Equal("NOT", unaryOp.Operator);
    }

    [Fact]
    public void SqlParser_ShouldParseWhereWithParentheses()
    {
        var sql = "SELECT * FROM users WHERE (age > 18 AND age < 65) OR admin = true";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.Where);
    }

    #endregion

    #region リテラル

    [Fact]
    public void SqlParser_ShouldParseIntegerLiteral()
    {
        var sql = "SELECT * FROM users WHERE age = 25";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.IntLiteral>(binOp.Right);
        Assert.Equal(25L, ((SqlParser.IntLiteral)binOp.Right).Value);
    }

    [Fact]
    public void SqlParser_ShouldParseFloatLiteral()
    {
        var sql = "SELECT * FROM items WHERE price = 19.99";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.DoubleLiteral>(binOp.Right);
        Assert.Equal(19.99, ((SqlParser.DoubleLiteral)binOp.Right).Value);
    }

    [Fact]
    public void SqlParser_ShouldParseStringLiteral()
    {
        var sql = "SELECT * FROM users WHERE name = 'Alice'";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.StringLiteral>(binOp.Right);
        Assert.Equal("Alice", ((SqlParser.StringLiteral)binOp.Right).Value);
    }

    [Fact]
    public void SqlParser_ShouldParseTrueLiteral()
    {
        var sql = "SELECT * FROM users WHERE active = true";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.BoolLiteral>(binOp.Right);
        Assert.True(((SqlParser.BoolLiteral)binOp.Right).Value);
    }

    [Fact]
    public void SqlParser_ShouldParseFalseLiteral()
    {
        var sql = "SELECT * FROM users WHERE active = false";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.BoolLiteral>(binOp.Right);
        Assert.False(((SqlParser.BoolLiteral)binOp.Right).Value);
    }

    [Fact]
    public void SqlParser_ShouldParseNullLiteral()
    {
        var sql = "SELECT * FROM users WHERE email = null";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.NullLiteral>(binOp.Right);
    }

    #endregion

    #region JOINs

    [Fact]
    public void SqlParser_ShouldParseInnerJoin()
    {
        var sql = "SELECT * FROM users INNER JOIN orders ON users.id = orders.user_id";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.From);
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        var join = (SqlParser.JoinedTable)result.From.Table;
        Assert.Equal(SqlParser.JoinType.Inner, join.Type);
    }

    [Fact]
    public void SqlParser_ShouldParseLeftJoin()
    {
        var sql = "SELECT * FROM users LEFT JOIN orders ON users.id = orders.user_id";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        var join = (SqlParser.JoinedTable)result.From.Table;
        Assert.Equal(SqlParser.JoinType.Left, join.Type);
    }

    [Fact]
    public void SqlParser_ShouldParseRightJoin()
    {
        var sql = "SELECT * FROM users RIGHT JOIN orders ON users.id = orders.user_id";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        var join = (SqlParser.JoinedTable)result.From.Table;
        Assert.Equal(SqlParser.JoinType.Right, join.Type);
    }

    [Fact]
    public void SqlParser_ShouldParseFullJoin()
    {
        var sql = "SELECT * FROM users FULL JOIN orders ON users.id = orders.user_id";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        var join = (SqlParser.JoinedTable)result.From.Table;
        Assert.Equal(SqlParser.JoinType.Full, join.Type);
    }

    [Fact]
    public void SqlParser_ShouldParseCrossJoin()
    {
        var sql = "SELECT * FROM users CROSS JOIN products";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        var join = (SqlParser.JoinedTable)result.From.Table;
        Assert.Equal(SqlParser.JoinType.Cross, join.Type);
        Assert.Null(join.On); // CROSS JOINにはON句がない
    }

    [Fact]
    public void SqlParser_ShouldParseLeftOuterJoin()
    {
        var sql = "SELECT * FROM users LEFT OUTER JOIN orders ON users.id = orders.user_id";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        var join = (SqlParser.JoinedTable)result.From.Table;
        Assert.Equal(SqlParser.JoinType.Left, join.Type);
    }

    [Fact]
    public void SqlParser_ShouldParseMultipleJoins()
    {
        var sql = "SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id LEFT JOIN products p ON o.product_id = p.id";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
    }

    #endregion

    #region サブクエリ

    [Fact]
    public void SqlParser_ShouldParseSubqueryInFrom()
    {
        var sql = "SELECT * FROM (SELECT id, name FROM users) AS subquery";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.SubqueryTable>(result.From!.Table);
        var subquery = (SqlParser.SubqueryTable)result.From.Table;
        Assert.Equal("subquery", subquery.Alias);
        Assert.NotNull(subquery.Query);
    }

    [Fact]
    public void SqlParser_ShouldParseScalarSubquery()
    {
        var sql = "SELECT name, (SELECT COUNT(*) FROM orders WHERE orders.user_id = users.id) AS order_count FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.Equal(2, result.Columns.Count);
        Assert.IsType<SqlParser.ScalarSubquery>(result.Columns[1].Expr);
    }

    [Fact]
    public void SqlParser_ShouldParseExistsSubquery()
    {
        var sql = "SELECT * FROM users WHERE EXISTS (SELECT 1 FROM orders WHERE orders.user_id = users.id)";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.ExistsExpression>(result.Where);
    }

    [Fact]
    public void SqlParser_ShouldParseInSubquery()
    {
        var sql = "SELECT * FROM users WHERE id IN (SELECT user_id FROM active_users)";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.InExpression>(result.Where);
        var inExpr = (SqlParser.InExpression)result.Where!;
        Assert.NotNull(inExpr.Subquery);
    }

    #endregion

    #region GROUP BY / HAVING

    [Fact]
    public void SqlParser_ShouldParseGroupBy()
    {
        var sql = "SELECT category, COUNT(*) FROM products GROUP BY category";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.GroupBy);
        Assert.Single(result.GroupBy!);
        Assert.IsType<SqlParser.ColumnRef>(result.GroupBy![0]);
    }

    [Fact]
    public void SqlParser_ShouldParseGroupByMultipleColumns()
    {
        var sql = "SELECT category, status, COUNT(*) FROM products GROUP BY category, status";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.GroupBy);
        Assert.Equal(2, result.GroupBy!.Count);
    }

    [Fact]
    public void SqlParser_ShouldParseHaving()
    {
        var sql = "SELECT category, COUNT(*) AS cnt FROM products GROUP BY category HAVING COUNT(*) > 5";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.Having);
        Assert.IsType<SqlParser.BinaryOp>(result.Having);
    }

    #endregion

    #region ORDER BY

    [Fact]
    public void SqlParser_ShouldParseOrderBy()
    {
        var sql = "SELECT * FROM users ORDER BY name";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.OrderBy);
        Assert.Single(result.OrderBy!);
        Assert.False(result.OrderBy![0].Descending);
    }

    [Fact]
    public void SqlParser_ShouldParseOrderByAsc()
    {
        var sql = "SELECT * FROM users ORDER BY name ASC";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.OrderBy);
        Assert.Single(result.OrderBy!);
        Assert.False(result.OrderBy![0].Descending);
    }

    [Fact]
    public void SqlParser_ShouldParseOrderByDesc()
    {
        var sql = "SELECT * FROM users ORDER BY created_at DESC";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.OrderBy);
        Assert.Single(result.OrderBy!);
        Assert.True(result.OrderBy![0].Descending);
    }

    [Fact]
    public void SqlParser_ShouldParseOrderByMultipleColumns()
    {
        var sql = "SELECT * FROM users ORDER BY last_name ASC, first_name DESC";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result.OrderBy);
        Assert.Equal(2, result.OrderBy!.Count);
        Assert.False(result.OrderBy[0].Descending);
        Assert.True(result.OrderBy[1].Descending);
    }

    #endregion

    #region LIMIT / OFFSET

    [Fact]
    public void SqlParser_ShouldParseLimit()
    {
        var sql = "SELECT * FROM users LIMIT 10";
        var result = SqlParser.Parse(sql);
        
        Assert.Equal(10, result.Limit);
        Assert.Null(result.Offset);
    }

    [Fact]
    public void SqlParser_ShouldParseLimitWithOffset()
    {
        var sql = "SELECT * FROM users LIMIT 10 OFFSET 20";
        var result = SqlParser.Parse(sql);
        
        Assert.Equal(10, result.Limit);
        Assert.Equal(20, result.Offset);
    }

    #endregion

    #region 式

    [Fact]
    public void SqlParser_ShouldParseFunctionCall()
    {
        var sql = "SELECT COUNT(*) FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.FunctionCall>(result.Columns[0].Expr);
        var func = (SqlParser.FunctionCall)result.Columns[0].Expr;
        Assert.Equal("COUNT", func.Name);
    }

    [Fact]
    public void SqlParser_ShouldParseFunctionWithMultipleArgs()
    {
        var sql = "SELECT COALESCE(name, 'Unknown') FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.FunctionCall>(result.Columns[0].Expr);
        var func = (SqlParser.FunctionCall)result.Columns[0].Expr;
        Assert.Equal("COALESCE", func.Name);
        Assert.Equal(2, func.Args.Count);
    }

    [Fact]
    public void SqlParser_ShouldParseCaseExpression()
    {
        var sql = "SELECT CASE WHEN age < 18 THEN 'minor' WHEN age < 65 THEN 'adult' ELSE 'senior' END FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.CaseExpression>(result.Columns[0].Expr);
        var caseExpr = (SqlParser.CaseExpression)result.Columns[0].Expr;
        Assert.Equal(2, caseExpr.WhenClauses.Count);
        Assert.NotNull(caseExpr.Else);
    }

    [Fact]
    public void SqlParser_ShouldParseBetween()
    {
        var sql = "SELECT * FROM products WHERE price BETWEEN 10 AND 100";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.BetweenExpression>(result.Where);
        var between = (SqlParser.BetweenExpression)result.Where!;
        Assert.False(between.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseNotBetween()
    {
        var sql = "SELECT * FROM products WHERE price NOT BETWEEN 10 AND 100";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.BetweenExpression>(result.Where);
        var between = (SqlParser.BetweenExpression)result.Where!;
        Assert.True(between.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseInList()
    {
        var sql = "SELECT * FROM users WHERE status IN ('active', 'pending')";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.InExpression>(result.Where);
        var inExpr = (SqlParser.InExpression)result.Where!;
        Assert.NotNull(inExpr.Values);
        Assert.Equal(2, inExpr.Values!.Count);
    }

    [Fact]
    public void SqlParser_ShouldParseNotIn()
    {
        var sql = "SELECT * FROM users WHERE status NOT IN ('deleted', 'banned')";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.InExpression>(result.Where);
        var inExpr = (SqlParser.InExpression)result.Where!;
        Assert.True(inExpr.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseLike()
    {
        var sql = "SELECT * FROM users WHERE name LIKE '%john%'";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.LikeExpression>(result.Where);
        var like = (SqlParser.LikeExpression)result.Where!;
        Assert.False(like.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseNotLike()
    {
        var sql = "SELECT * FROM users WHERE name NOT LIKE '%test%'";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.LikeExpression>(result.Where);
        var like = (SqlParser.LikeExpression)result.Where!;
        Assert.True(like.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseIsNull()
    {
        var sql = "SELECT * FROM users WHERE email IS NULL";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.IsNullExpression>(result.Where);
        var isNull = (SqlParser.IsNullExpression)result.Where!;
        Assert.False(isNull.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseIsNotNull()
    {
        var sql = "SELECT * FROM users WHERE email IS NOT NULL";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.IsNullExpression>(result.Where);
        var isNull = (SqlParser.IsNullExpression)result.Where!;
        Assert.True(isNull.Not);
    }

    [Fact]
    public void SqlParser_ShouldParseQualifiedColumnName()
    {
        var sql = "SELECT users.name FROM users";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.ColumnRef>(result.Columns[0].Expr);
        var col = (SqlParser.ColumnRef)result.Columns[0].Expr;
        Assert.Equal("users", col.TableName);
        Assert.Equal("name", col.ColumnName);
    }

    [Fact]
    public void SqlParser_ShouldParseNegativeNumber()
    {
        var sql = "SELECT * FROM items WHERE quantity > -5";
        var result = SqlParser.Parse(sql);
        
        var binOp = (SqlParser.BinaryOp)result.Where!;
        Assert.IsType<SqlParser.UnaryOp>(binOp.Right);
    }

    #endregion

    #region ToSql再生成

    [Fact]
    public void SqlParser_ToSql_ShouldRegenerateSimpleSelect()
    {
        var sql = "SELECT * FROM users";
        var result = SqlParser.Parse(sql);
        var regenerated = result.ToSql();
        
        Assert.Contains("SELECT", regenerated);
        Assert.Contains("*", regenerated);
        Assert.Contains("FROM", regenerated);
        Assert.Contains("users", regenerated);
    }

    [Fact]
    public void SqlParser_ToSql_ShouldRegenerateComplexQuery()
    {
        var sql = "SELECT u.name, o.total FROM users u INNER JOIN orders o ON u.id = o.user_id WHERE o.total > 100 ORDER BY o.total DESC LIMIT 10";
        var result = SqlParser.Parse(sql);
        var regenerated = result.ToSql();
        
        Assert.Contains("SELECT", regenerated);
        Assert.Contains("INNER JOIN", regenerated);
        Assert.Contains("WHERE", regenerated);
        Assert.Contains("ORDER BY", regenerated);
        Assert.Contains("LIMIT", regenerated);
    }

    [Fact]
    public void SqlParser_Parse_Regenerate_Reparse_ShouldWork()
    {
        var sql = "SELECT id, name FROM users WHERE age > 18 ORDER BY name ASC";
        var result1 = SqlParser.Parse(sql);
        var regenerated = result1.ToSql();
        var result2 = SqlParser.Parse(regenerated);
        
        Assert.Equal(result1.Columns.Count, result2.Columns.Count);
        Assert.Equal(result1.OrderBy?.Count, result2.OrderBy?.Count);
        Assert.NotNull(result2.Where);
    }

    #endregion

    #region TryParse

    [Fact]
    public void SqlParser_TryParse_ShouldReturnSuccessForValidSql()
    {
        var sql = "SELECT * FROM users";
        var result = SqlParser.TryParse(sql);
        
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SqlParser_TryParse_ShouldReturnFailureForInvalidSql()
    {
        var sql = "INVALID SQL";
        var result = SqlParser.TryParse(sql);
        
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region 大文字小文字区別なし

    [Fact]
    public void SqlParser_ShouldParseLowercaseKeywords()
    {
        var sql = "select * from users where age > 18";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result);
        Assert.NotNull(result.From);
    }

    [Fact]
    public void SqlParser_ShouldParseMixedCaseKeywords()
    {
        var sql = "SeLeCt * FrOm users WhErE age > 18";
        var result = SqlParser.Parse(sql);
        
        Assert.NotNull(result);
        Assert.NotNull(result.From);
    }

    #endregion

    #region 複合テスト

    [Fact]
    public void SqlParser_ShouldParseComplexQuery()
    {
        var sql = @"
            SELECT 
                u.name,
                u.email,
                COUNT(o.id) AS order_count,
                SUM(o.total) AS total_spent
            FROM users u
            LEFT JOIN orders o ON u.id = o.user_id
            WHERE u.status = 'active'
            GROUP BY u.id, u.name, u.email
            HAVING COUNT(o.id) > 0
            ORDER BY total_spent DESC
            LIMIT 100";
        
        var result = SqlParser.Parse(sql);
        
        Assert.Equal(4, result.Columns.Count);
        Assert.NotNull(result.From);
        Assert.IsType<SqlParser.JoinedTable>(result.From!.Table);
        Assert.NotNull(result.Where);
        Assert.NotNull(result.GroupBy);
        Assert.Equal(3, result.GroupBy!.Count);
        Assert.NotNull(result.Having);
        Assert.NotNull(result.OrderBy);
        Assert.Single(result.OrderBy!);
        Assert.Equal(100, result.Limit);
    }

    [Fact]
    public void SqlParser_ShouldParseNestedSubquery()
    {
        var sql = "SELECT * FROM (SELECT * FROM (SELECT id FROM users) AS inner_q) AS outer_q";
        var result = SqlParser.Parse(sql);
        
        Assert.IsType<SqlParser.SubqueryTable>(result.From!.Table);
        var outerSub = (SqlParser.SubqueryTable)result.From.Table;
        Assert.IsType<SqlParser.SubqueryTable>(outerSub.Query.From!.Table);
    }

    #endregion
}
