using System.Data.Common;
using System.Linq.Expressions;

namespace PAN.DapperLambdaToSql.Tests.Fakes;

/// <summary>
/// Ejecuta las extensiones contra una conexión simulada y devuelve el SQL que Dapper
/// iba a mandar al motor. No es una reconstrucción: es el <c>CommandText</c> real.
/// </summary>
internal static class SqlCapture
{
    /// <summary>Devuelve el SQL generado, o <c>null</c> si nunca se llegó a ejecutar nada.</summary>
    public static async Task<string?> TryUpdateAsync<T>(DbConnection connection, T entity)
    {
        try
        {
            await connection.UpdateAsync(entity);
            return null;
        }
        catch (SqlCapturedException ex)
        {
            return ex.Sql;
        }
    }

    public static async Task<string> UpdateAsync<T>(DbConnection connection, T entity)
        => await TryUpdateAsync(connection, entity)
           ?? throw new InvalidOperationException("No se capturó SQL: la consulta nunca se ejecutó.");

    public static async Task<string> ExistAsync<T>(DbConnection connection, Expression<Func<T, bool>> predicate)
    {
        try
        {
            await connection.ExistAsync(predicate);
        }
        catch (SqlCapturedException ex)
        {
            return ex.Sql;
        }

        throw new InvalidOperationException("No se capturó SQL: la consulta nunca se ejecutó.");
    }

    public static async Task<string> QueryAsync<T>(DbConnection connection, Expression<Func<T, bool>> predicate)
    {
        try
        {
            await connection.QueryAsync(predicate);
        }
        catch (SqlCapturedException ex)
        {
            return ex.Sql;
        }

        throw new InvalidOperationException("No se capturó SQL: la consulta nunca se ejecutó.");
    }
}
