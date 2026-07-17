namespace Durchblick.CSharp.Semantics;

using Durchblick.Collections;
using Durchblick.CSharp.Syntax;


public abstract record PatternInfo(Pattern Syntax);

public sealed record TypePatternInfo(
    TypePattern Node,
    TypeSymbol Type
) : PatternInfo(Node);

public sealed record ConstantPatternInfo(
    ConstantPattern Node,
    ExpressionInfo Expression
) : PatternInfo(Node);

public sealed record RelationalPatternInfo(
    RelationalPattern Node,
    ExpressionInfo Expression
) : PatternInfo(Node);

public sealed record LogicalPatternInfo(
    LogicalPattern Node,
    ImmutableCollection<PatternInfo> SubPatterns
) : PatternInfo(Node);

public sealed record RecursivePatternInfo(
    RecursivePattern Node,
    TypeSymbol Type,
    ImmutableCollection<PatternInfo> SubPatterns
) : PatternInfo(Node);

public sealed record UnknownPatternInfo(
    Pattern Node
) : PatternInfo(Node);
