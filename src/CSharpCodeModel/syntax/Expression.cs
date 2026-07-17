using CSharpCodeModel.Collections;

namespace CSharpCodeModel.Syntax;

public abstract record Expression : AstNode
{
    private protected Expression() { }

    // Factory methods
    public static LiteralExpression Literal(object value, TypeReference type)
        => new(value, type);

    public static IdentifierExpression Identifier(string name, SymbolReference symbol)
        => new(name, symbol);

    public static UnaryExpression Unary(UnaryOperator op, Expression operand)
        => new(op, operand);

    public static BinaryExpression Binary(BinaryOperator op, Expression left, Expression right)
        => new(op, left, right);

    public static ConditionalExpression Conditional(Expression condition, Expression thenExpr, Expression elseExpr)
        => new(condition, thenExpr, elseExpr);

    public static CallExpression Call(Expression target, IEnumerable<Expression> args, SymbolReference method)
        => new(target, [.. args], method);

    public static MemberAccessExpression Member(Expression target, string member, SymbolReference symbol)
        => new(target, member, symbol);

    public static IndexAccessExpression Index(Expression target, IEnumerable<Expression> indices)
        => new(target, [.. indices]);

    public static ObjectCreationExpression New(TypeReference type, IEnumerable<Expression> args, IEnumerable<AssignmentExpression> init)
        => new(type, [.. args], [.. init]);

    public static LambdaExpression Lambda(IEnumerable<Parameter> parameters, ExpressionOrBlock body, SymbolReference symbol)
        => new LambdaExpression([.. parameters], body, symbol);

    public static TupleExpression Tuple(IEnumerable<Expression> elements)
        => new([.. elements]);

    public static CastExpression Cast(TypeReference type, Expression expr)
        => new(type, expr);

    public static AwaitExpression Await(Expression expr)
        => new(expr);
}

public sealed record LiteralExpression(object Value, TypeReference Type) : Expression;
public sealed record IdentifierExpression(string Name, SymbolReference Symbol) : Expression;
public sealed record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;
public sealed record BinaryExpression(BinaryOperator Operator, Expression Left, Expression Right) : Expression;
public sealed record ConditionalExpression(Expression Condition, Expression Then, Expression Else) : Expression;
public sealed record CallExpression(Expression Target, ImmutableCollection<Expression> Arguments, SymbolReference Method) : Expression;
public sealed record MemberAccessExpression(Expression Target, string MemberName, SymbolReference Symbol) : Expression;
public sealed record IndexAccessExpression(Expression Target, ImmutableCollection<Expression> Indices) : Expression;
public sealed record ObjectCreationExpression(TypeReference Type, ImmutableCollection<Expression> Arguments, ImmutableCollection<AssignmentExpression> Initializer) : Expression;
public sealed record LambdaExpression(ImmutableCollection<Parameter> Parameters, ExpressionOrBlock Body, SymbolReference Symbol) : Expression;
public sealed record TupleExpression(ImmutableCollection<Expression> Elements) : Expression;
public sealed record CastExpression(TypeReference Type, Expression Expression) : Expression;
public sealed record AwaitExpression(Expression Expression) : Expression;
public sealed record Parameter(string Name, TypeReference Type) : Expression;
