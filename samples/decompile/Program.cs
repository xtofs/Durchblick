using System.Reflection;
using Durchblick.CSharp.Formatting;
using Durchblick.CSharp.Syntax;
using Durchblick.Decompilation;

internal class Program
{
    // Reconstruct and print only the specimen methods that live in this namespace.
    private const string SpecimenNamespace = "specimen";

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static void Main()
    {
        var classes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type.Namespace == SpecimenNamespace)
            .Select(BuildClass)
            .ToArray();

        var unit = Declaration.CompilationUnit([Declaration.Namespace(SpecimenNamespace, classes)]);

        // The CodeFormatter is a work in progress: statements it does not handle yet throw
        // NotImplementedException. Report that instead of crashing, so it is clear how far the
        // reconstructed source rendered.
        try
        {
            new CodeFormatter(Console.Out).Format(unit);
        }
        catch (NotImplementedException ex)
        {
            Console.WriteLine();
            Console.WriteLine($"// formatter incomplete: {ex.Message}");
        }
    }

    private static TypeDecl BuildClass(Type type)
    {
        var methods = type.GetMethods(DeclaredMembers).Select(BuildMethod);
        return Declaration.Class(type.Name, methods);
    }

    private static MemberDecl BuildMethod(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Select(parameter => Declaration.Parameter(parameter.Name ?? $"arg{parameter.Position}", TypeRef(parameter.ParameterType), []));

        var modifiers = method.IsPublic ? new[] { Modifier.Public } : [];

        return Declaration.Method(
            method.Name,
            TypeRef(method.ReturnType),
            Decompiler.DecompileBody(method),
            parameters,
            modifiers);
    }

    private static TypeReference TypeRef(Type type) => Declaration.TypeRef(FriendlyName(type));

    private static string FriendlyName(Type type) =>
        type == typeof(int) ? "int"
        : type == typeof(long) ? "long"
        : type == typeof(bool) ? "bool"
        : type == typeof(double) ? "double"
        : type == typeof(float) ? "float"
        : type == typeof(string) ? "string"
        : type == typeof(void) ? "void"
        : type.Name;
}
