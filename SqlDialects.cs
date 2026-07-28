using System.Text;

namespace PAN.DapperLambdaToSql;

/// <summary>
/// Delimita identificadores -tablas y columnas- según las reglas del motor.
/// Es lo único que cambia entre motores en el SQL que genera esta librería.
/// </summary>
public interface ISqlDialect
{
    string Delimit(string identifier);
}

/// <summary>SQL Server: <c>[Nombre]</c>.</summary>
public sealed class SqlServerDialect : ISqlDialect
{
    public string Delimit(string identifier) => DelimiterWriter.Write(identifier, '[', ']');
}

/// <summary>MySQL / MariaDB: <c>`Nombre`</c>. Los backticks son válidos incluso con ANSI_QUOTES activo.</summary>
public sealed class MySqlDialect : ISqlDialect
{
    public string Delimit(string identifier) => DelimiterWriter.Write(identifier, '`', '`');
}

/// <summary>
/// No delimita nada. Es el comportamiento que tenía la librería antes de soportar
/// varios motores, y se usa como respaldo cuando el proveedor no se reconoce.
/// </summary>
public sealed class NoDelimiterDialect : ISqlDialect
{
    public string Delimit(string identifier) => identifier;
}

public static class SqlDialects
{
    public static readonly ISqlDialect SqlServer = new SqlServerDialect();
    public static readonly ISqlDialect MySql = new MySqlDialect();
    public static readonly ISqlDialect None = new NoDelimiterDialect();
}

internal static class DelimiterWriter
{
    public static string Write(string identifier, char open, char close)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;

        // Un nombre calificado -"dbo.Users"- se delimita por partes: [dbo].[Users].
        var parts = identifier.Split('.');
        var sb = new StringBuilder();

        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append('.');
            var part = parts[i];

            // Si el nombre ya viene delimitado -[Users], `Users`- se respeta tal cual
            // para no terminar con [[Users]].
            if (part.Length > 1 && part[0] == open && part[part.Length - 1] == close)
            {
                sb.Append(part);
                continue;
            }

            // Duplicar el delimitador de cierre es la forma de escaparlo en ambos motores.
            sb.Append(open)
              .Append(part.Replace(close.ToString(), new string(close, 2)))
              .Append(close);
        }

        return sb.ToString();
    }
}
