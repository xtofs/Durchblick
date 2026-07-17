namespace Durchblick.CSharp.Semantics;

using Durchblick.CSharp.Syntax;

/// <summary>
/// Orchestrates the semantic pipeline for a compilation unit.
/// </summary>
/// <remarks>
/// This class owns the public entry point and delegates the two internal phases:
/// symbol collection first, then semantic model construction.
/// </remarks>
public sealed class Binder
{
    /// <summary>
    /// Runs symbol collection and semantic binding for the supplied compilation unit.
    /// </summary>
    /// <remarks>
    /// The caller does not need to manage intermediate state; both phases are executed internally.
    /// </remarks>
    public SemanticModel Bind(CompilationUnitDecl compilation)
    {
        var collector = new SymbolCollector();
        var boundCompilation = collector.Collect(compilation);
        var semanticBuilder = new SemanticModelBuilder(boundCompilation.SymbolTable);
        return semanticBuilder.Build(boundCompilation);
    }
}