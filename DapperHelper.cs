using Dapper;
using Dapper.Contrib.Extensions;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace PAN.DapperLambdaToSql;

public class DapperHelper
{
    // ConcurrentDictionary y no Dictionary: esta caché es estática y se escribe desde
    // cualquier hilo que resuelva una entidad por primera vez. Con Dictionary, dos
    // escrituras simultáneas pueden corromper sus buckets internos. Ver docs/adr/0003.
    private static readonly ConcurrentDictionary<RuntimeTypeHandle, string> TypeTableName = new();

    public static string GetTableName(Type type)
    {
        // Bajo contención GetOrAdd puede ejecutar la fábrica más de una vez, pero solo
        // un valor queda almacenado. Es inocuo aquí: ResolveTableName es una función
        // pura del tipo y siempre devuelve el mismo nombre.
        return TypeTableName.GetOrAdd(type.TypeHandle, _ => ResolveTableName(type));
    }

    private static string ResolveTableName(Type type)
    {
        string name;

        // Aquí se implementa el resto de la lógica del método GetTableName
        var tableAttrName =
                type.GetCustomAttribute<TableAttribute>(false)?.Name
                ?? (type.GetCustomAttributes(false).FirstOrDefault(attr => attr.GetType().Name == "TableAttribute") as dynamic)?.Name;

        if (tableAttrName != null)
        {
            name = tableAttrName;
        }
        else
        {
            name = type.Name + "s";
            if (type.IsInterface && name.StartsWith("I"))
                name = name.Substring(1);
        }

        return name;
    }

    public static bool IsWriteable(PropertyInfo property)
    {
        // Replica el IsWriteable interno de Dapper.Contrib: solo se excluye
        // la propiedad cuando existe un [Write(false)] explícito.
        var writeAttribute = property.GetCustomAttribute<WriteAttribute>(false);
        return writeAttribute?.Write ?? true;
    }

    public static bool IsComputed(PropertyInfo property)
    {
        // [Computed] es un atributo marcador (no tiene propiedades): la columna sí
        // existe en la tabla, pero su valor lo genera el motor -columnas calculadas,
        // DEFAULT, rowversion, triggers- y por eso no debe escribirse.
        return property.GetCustomAttribute<ComputedAttribute>(false) != null;
    }

    public static PropertyInfo GetKeyProperty(Type type, PropertyInfo[] properties)
    {
        // Orden de resolución (ver docs/adr/0002): los atributos explícitos ganan
        // siempre a la convención, y el "Id" exacto se evalúa antes que el resto
        // para no alterar el comportamiento de las entidades que ya funcionaban.
        var key = properties.FirstOrDefault(p =>
            p.GetCustomAttribute<ExplicitKeyAttribute>(false) != null
            || p.GetCustomAttribute<KeyAttribute>(false) != null);
        if (key != null) return key;

        key = properties.FirstOrDefault(p => p.Name == "Id");
        if (key != null) return key;

        // Insensible a mayúsculas: cubre ID, iD.
        key = properties.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
        if (key != null) return key;

        // Clase+Id. Se compara contra un nombre concreto y nunca contra un patrón
        // "termina en Id": de lo contrario las llaves foráneas -GymId, RoleId- o un
        // identificador externo -TransactionId- serían candidatos a clave primaria.
        var expectedName = type.Name + "Id";
        key = properties.FirstOrDefault(p => p.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
        if (key != null) return key;

        throw new InvalidOperationException(
            $"La entidad '{type.Name}' no tiene clave primaria. Se buscó una propiedad con " +
            $"[Key] o [ExplicitKey], una llamada 'Id' y una llamada '{expectedName}'.");
    }

    /// <summary>
    /// Fuerza un dialecto concreto. Si queda en <c>null</c> -lo normal- el dialecto se
    /// detecta a partir del tipo de conexión. Útil para proveedores que envuelven la
    /// conexión real y por tanto no se reconocen por su nombre de tipo.
    /// </summary>
    public static ISqlDialect? Dialect { get; set; }

    public static ISqlDialect GetDialect(IDbConnection connection)
    {
        if (Dialect != null) return Dialect;
        if (connection == null) return SqlDialects.None;

        // Mismo criterio que Dapper.Contrib para elegir su ISqlAdapter: el nombre del
        // tipo de conexión. Cubre System.Data.SqlClient y Microsoft.Data.SqlClient
        // -ambos SqlConnection- y MySql.Data y MySqlConnector -ambos MySqlConnection-.
        switch (connection.GetType().Name.ToLowerInvariant())
        {
            case "sqlconnection": return SqlDialects.SqlServer;
            case "mysqlconnection": return SqlDialects.MySql;

            // Un proveedor desconocido conserva el comportamiento previo -sin delimitar-
            // en lugar de arriesgar SQL inválido con delimitadores del motor equivocado.
            default: return SqlDialects.None;
        }
    }

    public static (string Sql, DynamicParameters Parameters) ToSql<T>(Expression<Func<T, bool>> predicate)
        => ToSql(predicate, SqlDialects.None);

    public static (string Sql, DynamicParameters Parameters) ToSql<T>(Expression<Func<T, bool>> predicate, ISqlDialect dialect)
    {
        var visitor = new SqlExpressionVisitor(dialect);
        visitor.Visit(predicate);

        return (visitor.Sql, visitor.Parameters);
    }
}
