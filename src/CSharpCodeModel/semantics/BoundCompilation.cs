namespace CSharpCodeModel.Semantics;

using System.Collections.Immutable;
using CSharpCodeModel.Syntax;

/// <summary>
/// Captures the output of phase 1 symbol collection.
/// </summary>
/// <remarks>
/// This is the handoff object consumed by semantic model construction.
/// It contains the syntactic root, the resolved symbol table, the global scope,
/// and the member symbols discovered during declaration binding.
/// </remarks>
internal sealed record BoundCompilation(
    /// <summary>The original compilation unit that was analyzed.</summary>
    CompilationUnitDecl Compilation,
    /// <summary>The root scope built from declared namespaces and types.</summary>
    Scope GlobalScope,
    /// <summary>The symbol table produced from declaration binding.</summary>
    SymbolTable SymbolTable,
    /// <summary>Diagnostics emitted while collecting symbols.</summary>
    ImmutableArray<string> Diagnostics,
    /// <summary>Member symbols created while walking type declarations.</summary>
    ImmutableArray<MemberSymbol> MemberSymbols
);