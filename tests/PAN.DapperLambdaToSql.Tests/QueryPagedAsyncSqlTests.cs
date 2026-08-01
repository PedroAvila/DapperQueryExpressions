using System.Data.Common;
using PAN.DapperLambdaToSql.Tests.Fakes;

namespace PAN.DapperLambdaToSql.Tests;

public abstract class QueryPagedAsyncSqlTests
{
    protected abstract DbConnection CreateConnection();
    protected abstract string D(string identifier);

    [Fact]
    public async Task QueryPagedAsync_ShouldGenerateTheSameSqlAsQueryAsync()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.QueryPagedAsync<User>(connection, u => u.Name == "Pedro");

        Assert.Equal($"SELECT * FROM {D("Users")} WHERE {D("Name")} = @param0", sql);
    }

    [Fact]
    public async Task QueryPagedAsync_WhenPredicateUsesAndAlso_ShouldDelimitBothColumns()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.QueryPagedAsync<User>(connection, u => u.Name == "Pedro" && u.RoleId == 2);

        Assert.Equal(
            $"SELECT * FROM {D("Users")} WHERE {D("Name")} = @param0 AND {D("RoleId")} = @param1",
            sql);
    }
}

public class QueryPagedAsyncMySqlTests : QueryPagedAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new MySqlConnection();
    protected override string D(string identifier) => $"`{identifier}`";
}

public class QueryPagedAsyncSqlServerTests : QueryPagedAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new SqlConnection();
    protected override string D(string identifier) => $"[{identifier}]";
}
