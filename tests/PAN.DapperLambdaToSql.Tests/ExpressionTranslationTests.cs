namespace PAN.DapperLambdaToSql.Tests;

public class ExpressionTranslationTests
{
    [Fact]
    public void ToSql_WhenComparingToAConstant_ShouldParameterizeTheValue()
    {
        var (sql, parameters) = DapperHelper.ToSql<User>(u => u.Name == "Pedro");

        Assert.Equal("Name = @param0", sql);
        Assert.Equal("Pedro", parameters.Get<string>("@param0"));
    }

    [Fact]
    public void ToSql_WhenComparingToACapturedVariable_ShouldParameterizeItsValue()
    {
        var expected = "pedro@example.com";

        var (sql, parameters) = DapperHelper.ToSql<User>(u => u.Name == expected);

        Assert.Equal("Name = @param0", sql);
        Assert.Equal(expected, parameters.Get<string>("@param0"));
    }

    [Fact]
    public void ToSql_WhenPredicateUsesAndAlso_ShouldJoinBothSides()
    {
        var (sql, parameters) = DapperHelper.ToSql<User>(u => u.Name == "Pedro" && u.RoleId == 2);

        Assert.Equal("Name = @param0 AND RoleId = @param1", sql);
        Assert.Equal("Pedro", parameters.Get<string>("@param0"));
        Assert.Equal(2, parameters.Get<int>("@param1"));
    }

    [Fact]
    public void ToSql_WithSqlServerDialect_ShouldDelimitTheColumn()
    {
        var (sql, _) = DapperHelper.ToSql<Role>(r => r.Key == "ADMIN", SqlDialects.SqlServer);

        Assert.Equal("[Key] = @param0", sql);
    }

    [Fact]
    public void ToSql_WithMySqlDialect_ShouldDelimitTheColumn()
    {
        var (sql, _) = DapperHelper.ToSql<Role>(r => r.Key == "ADMIN", SqlDialects.MySql);

        Assert.Equal("`Key` = @param0", sql);
    }

    [Fact]
    public void ToSql_WithoutDialect_ShouldKeepTheColumnUndelimited()
    {
        var (sql, _) = DapperHelper.ToSql<Role>(r => r.Key == "ADMIN");

        Assert.Equal("Key = @param0", sql);
    }

    [Theory]
    [InlineData("mayor")]
    [InlineData("distinto")]
    [InlineData("o-logico")]
    public void ToSql_WhenOperatorIsNotSupported_ShouldThrowNotSupported(string caso)
    {
        Func<object> act = caso switch
        {
            "mayor" => () => DapperHelper.ToSql<User>(u => u.RoleId > 2),
            "distinto" => () => DapperHelper.ToSql<User>(u => u.RoleId != 2),
            _ => () => DapperHelper.ToSql<User>(u => u.RoleId == 2 || u.RoleId == 3),
        };

        Assert.Throws<NotSupportedException>(act);
    }
}
