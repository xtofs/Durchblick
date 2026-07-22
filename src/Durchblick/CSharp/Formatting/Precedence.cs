namespace Durchblick.CSharp.Formatting;

using Durchblick.CSharp.Syntax;


/// <summary>C# operator precedence levels (higher binds tighter). Only expression operators matter.</summary>
internal static class Precedence
{
    public const int Primary = 12;        // literal, identifier, call, member/index access, new, tuple
    public const int Unary = 11;          // unary operator, cast, await
    public const int Multiplicative = 10; // Multiply, Divide
    public const int Additive = 9;        // Add, Subtract
    public const int Relational = 8;      // Less, Greater
    public const int Equality = 7;        // Equals, NotEquals
    public const int LogicalAnd = 6;      // And (&&)
    public const int LogicalOr = 5;       // Or (||)
    public const int Conditional = 3;     // ?:
    public const int Assignment = 2;      // =

    public static int Of(BinaryOperator op) => op switch
    {
        BinaryOperator.Multiply or BinaryOperator.Divide => Multiplicative,
        BinaryOperator.Add or BinaryOperator.Subtract => Additive,
        BinaryOperator.Less or BinaryOperator.Greater => Relational,
        BinaryOperator.Equals or BinaryOperator.NotEquals => Equality,
        BinaryOperator.And => LogicalAnd,
        BinaryOperator.Or => LogicalOr,
        _ => Additive,
    };

    public static int Of(Expression e) => e switch
    {
        BinaryExpression b => Of(b.Operator),
        ConditionalExpression => Conditional,
        AssignExpression => Assignment,
        UnaryExpression or CastExpression or AwaitExpression => Unary,
        IsInstanceExpression => Primary,
        _ => Primary,
    };
}

