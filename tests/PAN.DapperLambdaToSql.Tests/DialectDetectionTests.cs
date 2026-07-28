using PAN.DapperLambdaToSql.Tests.Fakes;

namespace PAN.DapperLambdaToSql.Tests;

public class DialectDetectionTests : IDisposable
{
    public void Dispose() => DapperHelper.Dialect = null;

    [Fact]
    public void GetDialect_WhenConnectionIsSqlConnection_ShouldReturnSqlServer()
    {
        using var connection = new SqlConnection();
        Assert.Same(SqlDialects.SqlServer, DapperHelper.GetDialect(connection));
    }

    [Fact]
    public void GetDialect_WhenConnectionIsMySqlConnection_ShouldReturnMySql()
    {
        using var connection = new MySqlConnection();
        Assert.Same(SqlDialects.MySql, DapperHelper.GetDialect(connection));
    }

    [Fact]
    public void GetDialect_WhenProviderIsUnknown_ShouldFallBackToNoDelimiter()
    {
        // Un proveedor no reconocido conserva el comportamiento previo a multi-motor
        // en lugar de arriesgar los delimitadores del motor equivocado.
        using var connection = new NpgsqlConnection();
        Assert.Same(SqlDialects.None, DapperHelper.GetDialect(connection));
    }

    [Fact]
    public void GetDialect_WhenConnectionIsNull_ShouldFallBackToNoDelimiter()
        => Assert.Same(SqlDialects.None, DapperHelper.GetDialect(null!));

    [Fact]
    public void GetDialect_WhenDialectIsForced_ShouldWinOverDetection()
    {
        DapperHelper.Dialect = SqlDialects.SqlServer;

        using var connection = new MySqlConnection();
        Assert.Same(SqlDialects.SqlServer, DapperHelper.GetDialect(connection));
    }

    [Fact]
    public async Task UpdateAsync_WhenDialectIsForced_ShouldUseItInsteadOfTheConnection()
    {
        DapperHelper.Dialect = SqlDialects.SqlServer;

        using var connection = new MySqlConnection();
        var sql = await SqlCapture.UpdateAsync(connection, new Gym { Id = 1, Name = "Central" });

        Assert.Equal("UPDATE [Gyms] SET [Name] = @Name WHERE [Id] = @Id", sql);
    }
}
