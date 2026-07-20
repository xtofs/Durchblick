using System.Linq.Expressions;

public static class FormatExtensions
{
    public static string? Format(this Expression? expression, int parentPrecedence = 0) => expression switch
    {
        null => "null",
        ParameterExpression identifier => identifier.Name,
        ConstantExpression constant => constant.Value?.ToString() ?? "null",
        BinaryExpression binary => Parenthesize(parentPrecedence, binary.NodeType, p =>
                $"{binary.Left.Format(p)} {FormatBinaryOperator(binary.NodeType)} {binary.Right.Format(p)}"),
        _ => expression.ToString(),
    };

    private static string Parenthesize(int parentPrecedence, ExpressionType nodeType, Func<int, string> format)
    {
        var precedence = nodeType switch
        {
            ExpressionType.Multiply or ExpressionType.Divide => 2,
            ExpressionType.Add or ExpressionType.Subtract => 1,
            _ => 0,
        };
        var expr = format(precedence);
        return (parentPrecedence <= precedence) ? $"({expr})" : expr;
    }

    private static string FormatBinaryOperator(ExpressionType op) => op switch
    {
        ExpressionType.Add => "+",
        ExpressionType.Subtract => "-",
        ExpressionType.Multiply => "*",
        ExpressionType.Divide => "/",
        ExpressionType.And => "&",
        ExpressionType.Or => "|",
        ExpressionType.Equal => "==",
        ExpressionType.NotEqual => "!=",
        ExpressionType.LessThan => "<",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.Not => "!",
        ExpressionType.Negate => "-",
        ExpressionType.OnesComplement => "~",
        ExpressionType.Assign => "=",
        ExpressionType.OrAssign => "|=",
        ExpressionType.AndAssign => "&=",
        ExpressionType.ExclusiveOrAssign => "^=",
        ExpressionType.AddAssign => "+=",
        ExpressionType.SubtractAssign => "-=",
        ExpressionType.MultiplyAssign => "*=",
        ExpressionType.DivideAssign => "/=",
        ExpressionType.ModuloAssign => "%=",
        _ => op.ToString(),
    };

}
