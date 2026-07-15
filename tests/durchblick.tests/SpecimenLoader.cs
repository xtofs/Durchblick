namespace Durchblick.Tests;

using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// Resolves a <see cref="MethodInfo"/> from a specimen assembly by name. The specimen assemblies
/// are copied next to the test binaries via project references, so they load from the test's base
/// directory.
/// </summary>
internal static class SpecimenLoader
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static MethodInfo Resolve(string assemblyName, string typeName, string methodName)
    {
        var assembly = Load(assemblyName);

        var type = assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' not found in assembly '{assemblyName}'.");

        return type.GetMethod(methodName, AnyMember)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on '{typeName}'.");
    }

    private static Assembly Load(string assemblyName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
        return File.Exists(path)
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path)
            : Assembly.Load(new AssemblyName(assemblyName));
    }
}
