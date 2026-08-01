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
    [InlineData("mayorigual")]
    [InlineData("menor")]
    [InlineData("menorigual")]
    [InlineData("distinto")]
    public void ToSql_WhenUsingAComparisonOperator_ShouldTranslateIt(string caso)
    {
        var (sql, expectedOperator) = caso switch
        {
            "mayor" => (DapperHelper.ToSql<User>(u => u.RoleId > 2).Sql, ">"),
            "mayorigual" => (DapperHelper.ToSql<User>(u => u.RoleId >= 2).Sql, ">="),
            "menor" => (DapperHelper.ToSql<User>(u => u.RoleId < 2).Sql, "<"),
            "menorigual" => (DapperHelper.ToSql<User>(u => u.RoleId <= 2).Sql, "<="),
            _ => (DapperHelper.ToSql<User>(u => u.RoleId != 2).Sql, "<>"),
        };

        Assert.Equal($"RoleId {expectedOperator} @param0", sql);
    }

    [Fact]
    public void ToSql_WhenPredicateUsesOrElse_ShouldJoinBothSidesWithOr()
    {
        var (sql, parameters) = DapperHelper.ToSql<User>(u => u.RoleId == 2 || u.RoleId == 3);

        Assert.Equal("RoleId = @param0 OR RoleId = @param1", sql);
        Assert.Equal(2, parameters.Get<int>("@param0"));
        Assert.Equal(3, parameters.Get<int>("@param1"));
    }

    [Fact]
    public void ToSql_WhenAndAlsoWrapsOrElseOnTheRight_ShouldParenthesizeTheOrElse()
    {
        var (sql, _) = DapperHelper.ToSql<User>(u => u.Name == "Pedro" && (u.RoleId == 2 || u.RoleId == 3));

        Assert.Equal("Name = @param0 AND (RoleId = @param1 OR RoleId = @param2)", sql);
    }

    [Fact]
    public void ToSql_WhenAndAlsoWrapsOrElseOnTheLeft_ShouldParenthesizeTheOrElse()
    {
        var (sql, _) = DapperHelper.ToSql<User>(u => (u.RoleId == 2 || u.RoleId == 3) && u.Name == "Pedro");

        Assert.Equal("(RoleId = @param0 OR RoleId = @param1) AND Name = @param2", sql);
    }

    [Fact]
    public void ToSql_WhenOrElseWrapsAndAlso_ShouldNotAddParens()
    {
        var (sql, _) = DapperHelper.ToSql<User>(u => u.Name == "Pedro" || (u.RoleId == 2 && u.GymId == 1));

        Assert.Equal("Name = @param0 OR RoleId = @param1 AND GymId = @param2", sql);
    }

    [Fact]
    public void ToSql_WhenUsingStringContains_ShouldTranslateToLike()
    {
        var (sql, parameters) = DapperHelper.ToSql<User>(u => u.Name!.Contains("Pedro"));

        Assert.Equal("Name LIKE @param0", sql);
        Assert.Equal("%Pedro%", parameters.Get<string>("@param0"));
    }

    [Fact]
    public void ToSql_WhenMethodCallIsNotContains_ShouldThrowNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => DapperHelper.ToSql<User>(u => u.Name!.StartsWith("Pedro")));
    }
}
