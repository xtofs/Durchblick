namespace Durchblick.Tests;

using System.Reflection;

/// <summary>
/// Resolves a specimen <see cref="MethodInfo"/> by type and method name from this test assembly.
/// The specimen sources are compiled into the test project (see <c>Specimens.cs</c>), so the tests
/// reflect over their own assembly with no external project dependency.
/// </summary>
internal static class SpecimenLoader
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static MethodInfo Resolve(string typeName, string methodName)
    {
        var assembly = typeof(SpecimenLoader).Assembly;

        var type = assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Specimen type '{typeName}' not found in assembly '{assembly.GetName().Name}'.");

        return type.GetMethod(methodName, AnyMember)
            ?? throw new InvalidOperationException($"Specimen method '{methodName}' not found on '{typeName}'.");
    }
}
