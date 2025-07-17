using Dapper;
using Dapper.Contrib.Extensions;
using System.Linq.Expressions;
using System.Reflection;

namespace PAN.DapperLambdaToSql;

public class DapperHelper
{
    private static Dictionary<RuntimeTypeHandle, string> TypeTableName = new();

    public static string GetTableName(Type type)
    {
        string name;
        if (TypeTableName.TryGetValue(type.TypeHandle, out name) && name != null) return name;

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

        TypeTableName[type.TypeHandle] = name;
        return name;
    }

    public static (string Sql, DynamicParameters Parameters) ToSql<T>(Expression<Func<T, bool>> predicate)
    {
        var visitor = new SqlExpressionVisitor();
        visitor.Visit(predicate);

        return (visitor.Sql, visitor.Parameters);
    }
}
