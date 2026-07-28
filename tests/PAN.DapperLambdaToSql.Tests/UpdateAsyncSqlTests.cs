using System.Data.Common;
using PAN.DapperLambdaToSql.Tests.Fakes;

namespace PAN.DapperLambdaToSql.Tests;

/// <summary>
/// Batería de SQL generado por <c>UpdateAsync</c>. Los casos se declaran una sola vez y
/// cada motor los hereda; lo único que varía es cómo se delimitan los identificadores.
/// </summary>
public abstract class UpdateAsyncSqlTests
{
    protected abstract DbConnection CreateConnection();
    protected abstract string D(string identifier);

    [Fact]
    public async Task UpdateAsync_ShouldDelimitTableColumnsAndKey()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.UpdateAsync(connection, new Gym { Id = 1, Name = "Central" });

        Assert.Equal($"UPDATE {D("Gyms")} SET {D("Name")} = @Name WHERE {D("Id")} = @Id", sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenColumnIsAReservedWord_ShouldDelimitIt()
    {
        // "Key" es palabra reservada en MySQL y en SQL Server: sin delimitar,
        // esta sentencia es un error de sintaxis en ambos motores.
        using var connection = CreateConnection();
        var role = new Role { Id = 7, Code = 1, GymId = 2, Name = "Admin", Key = "ADMIN", IsSystemRole = true };

        var sql = await SqlCapture.UpdateAsync(connection, role);

        Assert.Equal(
            $"UPDATE {D("Roles")} SET {D("Code")} = @Code, {D("GymId")} = @GymId, {D("Name")} = @Name, " +
            $"{D("Key")} = @Key, {D("IsSystemRole")} = @IsSystemRole WHERE {D("Id")} = @Id",
            sql);
    }

    [Fact]
    public async Task UpdateAsync_ShouldDelimitColumnsButNeverParameters()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.UpdateAsync(connection, new Role { Id = 7, Key = "ADMIN" });

        Assert.Contains($"{D("Key")} = @Key", sql);
        Assert.Contains("@Key", sql);
        Assert.DoesNotContain($"@{D("Key")}", sql);
    }

    [Fact]
    public async Task UpdateAsync_ShouldKeepForeignKeysInTheSetClause()
    {
        using var connection = CreateConnection();
        var user = new User { Id = 3, GymId = 1, Name = "Pedro", RoleId = 2 };

        var sql = await SqlCapture.UpdateAsync(connection, user);

        Assert.Contains($"{D("GymId")} = @GymId", sql);
        Assert.Contains($"{D("RoleId")} = @RoleId", sql);
    }

    [Fact]
    public async Task UpdateAsync_ShouldExcludeThePrimaryKeyFromTheSetClause()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.UpdateAsync(connection, new User { Id = 3, Name = "Pedro", RoleId = 2 });

        var setClause = sql[..sql.IndexOf(" WHERE ", StringComparison.Ordinal)];
        Assert.DoesNotContain($"{D("Id")} =", setClause);
        Assert.EndsWith($"WHERE {D("Id")} = @Id", sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenPropertiesAreNull_ShouldSkipThem()
    {
        using var connection = CreateConnection();

        // Code, GymId y Key quedan en null: es una actualización parcial.
        var sql = await SqlCapture.UpdateAsync(connection, new Role { Id = 7, Name = "Admin" });

        Assert.Equal(
            $"UPDATE {D("Roles")} SET {D("Name")} = @Name, {D("IsSystemRole")} = @IsSystemRole WHERE {D("Id")} = @Id",
            sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenPropertyIsMarkedWriteFalse_ShouldSkipIt()
    {
        using var connection = CreateConnection();
        var user = new User
        {
            Id = 3,
            Name = "Pedro",
            RoleId = 2,
            Role = new Role { Id = 2, Name = "Admin" },
            Gym = new Gym { Id = 1, Name = "Central" },
        };

        var sql = await SqlCapture.UpdateAsync(connection, user);

        // Vienen cargadas desde un JOIN y no son columnas de la tabla.
        Assert.DoesNotContain($"{D("Role")} =", sql);
        Assert.DoesNotContain($"{D("Gym")} =", sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenPropertyIsComputed_ShouldSkipIt()
    {
        using var connection = CreateConnection();
        var employee = new Employee { Id = 1, FirstName = "Ana", FullName = "Ana Pérez" };

        var sql = await SqlCapture.UpdateAsync(connection, employee);

        Assert.Equal($"UPDATE {D("Employees")} SET {D("FirstName")} = @FirstName WHERE {D("Id")} = @Id", sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenKeyIsExplicit_ShouldUseItInTheWhereClause()
    {
        using var connection = CreateConnection();
        var product = new Product { Id = 4, Sku = "TEC-001", Name = "Teclado" };

        var sql = await SqlCapture.UpdateAsync(connection, product);

        // Al no ser Id la clave, Id pasa a ser una columna más del SET.
        Assert.Equal(
            $"UPDATE {D("Products")} SET {D("Id")} = @Id, {D("Name")} = @Name WHERE {D("Sku")} = @Sku",
            sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenTableNameIsQualified_ShouldDelimitEachPart()
    {
        using var connection = CreateConnection();

        var sql = await SqlCapture.UpdateAsync(connection, new QualifiedBranch { Id = 1, Name = "Norte" });

        Assert.StartsWith($"UPDATE {D("dbo")}.{D("Sucursales")} SET", sql);
    }

    [Fact]
    public async Task UpdateAsync_WhenThereIsNothingToUpdate_ShouldReturnFalseWithoutExecuting()
    {
        using var connection = CreateConnection();

        var updated = await connection.UpdateAsync(new Gym { Id = 1 });

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateAsync_WhenEntityHasNoKey_ShouldThrowBeforeBuildingAnySql()
    {
        using var connection = CreateConnection();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => connection.UpdateAsync(new Detail { GymId = 1, CustomerId = 2, Note = "x" }));

        Assert.Contains("Detail", exception.Message);
    }
}

public class UpdateAsyncMySqlTests : UpdateAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new MySqlConnection();
    protected override string D(string identifier) => $"`{identifier}`";
}

public class UpdateAsyncSqlServerTests : UpdateAsyncSqlTests
{
    protected override DbConnection CreateConnection() => new SqlConnection();
    protected override string D(string identifier) => $"[{identifier}]";
}

/// <summary>El proveedor no reconocido conserva el SQL sin delimitar de siempre.</summary>
public class UpdateAsyncUnknownProviderTests
{
    [Fact]
    public async Task UpdateAsync_WhenProviderIsUnknown_ShouldNotDelimitAnything()
    {
        using var connection = new NpgsqlConnection();

        var sql = await SqlCapture.UpdateAsync(connection, new Gym { Id = 1, Name = "Central" });

        Assert.Equal("UPDATE Gyms SET Name = @Name WHERE Id = @Id", sql);
    }
}
