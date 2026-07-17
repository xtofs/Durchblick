namespace Durchblick.CSharp.Semantics;

using System.Collections.Generic;
using Durchblick.Collections;
using Durchblick.CSharp.Syntax;

/// <summary>
/// Resolves syntactic type references against the symbol table produced by phase 1.
/// </summary>
/// <remarks>
/// The resolver is intentionally narrow: it maps a <see cref="TypeReference"/> to a symbol and
/// preserves generic arguments as symbol references, returning an error symbol when the lookup fails.
/// </remarks>
public sealed class TypeResolver
{
    private static readonly Dictionary<string, PrimitiveTypeSymbol> BuiltInTypes = new(StringComparer.Ordinal)
    {
        [BuiltInTypeNames.Bool] = new PrimitiveTypeSymbol(BuiltInTypeNames.Bool),
        [BuiltInTypeNames.Char] = new PrimitiveTypeSymbol(BuiltInTypeNames.Char),
        [BuiltInTypeNames.Double] = new PrimitiveTypeSymbol(BuiltInTypeNames.Double),
        [BuiltInTypeNames.Float] = new PrimitiveTypeSymbol(BuiltInTypeNames.Float),
        [BuiltInTypeNames.Int] = new PrimitiveTypeSymbol(BuiltInTypeNames.Int),
        [BuiltInTypeNames.Long] = new PrimitiveTypeSymbol(BuiltInTypeNames.Long),
        [BuiltInTypeNames.Null] = new PrimitiveTypeSymbol(BuiltInTypeNames.Null),
        [BuiltInTypeNames.String] = new PrimitiveTypeSymbol(BuiltInTypeNames.String),
        [BuiltInTypeNames.Void] = new PrimitiveTypeSymbol(BuiltInTypeNames.Void)
    };

    private readonly SymbolTable _symbols;

    /// <summary>
    /// Creates a resolver over a symbol table snapshot.
    /// </summary>
    public TypeResolver(SymbolTable symbols)
    {
        _symbols = symbols;
    }

    /// <summary>
    /// Resolves a type reference to a declared, primitive, or error symbol.
    /// </summary>
    public TypeSymbol Resolve(TypeReference reference)
    {
        if (TryResolveBuiltIn(reference.Name, out var builtInType))
        {
            return builtInType;
        }

        var type = _symbols.LookupType(reference.Name);
        if (type is DeclaredTypeSymbol declared)
        {
            var genericArgs = reference.GenericArguments
                .Select(Resolve)
                .ToImmutableCollection();

            return declared with { GenericArguments = genericArgs };
        }

        return new ErrorTypeSymbol($"Unknown type: {reference.Name}");
    }

    /// <summary>
    /// Resolves a predeclared language type by its surface syntax name.
    /// </summary>
    public TypeSymbol ResolveBuiltIn(string name)
    {
        return TryResolveBuiltIn(name, out var builtInType)
            ? builtInType
            : new ErrorTypeSymbol($"Unknown built-in type: {name}");
    }

    private static bool TryResolveBuiltIn(string name, out TypeSymbol builtInType)
    {
        if (BuiltInTypes.TryGetValue(name, out var primitiveType))
        {
            builtInType = primitiveType;
            return true;
        }

        builtInType = null!;
        return false;
    }
}
