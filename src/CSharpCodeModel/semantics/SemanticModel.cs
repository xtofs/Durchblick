
namespace CSharpCodeModel.Semantics;

using System.Collections.Immutable;
using CSharpCodeModel.Syntax;

/// <summary>
/// The result of phase 2 semantic binding.
/// </summary>
/// <remarks>
/// This model stores the resolved symbol table alongside the computed semantic annotations
/// for expressions, statements, and patterns.
/// </remarks>
public sealed record SemanticModel(
    /// <summary>The symbol table used to interpret the syntax tree.</summary>
    SymbolTable Symbols,
    /// <summary>Semantic annotations for expressions.</summary>
    ImmutableDictionary<Expression, ExpressionInfo> ExpressionInfos,
    /// <summary>Semantic annotations for statements.</summary>
    ImmutableDictionary<Statement, StatementInfo> StatementInfos,
    /// <summary>Semantic annotations for patterns.</summary>
    ImmutableDictionary<Pattern, PatternInfo> PatternInfos,
    /// <summary>Diagnostics emitted while building semantic information.</summary>
    ImmutableArray<string> Diagnostics
);
