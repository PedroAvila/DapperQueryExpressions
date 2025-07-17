using Dapper;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace PAN.DapperLambdaToSql;

public static class DapperExtensions
{
    public static async Task<bool> UpdateAsync<T>(this IDbConnection connection, T entity)
    {
        Type entityType = typeof(T);

        var tableName = DapperHelper.GetTableName(entityType);
        PropertyInfo[] properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        StringBuilder setClause = new StringBuilder();
        var parameters = new DynamicParameters();
        foreach (PropertyInfo property in properties)
        {
            var value = property.GetValue(entity);
            if (value != null && !property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)) // Omitir la clave primaria
            {
                // Agregar la propiedad a la parte SET de la consulta SQL
                setClause.Append($"{property.Name} = @{property.Name}, ");
                parameters.Add(property.Name, value); // Agregar el parámetro correspondiente
            }
        }
        parameters.Add("Id", entityType.GetProperty("Id").GetValue(entity));
        if (setClause.Length > 0)
        {
            setClause.Length -= 2; // Eliminar la coma y el espacio finales

            // Construir la consulta SQL completa
            var query = $"UPDATE {tableName} SET {setClause} WHERE Id = @Id";

            // Ejecutar la consulta SQL utilizando Dapper
            var affectedRows = await connection.ExecuteAsync(query, parameters);

            // Retornar true si se afectaron filas, false si no
            return affectedRows > 0;
        }
        else
        {
            // No hay propiedades para actualizar, retorno falso
            return false;
        }
    }

    public static async Task<bool> ExistAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
    {
        Type entityType = typeof(T);

        // Obtener el nombre de la tabla utilizando Dapper.Contrib
        string tableName = DapperHelper.GetTableName(entityType);

        // Construir la consulta SQL
        var query = $"SELECT COUNT(*) FROM {tableName} WHERE ";

        // Obtener la condición del predicado y agregarla a la consulta SQL
        var whereClause = DapperHelper.ToSql(predicate);
        query += whereClause.Sql;

        // Ejecutar la consulta SQL utilizando Dapper y obtener el recuento de filas
        var count = await connection.QueryFirstOrDefaultAsync<int>(query, whereClause.Parameters);

        // Si el recuento es mayor que cero, la entidad existe
        return count > 0;
    }
}
