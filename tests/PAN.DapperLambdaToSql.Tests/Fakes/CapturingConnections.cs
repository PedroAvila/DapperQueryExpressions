using System.Collections;
using System.Data;
using System.Data.Common;

namespace PAN.DapperLambdaToSql.Tests.Fakes;

/// <summary>
/// Se lanza en cuanto el comando va a ejecutarse, ya con el <c>CommandText</c> definitivo.
/// Evita tener que simular un <see cref="DbDataReader"/> completo solo para leer el SQL.
/// </summary>
public class SqlCapturedException(string sql) : Exception("SQL capturado")
{
    public string Sql { get; } = sql;
}

public abstract class CapturingConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "test";
    public override string DataSource => "test";
    public override string ServerVersion => "0.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() => _state = ConnectionState.Closed;
    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel il) => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new CapturingCommand { Connection = this };
}

// El nombre del tipo es lo único que mira DapperHelper.GetDialect, así que estas clases
// reproducen fielmente la detección real sin depender de los paquetes de proveedor.
public sealed class SqlConnection : CapturingConnection;
public sealed class MySqlConnection : CapturingConnection;
public sealed class NpgsqlConnection : CapturingConnection;

public sealed class CapturingCommand : DbCommand
{
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    public override bool DesignTimeVisible { get; set; }

    protected override DbConnection? DbConnection { get; set; }
    protected override DbTransaction? DbTransaction { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; } = new CapturingParameterCollection();

    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new CapturingParameter();

    public override int ExecuteNonQuery() => throw new SqlCapturedException(CommandText);
    public override object? ExecuteScalar() => throw new SqlCapturedException(CommandText);
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new SqlCapturedException(CommandText);
}

public sealed class CapturingParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() { }
}

public sealed class CapturingParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items = [];

    public override int Count => _items.Count;
    public override object SyncRoot => _items;

    public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) Add(v!); }
    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string value) => IndexOf(value) >= 0;
    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
}
