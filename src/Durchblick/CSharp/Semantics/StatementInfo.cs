namespace Durchblick.CSharp.Semantics;

using Durchblick.Collections;
using Durchblick.CSharp.Syntax;

public abstract record StatementInfo(Statement Syntax);

public sealed record BlockStatementInfo(
    BlockStatement Node,
    ImmutableCollection<StatementInfo> Statements
) : StatementInfo(Node);

public sealed record ExpressionStatementInfo(
    ExpressionStatement Node,
    ExpressionInfo Expression
) : StatementInfo(Node);

public sealed record VariableDeclarationStatementInfo(
    VariableDeclarationStatement Node,
    LocalSymbol Symbol,
    ExpressionInfo? Initializer
) : StatementInfo(Node);

public sealed record IfStatementInfo(
    IfStatement Node,
    ExpressionInfo Condition,
    StatementInfo Then,
    StatementInfo? Else
) : StatementInfo(Node);

public sealed record ForEachStatementInfo(
    ForEachStatement Node,
    LocalSymbol IterationVariable,
    ExpressionInfo Collection,
    TypeSymbol ElementType,
    StatementInfo Body
) : StatementInfo(Node);

public sealed record ReturnStatementInfo(
    ReturnStatement Node,
    ExpressionInfo? Expression
) : StatementInfo(Node);

public sealed record UnknownStatementInfo(
    Statement Node
) : StatementInfo(Node);
