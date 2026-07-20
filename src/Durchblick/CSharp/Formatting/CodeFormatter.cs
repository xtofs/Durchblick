namespace Durchblick.CSharp.Formatting;

using Durchblick.CSharp.Syntax;



public partial class CodeFormatter(TextWriter textWriter)
{
    private readonly IndentedTextWriter writer = new IndentedTextWriter(textWriter);

    public void Format(CompilationUnitDecl decl)
    {
        foreach (var ns in decl.Namespaces)
        {
            Format(ns);
        }
    }
    public void Format(NamespaceDecl decl)
    {
        writer.WriteLine($"namespace {decl.Name}");
        using (writer.WithIndentation("{", "}"))
        {
            foreach (var (i, member) in decl.Members.Select((m, i) => (i, m)))
            {
                if (i != 0) { writer.WriteLine(); }
                Format(member);
            }
        }
    }

    public void Format(TypeDecl type)
    {
        var keyword = type.Kind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            _ => throw new NotImplementedException($"Unknown type kind: {type.Kind}")
        };
        var typeParameterList = type.TypeParameters.FormatAsList("<", ", ", ">", false);
        writer.WriteLine($"{keyword} {type.Name}{typeParameterList}");
        using (writer.WithIndentation("{", "}"))
        {
            if (type.Kind == TypeKind.Enum)
            {
                foreach (var (i, member, f) in type.Members.Enumerate())
                {
                    FormatEnumMember(member, i, f);
                }
            }
            else
            {
                foreach (var (i, member, f) in type.Members.Enumerate())
                {
                    FormatMember(member, i, f);
                }
            }
        }
    }

    private void FormatEnumMember(MemberDecl member, bool isInitial, bool isFinal)
    {
        if (!isInitial) { writer.WriteLine(); }
        writer.WriteLine($"{member.Name}{(isFinal ? "" : ",")}");
    }

    private void FormatMember(MemberDecl member, bool isInitial, bool isFinal)
    {
        if (!isInitial) { writer.WriteLine(); }
        var mods = member.Modifiers.FormatAsList("", " ", " "); // separated by space and additional space if not empty
        writer.Write($"{mods}{member.TypeReference.Name} {member.Name} ");
        switch (member.Kind)
        {
            case MemberKind.Field:
                writer.WriteLine(";");
                break;
            case MemberKind.Property:
                writer.WriteLine("{ get; set; }");
                break;
            case MemberKind.Method:
                var parameters = member.Parameters.Select(p => $"{p.TypeReference.Name}: {p.Name}");
                writer.WriteLine($"({string.Join(", ", parameters)}) {{ /* ... */ }}");
                break;
            default:
                throw new NotImplementedException($"Formatter not implemented for member kind {member.Kind}");
        }
    }
}
