using System.Data.Common;
using PAN.DapperLambdaToSql.Tests.Fakes;

namespace PAN.DapperLambdaToSql.Tests;

public abstract class QueryAsyncSqlTests
{
    protected abstract DbConnection CreateConnection();
    protected abstract string D(string identifier);

    [Fact]
    public async Task QueryAsync_ShouldDelimitTheTableAndTheColumn()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.QueryAsync<User>(connection, u => u.Name == "Pedro");

        Assert.Equal($"SELECT * FROM {D("Users")} WHERE {D("Name")} = @param0", sql);
    }

    [Fact]
    public async Task QueryAsync_WhenPredicateUsesAndAlso_ShouldDelimitBothColumns()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.QueryAsync<User>(connection, u => u.Name == "Pedro" && u.RoleId == 2);

        Assert.Equal(
            $"SELECT * FROM {D("Users")} WHERE {D("Name")} = @param0 AND {D("RoleId")} = @param1",
            sql);
    }

    [Fact]
    public async Task QueryAsync_WhenPredicateUsesOrElseGrouping_ShouldWrapTheOrElseInParens()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.QueryAsync<User>(connection,
            u => u.Name == "Pedro" && (u.RoleId == 2 || u.RoleId == 3));

        Assert.Equal(
            $"SELECT * FROM {D("Users")} WHERE {D("Name")} = @param0 AND ({D("RoleId")} = @param1 OR {D("RoleId")} = @param2)",
            sql);
    }
}

public class QueryAsyncMySqlTests : QueryAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new MySqlConnection();
    protected override string D(string identifier) => $"`{identifier}`";
}

public class QueryAsyncSqlServerTests : QueryAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new SqlConnection();
    protected override string D(string identifier) => $"[{identifier}]";
}
