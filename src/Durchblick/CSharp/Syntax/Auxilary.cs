namespace Durchblick.CSharp.Syntax;

using Durchblick.Collections;

public sealed record TypeReference(string Name, string? Namespace, ImmutableCollection<TypeReference> GenericArguments) : AstNode;


public static class BuiltInTypeReferences
{
    public static TypeReference Bool { get; } = Declaration.TypeRef("Boolean", "System");
    public static TypeReference Char { get; } = Declaration.TypeRef("Char", "System");
    public static TypeReference Double { get; } = Declaration.TypeRef("Double", "System");
    public static TypeReference Float { get; } = Declaration.TypeRef("Single", "System");
    public static TypeReference Int { get; } = Declaration.TypeRef("Int32", "System");
    public static TypeReference Long { get; } = Declaration.TypeRef("Int64", "System");
    public static TypeReference String { get; } = Declaration.TypeRef("String", "System");
    public static TypeReference Object { get; } = Declaration.TypeRef("Object", "System");
}

public sealed record SymbolReference(string Id, SymbolKind Kind);

public sealed record Attribute(TypeReference Type, ImmutableCollection<Expression> Arguments) { }

public sealed record Modifier(ModifierKind Kind)
{
    public static Modifier Public { get; } = new(ModifierKind.Public);
    public static Modifier Private { get; } = new(ModifierKind.Private);
    public static Modifier Protected { get; } = new(ModifierKind.Protected);
    public static Modifier Internal { get; } = new(ModifierKind.Internal);

    public string Keyword = Kind.ToString().ToLower();
}

public sealed record SwitchCase(Pattern Pattern, ImmutableCollection<Statement> Body);

public sealed record CatchClause(TypeReference Type, string Variable, Statement Body);

public sealed record AssignmentExpression(string Member, Expression Value);

public abstract record ExpressionOrBlock
{
    private protected ExpressionOrBlock() { }

    // Factory methods
    public static ExprBody FromExpression(Expression expr)
        => new(expr);

    public static BlockBody FromBlock(BlockStatement block)
        => new(block);
}

public sealed record ExprBody(Expression Value) : ExpressionOrBlock;
public sealed record BlockBody(BlockStatement Block) : ExpressionOrBlock;
