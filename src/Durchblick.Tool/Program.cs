using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Durchblick.CSharp.Formatting;
using Durchblick.CSharp.Syntax;
using Durchblick.Decompilation;

internal static class Program
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 1 ? 0 : 1;
        }

        var assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
            return 1;
        }

        try
        {
            var assembly = LoadAssembly(assemblyPath);
            var unit = BuildCompilationUnit(assembly);

            new CodeFormatter(Console.Out).Format($"{unit}");
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or NotSupportedException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: durchblick <assembly-path>");
    }

    private static CompilationUnitDecl BuildCompilationUnit(Assembly assembly)
    {
        var namespaces = assembly
            .GetTypes()
            .Where(type => type.Namespace is not null && !type.IsNested && !IsCompilerGenerated(type))
            .GroupBy(type => type.Namespace ?? string.Empty)
            .OrderBy(group => group.Key)
            .Select(group => Declaration.Namespace(group.Key, group.OrderBy(type => type.Name).Select(TryBuildType).OfType<TypeDecl>()))
            .Where(@namespace => @namespace.Members.Count > 0);

        return Declaration.CompilationUnit(namespaces);
    }

    private static Assembly LoadAssembly(string assemblyPath)
    {
        var resolver = new AssemblyDependencyResolver(assemblyPath);
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            var resolvedPath = resolver.ResolveAssemblyToPath(assemblyName);
            return resolvedPath is null ? null : context.LoadFromAssemblyPath(resolvedPath);
        };

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    }

    private static TypeDecl? TryBuildType(Type type)
    {
        var backingFields = type
            .GetFields(DeclaredMembers)
            .Where(IsAutoPropertyBackingField)
            .OrderBy(field => field.MetadataToken)
            .Select(BuildBackingField);

        var properties = type
            .GetProperties(DeclaredMembers)
            .Select(TryBuildProperty)
            .OfType<MemberDecl>()
            .OrderBy(property => property.Name);

        var methods = type
            .GetMethods(DeclaredMembers)
            .Where(method => !method.IsSpecialName)
            .OrderBy(method => method.MetadataToken)
            .Select(TryBuildMethod)
            .OfType<MemberDecl>()
            .ToArray();

        var members = backingFields.Concat(properties).Concat(methods).ToArray();

        if (members.Length == 0)
        {
            return null;
        }

        return Declaration.Class(type.Name, members, modifiers: TypeModifiers(type));
    }

    private static bool IsCompilerGenerated(MemberInfo member)
        => member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            || member.Name.StartsWith("<", StringComparison.Ordinal);

    private static MemberDecl? TryBuildMethod(MethodInfo method)
    {
        try
        {
            return BuildMethod(method);
        }
        catch (UnsupportedInstructionException ex)
        {
            Console.Error.WriteLine($"Skipping {method.DeclaringType?.FullName}.{method.Name} {ex.Message}");
            foreach (var instruction in ex.BlockInstructions)
            {
                Console.Error.WriteLine($"  {instruction}");
            }

            return null;
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine($"Skipping {method.DeclaringType?.FullName}.{method.Name} {ex.Message}");
            return null;
        }
    }

    private static MemberDecl BuildMethod(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Select(parameter => Declaration.Parameter(parameter.Name ?? $"arg{parameter.Position}", Declaration.TypeRef(parameter.ParameterType), []));

        return Declaration.Method(
            method.Name,
            Declaration.TypeRef(method.ReturnType),
            Decompiler.DecompileBody(method),
            parameters,
            MethodModifiers(method));
    }

    private static MemberDecl? TryBuildProperty(PropertyInfo property)
    {
        var backingField = FindAutoPropertyBackingField(property);
        if (backingField is null)
        {
            return null;
        }

        var accessors = new List<AccessorDecl>();
        var backingFieldReference = Expression.Identifier(SanitizeBackingFieldName(backingField.Name), new SymbolReference(backingField.Name, SymbolKind.Field));

        if (property.GetMethod is not null)
        {
            accessors.Add(Declaration.Accessor(
                AccessorKind.Get,
                Statement.Block([Statement.Return(backingFieldReference)])));
        }

        if (property.SetMethod is not null)
        {
            accessors.Add(Declaration.Accessor(
                IsInitOnlySetter(property.SetMethod) ? AccessorKind.Init : AccessorKind.Set,
                Statement.Block([
                    Statement.Expr(Expression.Assign(
                        backingFieldReference,
                        Expression.Identifier("value", new SymbolReference("value", SymbolKind.Parameter))))
                ])));
        }

        return Declaration.Property(
            property.Name,
            Declaration.TypeRef(property.PropertyType),
            accessors,
            PropertyModifiers(property));
    }

    private static MemberDecl BuildBackingField(FieldInfo field)
        => Declaration.Field(
            SanitizeBackingFieldName(field.Name),
            Declaration.TypeRef(field.FieldType),
            FieldModifiers(field));

    private static IEnumerable<Modifier> TypeModifiers(Type type)
    {
        if (type.IsPublic)
        {
            yield return Modifier.Public;
        }
    }

    private static IEnumerable<Modifier> MethodModifiers(MethodInfo method)
    {
        if (method.IsPublic)
        {
            yield return Modifier.Public;
        }
    }

    private static IEnumerable<Modifier> PropertyModifiers(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        if (accessor?.IsPublic == true)
        {
            yield return Modifier.Public;
        }
    }

    private static IEnumerable<Modifier> FieldModifiers(FieldInfo field)
    {
        if (field.IsPrivate)
        {
            yield return Modifier.Private;
        }

        if (field.IsStatic)
        {
            yield return new Modifier(ModifierKind.Static);
        }

        if (field.IsInitOnly)
        {
            yield return new Modifier(ModifierKind.ReadOnly);
        }
    }

    private static FieldInfo? FindAutoPropertyBackingField(PropertyInfo property)
    {
        var expectedName = $"<{property.Name}>k__BackingField";
        var field = property.DeclaringType?
            .GetField(expectedName, DeclaredMembers);
        return field is not null && IsAutoPropertyBackingField(field) ? field : null;
    }

    private static bool IsAutoPropertyBackingField(FieldInfo field)
        => field.Name.StartsWith("<", StringComparison.Ordinal)
            && field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal)
            && field.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    private static string SanitizeBackingFieldName(string name)
        => name.StartsWith("<", StringComparison.Ordinal) && name.EndsWith(">k__BackingField", StringComparison.Ordinal)
            ? name[1..].Replace(">k__", "__", StringComparison.Ordinal)
            : name;

    private static bool IsInitOnlySetter(MethodInfo setter)
        => setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
