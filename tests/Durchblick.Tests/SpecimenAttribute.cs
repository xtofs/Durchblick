namespace Durchblick.Tests;

using System.Reflection;
using Durchblick.IL;
using Xunit.Sdk;

/// <summary>
/// Data attribute that decompiles a specimen method and feeds it to a <c>[Theory]</c>. Annotate a
/// test with the specimen's assembly, type, and method name; the test receives inputs decoded from
/// that method, matched to its parameters by type:
/// <list type="bullet">
/// <item><see cref="IReadOnlyList{Instruction}"/> (or <c>Instruction[]</c>) — the decoded instruction list.</item>
/// <item><see cref="MethodInfo"/> / <see cref="MethodBase"/> — the resolved specimen method (for parameters and locals).</item>
/// </list>
/// </summary>
/// <example><code>
/// [Theory]
/// [Specimen("add", "specimen.Class1", "Calculate3")]
/// public void MyTest(IReadOnlyList&lt;Instruction&gt; instructions) { ... }
/// </code></example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SpecimenAttribute(string assembly, string type, string method) : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var specimen = SpecimenLoader.Resolve(assembly, type, method);
        var arguments = testMethod.GetParameters()
            .Select(parameter => Supply(parameter.ParameterType, specimen))
            .ToArray();
        return [arguments];
    }

    private static object Supply(Type parameterType, MethodInfo specimen)
    {
        if (parameterType == typeof(MethodInfo) || parameterType == typeof(MethodBase))
        {
            return specimen;
        }

        if (parameterType.IsAssignableFrom(typeof(List<Instruction>)) || parameterType == typeof(Instruction[]))
        {
            var instructions = new ILReader(specimen).ToInstructions().ToList();
            return parameterType == typeof(Instruction[]) ? instructions.ToArray() : instructions;
        }

        throw new ArgumentException(
            $"[Specimen] cannot supply a value for a parameter of type {parameterType}. " +
            $"Supported: IReadOnlyList<Instruction>, Instruction[], MethodInfo, MethodBase.");
    }
}
