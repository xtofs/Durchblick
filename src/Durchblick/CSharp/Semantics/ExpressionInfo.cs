namespace Durchblick.CSharp.Semantics;

using Durchblick.Collections;
using Durchblick.CSharp.Syntax;

public abstract record ExpressionInfo(Expression Node);

public sealed record LiteralInfo(LiteralExpression Lit, TypeSymbol Type) : ExpressionInfo(Lit);

public sealed record IdentifierInfo(
    IdentifierExpression Expression,
    Symbol? Symbol,
    TypeSymbol? Type
) : ExpressionInfo(Expression);

public sealed record MemberAccessInfo(
    MemberAccessExpression Expression,
    Symbol? Member,
    TypeSymbol? Type
) : ExpressionInfo(Expression);

public sealed record CallInfo(
    CallExpression Expression,
    MethodSymbol? Method,
    TypeSymbol? ReturnType,
    ImmutableCollection<TypeSymbol> ArgumentTypes
) : ExpressionInfo(Expression);

public sealed record ObjectCreationInfo(
    ObjectCreationExpression Expression,
    TypeSymbol Type,
    ImmutableCollection<TypeSymbol> ArgumentTypes
) : ExpressionInfo(Expression);

public sealed record CastInfo(
    CastExpression Expression,
    TypeSymbol TargetType,
    TypeSymbol SourceType
) : ExpressionInfo(Expression);

public sealed record BinaryInfo(
    BinaryExpression Expression,
    TypeSymbol LeftType,
    TypeSymbol RightType,
    MethodSymbol? OperatorMethod,
    TypeSymbol ResultType
) : ExpressionInfo(Expression);

public sealed record UnaryInfo(
    UnaryExpression Expression,
    TypeSymbol OperandType,
    MethodSymbol? OperatorMethod,
    TypeSymbol ResultType
) : ExpressionInfo(Expression);

public sealed record ConditionalInfo(
    ConditionalExpression Expression,
    TypeSymbol ConditionType,
    TypeSymbol ThenType,
    TypeSymbol ElseType,
    TypeSymbol ResultType
) : ExpressionInfo(Expression);

public sealed record LambdaInfo(
    LambdaExpression Expression,
    MethodSymbol? Symbol,
    TypeSymbol DelegateType
) : ExpressionInfo(Expression);

public sealed record AwaitInfo(
    AwaitExpression Expression,
    TypeSymbol AwaitedType,
    TypeSymbol ResultType
) : ExpressionInfo(Expression);

public sealed record UnknownExpressionInfo(
    Expression Node
) : ExpressionInfo(Node);

public sealed record VariableInfo(
    VariableDecl Declaration,
    LocalSymbol Symbol
);

public sealed record ForEachInfo(
    ForEachStatement Node,
    LocalSymbol IterationVariable,
    TypeSymbol CollectionType,
    TypeSymbol ElementType
);
