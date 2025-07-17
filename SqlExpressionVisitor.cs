using Dapper;
using System.Linq.Expressions;

namespace PAN.DapperLambdaToSql;

public class SqlExpressionVisitor : ExpressionVisitor
{
    public string Sql { get; private set; }
    public DynamicParameters Parameters { get; private set; }

    public SqlExpressionVisitor()
    {
        Sql = "";
        Parameters = new DynamicParameters();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.Equal)
        {
            Visit(node.Left);
            Sql += " = ";

            if (node.Right.NodeType == ExpressionType.MemberAccess)
            {
                VisitMemberAccess((MemberExpression)node.Right);
            }
            else
            {
                Visit(node.Right);
            }
        }
        else if (node.NodeType == ExpressionType.AndAlso)
        {
            // Si es AndAlso, maneja la expresión
            VisitAndAlsoBinary(node);
        }
        else
        {
            throw new NotSupportedException("Only equality comparison is supported.");
        }

        return node;
    }

    private void VisitAndAlsoBinary(BinaryExpression node)
    {
        Visit(node.Left);
        Sql += " AND ";

        if (node.Right.NodeType == ExpressionType.Equal)
        {
            VisitEqualityBinary((BinaryExpression)node.Right);
        }
        else
        {
            throw new NotSupportedException("Unsupported AndAlso expression.");
        }
    }

    private void VisitEqualityBinary(BinaryExpression node)
    {
        Visit(node.Left);
        Sql += " = ";

        if (node.Right.NodeType == ExpressionType.MemberAccess)
        {
            VisitMemberAccess((MemberExpression)node.Right);
        }
        else
        {
            Visit(node.Right);
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
        // Obtener el valor de la propiedad o campo
        var value = Expression.Lambda(node).Compile().DynamicInvoke();

        // Crear una expresión constante con el valor
        var constant = Expression.Constant(value);

        // Llamar a VisitConstant para manejar el valor como una constante
        VisitConstant(constant);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        Sql += node.Member.Name;
        return node;
    }
}
