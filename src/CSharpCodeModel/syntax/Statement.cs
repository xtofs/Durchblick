using CSharpCodeModel.Collections;

namespace CSharpCodeModel.Syntax;

public abstract record Statement : AstNode
{
    private protected Statement() { }

    public static BlockStatement Block(IEnumerable<Statement> statements)
        => new([.. statements]);

    public static ExpressionStatement Expr(Expression expr)
        => new(expr);

    public static ReturnStatement Return(Expression expr)
        => new(expr);

    public static IfStatement If(Expression condition, Statement thenStmt, Statement? elseStmt)
        => new(condition, thenStmt, elseStmt);

    public static WhileStatement While(Expression condition, Statement body)
        => new(condition, body);

    public static ForStatement For(IEnumerable<Statement> init, Expression? condition, IEnumerable<Statement> iter, Statement body)
        => new(init.ToImmutableCollection(), condition, iter.ToImmutableCollection(), body);

    public static ForEachStatement ForEach(VariableDecl variable, Expression collection, Statement body)
        => new(variable, collection, body);

    public static SwitchStatement Switch(Expression expr, IEnumerable<SwitchCase> cases)
        => new(expr, cases.ToImmutableCollection());

    public static TryStatement Try(Statement body, IEnumerable<CatchClause> catches, Statement? finallyStmt)
        => new(body, catches.ToImmutableCollection(), finallyStmt);

    public static ThrowStatement Throw(Expression expr)
        => new(expr);

    public static BreakStatement Break() => new();
    public static ContinueStatement Continue() => new();

    public static VariableDeclarationStatement Var(VariableDecl decl)
        => new(decl);
}

public sealed record BlockStatement(ImmutableCollection<Statement> Statements) : Statement;
public sealed record ExpressionStatement(Expression Expression) : Statement;
public sealed record ReturnStatement(Expression Expression) : Statement;
public sealed record IfStatement(Expression Condition, Statement Then, Statement? Else) : Statement;
public sealed record WhileStatement(Expression Condition, Statement Body) : Statement;
public sealed record ForStatement(ImmutableCollection<Statement> Initializer, Expression? Condition, ImmutableCollection<Statement> Iterator, Statement Body) : Statement;
public sealed record ForEachStatement(VariableDecl Variable, Expression Collection, Statement Body) : Statement;
public sealed record SwitchStatement(Expression Expression, ImmutableCollection<SwitchCase> Cases) : Statement;
public sealed record TryStatement(Statement Body, ImmutableCollection<CatchClause> Catches, Statement? Finally) : Statement;
public sealed record ThrowStatement(Expression Expression) : Statement;
public sealed record BreakStatement() : Statement;
public sealed record ContinueStatement() : Statement;
public sealed record VariableDeclarationStatement(VariableDecl Declaration) : Statement;
