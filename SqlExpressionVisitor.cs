using Dapper;
using System.Linq.Expressions;

namespace PAN.DapperLambdaToSql;

public class SqlExpressionVisitor : ExpressionVisitor
{
    private readonly ISqlDialect _dialect;

    public string Sql { get; private set; }
    public DynamicParameters Parameters { get; private set; }

    public SqlExpressionVisitor() : this(SqlDialects.None)
    {
    }

    public SqlExpressionVisitor(ISqlDialect dialect)
    {
        _dialect = dialect ?? SqlDialects.None;
        Sql = "";
        Parameters = new DynamicParameters();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        switch (node.NodeType)
        {
            case ExpressionType.AndAlso:
                VisitLogicalBinary(node, " AND ");
                break;
            case ExpressionType.OrElse:
                VisitLogicalBinary(node, " OR ");
                break;
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
                VisitComparisonBinary(node, ComparisonOperatorFor(node.NodeType));
                break;
            default:
                throw new NotSupportedException($"Only equality comparison is supported. Unsupported operator: '{node.NodeType}'.");
        }

        return node;
    }

    private static string ComparisonOperatorFor(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        _ => throw new NotSupportedException($"Unsupported comparison operator: '{nodeType}'."),
    };

    private void VisitComparisonBinary(BinaryExpression node, string sqlOperator)
    {
        Visit(node.Left);
        Sql += sqlOperator;
        VisitValueOperand(node.Right);
    }

    private void VisitValueOperand(Expression operand)
    {
        if (operand.NodeType == ExpressionType.MemberAccess)
        {
            VisitMemberAccess((MemberExpression)operand);
        }
        else
        {
            Visit(operand);
        }
    }

    private void VisitLogicalBinary(BinaryExpression node, string sqlOperator)
    {
        VisitLogicalOperand(node.Left, node.NodeType);
        Sql += sqlOperator;
        VisitLogicalOperand(node.Right, node.NodeType);
    }

    private void VisitLogicalOperand(Expression operand, ExpressionType parentNodeType)
    {
        // Paréntesis mínimos: solo hace falta envolver un OrElse cuando cuelga
        // de un AndAlso, porque AND liga más fuerte que OR tanto en C# como en SQL.
        var needsParens = parentNodeType == ExpressionType.AndAlso
            && operand is BinaryExpression { NodeType: ExpressionType.OrElse };

        if (needsParens)
        {
            Sql += "(";
            Visit(operand);
            Sql += ")";
        }
        else
        {
            Visit(operand);
        }
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var parameterName = $"@param{Parameters.ParameterNames.Count()}";
        Parameters.Add(parameterName, node.Value);

        Sql += parameterName;

        return node;
    }

    private void VisitMemberAccess(MemberExpression node)
    {
        VisitConstant(Expression.Constant(ResolveValue(node)));
    }

    private static object? ResolveValue(Expression node)
        => Expression.Lambda(node).Compile().DynamicInvoke();

    protected override Expression VisitMember(MemberExpression node)
    {
        // Aquí el miembro siempre representa una columna: los valores del lado derecho
        // los desvía VisitMemberAccess antes de llegar a este punto.
        Sql += _dialect.Delimit(node.Member.Name);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (IsStringContains(node))
        {
            Visit(node.Object!); // emite la columna, p. ej. u.Name -> Name
            Sql += " LIKE ";

            var rawValue = ResolveValue(node.Arguments[0]);
            var pattern = rawValue is null ? null : $"%{rawValue}%";
            VisitConstant(Expression.Constant(pattern));

            return node;
        }

        throw new NotSupportedException(
            $"El método '{node.Method.DeclaringType?.Name}.{node.Method.Name}' no está soportado. " +
            "Solo string.Contains(...) se traduce a LIKE.");
    }

    private static bool IsStringContains(MethodCallExpression node)
        => node.Method.DeclaringType == typeof(string)
           && node.Method.Name == nameof(string.Contains)
           && node.Object != null
           && node.Arguments.Count == 1;
}
