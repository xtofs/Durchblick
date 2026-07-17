namespace Durchblick.CSharp.Semantics;

using Durchblick.Collections;

/// <summary>
/// The declaration-level symbol inventory for a compilation.
/// </summary>
/// <remarks>
/// Phase 1 produces this table and phase 2 consumes it as the stable lookup surface
/// for namespaces and types.
/// </remarks>
public sealed record SymbolTable(
    /// <summary>Declared namespaces discovered in the compilation.</summary>
    ImmutableCollection<NamespaceSymbol> Namespaces,
    /// <summary>Declared types discovered in the compilation.</summary>
    ImmutableCollection<TypeSymbol> Types
)
{
    /// <summary>
    /// Creates an empty symbol table.
    /// </summary>
    public SymbolTable() : this([], []) { }

    /// <summary>
    /// Looks up a namespace by name in the current table.
    /// </summary>
    public NamespaceSymbol? LookupNamespace(string name)
        => Namespaces.FirstOrDefault(n => n.Name == name);

    /// <summary>
    /// Looks up a type by name in the current table.
    /// </summary>
    public TypeSymbol? LookupType(string name)
        => Types.FirstOrDefault(t => t.Name == name);
}
