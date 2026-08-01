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

        var dialect = DapperHelper.GetDialect(connection);
        var tableName = DapperHelper.GetTableName(entityType);
        PropertyInfo[] properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo keyProperty = DapperHelper.GetKeyProperty(entityType, properties);
        StringBuilder setClause = new StringBuilder();
        var parameters = new DynamicParameters();
        foreach (PropertyInfo property in properties)
        {
            // Omitir las propiedades marcadas con [Write(false)] -propiedades de
            // navegación cargadas por JOIN que no existen como columna en la tabla-
            // y las [Computed], cuyo valor produce la base de datos. Ver docs/adr/0001.
            if (!DapperHelper.IsWriteable(property) || DapperHelper.IsComputed(property)) continue;

            // Omitir la clave primaria comparando la instancia resuelta y no el nombre:
            // así las llaves foráneas -GymId, RoleId- se siguen actualizando con normalidad.
            if (property == keyProperty) continue;

            var value = property.GetValue(entity);
            if (value != null)
            {
                // Agregar la propiedad a la parte SET de la consulta SQL. Se delimita la
                // columna pero nunca el parámetro: @Key es válido, @`Key` no lo sería.
                setClause.Append($"{dialect.Delimit(property.Name)} = @{property.Name}, ");
                parameters.Add(property.Name, value); // Agregar el parámetro correspondiente
            }
        }
        parameters.Add(keyProperty.Name, keyProperty.GetValue(entity));
        if (setClause.Length > 0)
        {
            setClause.Length -= 2; // Eliminar la coma y el espacio finales

            // Construir la consulta SQL completa
            var query = $"UPDATE {dialect.Delimit(tableName)} SET {setClause} WHERE {dialect.Delimit(keyProperty.Name)} = @{keyProperty.Name}";

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
        var dialect = DapperHelper.GetDialect(connection);
        string tableName = DapperHelper.GetTableName(entityType);

        // Construir la consulta SQL
        var query = $"SELECT COUNT(*) FROM {dialect.Delimit(tableName)} WHERE ";

        // Obtener la condición del predicado y agregarla a la consulta SQL
        var whereClause = DapperHelper.ToSql(predicate, dialect);
        query += whereClause.Sql;

        // Ejecutar la consulta SQL utilizando Dapper y obtener el recuento de filas
        var count = await connection.QueryFirstOrDefaultAsync<int>(query, whereClause.Parameters);

        // Si el recuento es mayor que cero, la entidad existe
        return count > 0;
    }

    public static async Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
    {
        Type entityType = typeof(T);

        var dialect = DapperHelper.GetDialect(connection);
        string tableName = DapperHelper.GetTableName(entityType);

        // Construir la consulta SQL
        var query = $"SELECT * FROM {dialect.Delimit(tableName)} WHERE ";

        var whereClause = DapperHelper.ToSql(predicate, dialect);
        query += whereClause.Sql;

        // Ejecutar la consulta SQL utilizando Dapper y devolver las filas encontradas
        return await connection.QueryAsync<T>(query, whereClause.Parameters);
    }
}
