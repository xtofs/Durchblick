namespace CSharpCodeModel.Syntax;

using CSharpCodeModel.Collections;

public sealed record TypeReference(string Name, string? Namespace, ImmutableCollection<TypeReference> GenericArguments) : AstNode;

public static class BuiltInTypeNames
{
    public const string Bool = "bool";
    public const string Char = "char";
    public const string Double = "double";
    public const string Float = "float";
    public const string Int = "int";
    public const string Long = "long";
    public const string Null = "null";
    public const string String = "string";
    public const string Void = "void";
}

public static class BuiltInTypeReferences
{
    public static TypeReference Bool { get; } = Declaration.TypeRef(BuiltInTypeNames.Bool);
    public static TypeReference Char { get; } = Declaration.TypeRef(BuiltInTypeNames.Char);
    public static TypeReference Double { get; } = Declaration.TypeRef(BuiltInTypeNames.Double);
    public static TypeReference Float { get; } = Declaration.TypeRef(BuiltInTypeNames.Float);
    public static TypeReference Int { get; } = Declaration.TypeRef(BuiltInTypeNames.Int);
    public static TypeReference Long { get; } = Declaration.TypeRef(BuiltInTypeNames.Long);
    public static TypeReference String { get; } = Declaration.TypeRef(BuiltInTypeNames.String);
    public static TypeReference Void { get; } = Declaration.TypeRef(BuiltInTypeNames.Void);
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
