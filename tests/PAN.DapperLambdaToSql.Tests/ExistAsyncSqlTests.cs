using System.Data.Common;
using NSubstitute;
using PAN.DapperLambdaToSql.Tests.Fakes;

namespace PAN.DapperLambdaToSql.Tests;

public abstract class ExistAsyncSqlTests
{
    protected abstract DbConnection CreateConnection();
    protected abstract string D(string identifier);

    [Fact]
    public async Task ExistAsync_ShouldDelimitTheTableAndTheColumn()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.ExistAsync<User>(connection, u => u.Name == "Pedro");

        Assert.Equal($"SELECT COUNT(*) FROM {D("Users")} WHERE {D("Name")} = @param0", sql);
    }

    [Fact]
    public async Task ExistAsync_WhenColumnIsAReservedWord_ShouldDelimitIt()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.ExistAsync<Role>(connection, r => r.Key == "ADMIN");

        Assert.Equal($"SELECT COUNT(*) FROM {D("Roles")} WHERE {D("Key")} = @param0", sql);
    }

    [Fact]
    public async Task ExistAsync_WhenPredicateUsesAndAlso_ShouldDelimitBothColumns()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.ExistAsync<User>(connection, u => u.Name == "Pedro" && u.RoleId == 2);

        Assert.Equal(
            $"SELECT COUNT(*) FROM {D("Users")} WHERE {D("Name")} = @param0 AND {D("RoleId")} = @param1",
            sql);
    }

    [Fact]
    public async Task ExistAsync_WhenComparingToACapturedVariable_ShouldParameterizeIt()
    {
        using var connection = CreateConnection();
        var name = "Pedro";

        var sql = await SqlCapture.ExistAsync<User>(connection, u => u.Name == name);

        Assert.Equal($"SELECT COUNT(*) FROM {D("Users")} WHERE {D("Name")} = @param0", sql);
    }

    [Fact]
    public async Task ExistAsync_WhenPredicateUsesOrElseGrouping_ShouldDelimitAndParenthesize()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.ExistAsync<User>(connection,
            u => u.Name == "Pedro" && (u.RoleId == 2 || u.RoleId == 3));

        Assert.Equal(
            $"SELECT COUNT(*) FROM {D("Users")} WHERE {D("Name")} = @param0 AND ({D("RoleId")} = @param1 OR {D("RoleId")} = @param2)",
            sql);
    }
}

public class ExistAsyncMySqlTests : ExistAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new MySqlConnection();
    protected override string D(string identifier) => $"`{identifier}`";
}

public class ExistAsyncSqlServerTests : ExistAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new SqlConnection();
    protected override string D(string identifier) => $"[{identifier}]";
}

public class DialectCollaborationTests : IDisposable
{
    public void Dispose() => DapperHelper.Dialect = null;

    [Fact]
    public async Task UpdateAsync_ShouldDelegateEveryIdentifierToTheDialect()
    {
        var dialect = Substitute.For<ISqlDialect>();
        dialect.Delimit(Arg.Any<string>()).Returns(call => $"<{call.Arg<string>()}>");
        DapperHelper.Dialect = dialect;

        using var connection = new MySqlConnection();
        var sql = await SqlCapture.UpdateAsync(connection, new Gym { Id = 1, Name = "Central" });

        Assert.Equal("UPDATE <Gyms> SET <Name> = @Name WHERE <Id> = @Id", sql);

        dialect.Received(1).Delimit("Gyms");
        dialect.Received(1).Delimit("Name");
        dialect.Received(1).Delimit("Id");
    }
}
