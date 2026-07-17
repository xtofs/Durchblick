namespace CSharpCodeModel.Semantics;

using CSharpCodeModel.Collections;
using CSharpCodeModel.Syntax;

// These are the “named entities” in a program.

public abstract record Symbol
{
    private protected Symbol(string name) => Name = name;

    public string Name { get; init; }
}

public sealed record NamespaceSymbol(
    string Name,
    ImmutableCollection<NamespaceSymbol> Namespaces,
    ImmutableCollection<TypeSymbol> Types
) : Symbol(Name);

public abstract record TypeSymbol : Symbol
{
    private protected TypeSymbol(string name) : base(name) { }
}

public sealed record DeclaredTypeSymbol(
    TypeDecl Declaration,
    ImmutableCollection<TypeSymbol> GenericArguments
) : TypeSymbol(Declaration.Name);

public sealed record PrimitiveTypeSymbol(string Name) : TypeSymbol(Name);

public sealed record ErrorTypeSymbol(string Message) : TypeSymbol("<error>");

public abstract record MemberSymbol(string Name) : Symbol(Name);

public sealed record MethodSymbol(
    MemberDecl Declaration,
    TypeSymbol ReturnType,
    ImmutableCollection<ParameterSymbol> Parameters
) : MemberSymbol(Declaration.Name);

public sealed record PropertySymbol(
    MemberDecl Declaration,
    TypeSymbol Type
) : MemberSymbol(Declaration.Name);

public sealed record FieldSymbol(
    MemberDecl Declaration,
    TypeSymbol Type
) : MemberSymbol(Declaration.Name);

public sealed record EventSymbol(
    MemberDecl Declaration,
    TypeSymbol Type
) : MemberSymbol(Declaration.Name);

public sealed record ParameterSymbol(
    ParameterDecl Declaration,
    TypeSymbol Type
) : Symbol(Declaration.Name);


public sealed record LocalSymbol(
    VariableDecl Declaration,
    TypeSymbol Type
) : Symbol(Declaration.Name);

public sealed record TypeParameterSymbol(
    TypeParameterDecl Declaration
) : Symbol(Declaration.Name);

