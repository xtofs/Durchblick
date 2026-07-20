using Durchblick.Collections;

namespace Durchblick.CSharp.Syntax;

public abstract record Pattern : AstNode
{
    private protected Pattern() { }

    public static TypePattern Type(TypeReference type) => new(type);
    public static ConstantPattern Constant(LiteralExpression value) => new(value);
    public static DiscardPattern Discard() => new();
    public static RelationalPattern Rel(RelationalOperator op, Expression value) => new(op, value);
    public static LogicalPattern Logical(LogicalOperator op, Pattern left, Pattern right) => new(op, left, right);
    public static RecursivePattern Recursive(TypeReference type, IEnumerable<PatternProperty> props)
        => new(type, [.. props]);
}

public sealed record TypePattern(TypeReference TypeReference) : Pattern;
public sealed record ConstantPattern(LiteralExpression Value) : Pattern;
public sealed record DiscardPattern : Pattern;
public sealed record RelationalPattern(RelationalOperator Operator, Expression Value) : Pattern;
public sealed record LogicalPattern(LogicalOperator Operator, Pattern Left, Pattern Right) : Pattern;
public sealed record RecursivePattern(TypeReference TypeReference, ImmutableCollection<PatternProperty> Properties) : Pattern;

public sealed record PatternProperty(string Name, Pattern Pattern);

