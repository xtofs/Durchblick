namespace Durchblick.CSharp.Syntax;

using Durchblick.Collections;


public abstract record Declaration : AstNode
{
    private protected Declaration() { }

    // ─────────────────────────────────────────────
    // Factory Methods
    // ─────────────────────────────────────────────

    public static CompilationUnitDecl CompilationUnit(IEnumerable<NamespaceDecl> namespaces)
        => new CompilationUnitDecl([.. namespaces]);

    public static NamespaceDecl Namespace(string name, IEnumerable<TypeDecl> members)
        => new NamespaceDecl(name, [.. members]);

    public static TypeReference TypeRef(
        string name,
        string? @namespace = null,
        IEnumerable<TypeReference>? genericArguments = null)
        => new TypeReference(name, @namespace, [.. (genericArguments ?? [])]);

    /// <summary>Maps a runtime <see cref="Type"/> to a structured type reference.</summary>
    public static TypeReference TypeRef(Type type)
        => TypeRef(MetadataTypeName(type), type.Namespace, type.IsGenericType ? type.GetGenericArguments().Select(TypeRef) : null);

    private static string MetadataTypeName(Type type) => StripGenericArity(type.Name);

    private static string StripGenericArity(string name)
    {
        var arityMarker = name.IndexOf('`', StringComparison.Ordinal);
        return arityMarker < 0 ? name : name[..arityMarker];
    }

    public static TypeDecl Type(
        TypeKind kind,
        string name,
        IEnumerable<TypeParameterDecl> typeParameters,
        IEnumerable<TypeReference> baseTypes,
        IEnumerable<MemberDecl> members,
        IEnumerable<Modifier> modifiers,
        IEnumerable<AttributeDecl> attributes)
        => new(
            kind,
            name,
            [.. typeParameters],
            [.. baseTypes],
            [.. members],
            [.. modifiers],
            [.. attributes]
        );

    public static TypeDecl Enum(
        string name,
        IEnumerable<MemberDecl> members,
        IEnumerable<Modifier>? modifiers = null,
        IEnumerable<AttributeDecl>? attributes = null)
        => Type(
            TypeKind.Enum,
            name,
            [],
            [],
            members,
            modifiers ?? [],
            attributes ?? []);

    public static TypeDecl Class(
        string name,
        IEnumerable<MemberDecl> members,
        IEnumerable<TypeParameterDecl>? typeParameters = null,
        IEnumerable<TypeReference>? baseTypes = null,
        IEnumerable<Modifier>? modifiers = null,
        IEnumerable<AttributeDecl>? attributes = null)
        => Type(
            TypeKind.Class,
            name,
            typeParameters ?? [],
            baseTypes ?? [],
            members,
            modifiers ?? [],
            attributes ?? []);

    public static MemberDecl Member(
        MemberKind kind,
        string name,
        TypeReference type,
        IEnumerable<ParameterDecl> parameters,
        Statement? body,
        IEnumerable<AccessorDecl> accessors,
        IEnumerable<Modifier> modifiers,
        IEnumerable<AttributeDecl> attributes)
        => new(
            kind,
            name,
            type,
            [.. parameters],
            body,
            [.. accessors],
            [.. modifiers],
            [.. attributes]
        );

    public static MemberDecl Field(
        string name,
        TypeReference type,
        IEnumerable<Modifier>? modifiers = null,
        IEnumerable<AttributeDecl>? attributes = null)
        => Member(
            MemberKind.Field,
            name,
            type,
            [],
            null,
            [],
            modifiers ?? [],
            attributes ?? []);

    public static MemberDecl Property(
        string name,
        TypeReference type,
        IEnumerable<AccessorDecl>? accessors = null,
        IEnumerable<Modifier>? modifiers = null,
        IEnumerable<AttributeDecl>? attributes = null)
        => Member(
            MemberKind.Property,
            name,
            type,
            [],
            null,
            accessors ?? [],
            modifiers ?? [],
            attributes ?? []);

    public static MemberDecl Method(
        string name,
        TypeReference returnType,
        Statement body,
        IEnumerable<ParameterDecl>? parameters = null,
        IEnumerable<Modifier>? modifiers = null,
        IEnumerable<AttributeDecl>? attributes = null)
        => Member(
            MemberKind.Method,
            name,
            returnType,
            parameters ?? [],
            body,
            [],
            modifiers ?? [],
            attributes ?? []);

    public static AccessorDecl Accessor(AccessorKind kind, Statement body)
        => new(kind, body);

    public static VariableDecl Variable(TypeReference type, string name, Expression? initializer)
        => new(type, name, initializer);

    public static ParameterDecl Parameter(string name, TypeReference type, IEnumerable<Modifier> modifiers)
        => new(name, type, [.. modifiers]);

    public static TypeParameterDecl TypeParameter(string name, IEnumerable<TypeReference> constraints)
        => new(name, [.. constraints]);
}

public sealed record CompilationUnitDecl(
    ImmutableCollection<NamespaceDecl> Namespaces
) : Declaration
{
}

public sealed record NamespaceDecl(
    string Name,
    ImmutableCollection<TypeDecl> Members
) : Declaration
{

}


public sealed record TypeDecl(
    TypeKind Kind,
    string Name,
    ImmutableCollection<TypeParameterDecl> TypeParameters,
    ImmutableCollection<TypeReference> BaseTypes,
    ImmutableCollection<MemberDecl> Members,
    ImmutableCollection<Modifier> Modifiers,
    ImmutableCollection<AttributeDecl> Attributes
) : Declaration
{

}


public sealed record MemberDecl(
    MemberKind Kind,
    string Name,
    TypeReference TypeReference,
    ImmutableCollection<ParameterDecl> Parameters,
    Statement? Body,
    ImmutableCollection<AccessorDecl> Accessors,
    ImmutableCollection<Modifier> Modifiers,
    ImmutableCollection<AttributeDecl> Attributes
) : Declaration
{

}

public sealed record AccessorDecl(
    AccessorKind Kind,
    Statement Body
) : Declaration;

public sealed record VariableDecl(
    TypeReference TypeReference,
    string Name,
    Expression? Initializer
) : Declaration;

public sealed record ParameterDecl(
    string Name,
    TypeReference TypeReference,
    ImmutableCollection<Modifier> Modifiers
) : Declaration;

public sealed record TypeParameterDecl(
    string Name,
    ImmutableCollection<TypeReference> Constraints
) : Declaration;

public sealed record AttributeDecl(
    TypeReference TypeReference,
    ImmutableCollection<Expression> Arguments
) : Declaration;
