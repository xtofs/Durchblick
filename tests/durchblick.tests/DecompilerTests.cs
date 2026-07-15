namespace Durchblick.Tests;

using System.Reflection;

/// <summary>
/// Data-driven tests over specimen methods. Each test is annotated with the specimen's assembly,
/// type, and method via <see cref="SpecimenAttribute"/> and receives the decoded instruction list
/// (and, where needed, the resolved <see cref="MethodInfo"/>). Together they cover the pipeline the
/// <c>cli</c> project runs: decode → basic blocks → expression reconstruction.
/// </summary>
public class DecompilerTests
{
    [Theory]
    [Specimen("add", "specimen.Class1", "Calculate3")]
    public void Splits_into_basic_blocks(IReadOnlyList<Instruction> instructions)
    {
        var blocks = BasicBlockBuilder.Build(instructions);

        Assert.Equal(
            [0x0000, 0x0009, 0x0012, 0x0018],
            blocks.Select(block => block.StartOffset));

        // The entry block ends at the conditional branch that splits the `if`.
        Assert.Equal("brfalse.s", blocks[0].Exit.OpCode.Name);
    }

    [Theory]
    [Specimen("add", "specimen.Class1", "Calculate3")]
    public void Reconstructs_conditional_expression(MethodInfo method, IReadOnlyList<Instruction> instructions)
    {
        var blocks = BasicBlockBuilder.Build(instructions).ToDictionary(block => block.StartOffset);
        Decompiler.GetParametersAndLocals(method, out var parameters, out var locals);

        var expression = Decompiler.ToExpression(blocks, parameters, locals);

        // `if (a > 3) return (a + b) * 2; return a + b;` reconstructs to a ternary.
        Assert.Equal("IIF((a > 3), ((a + b) * 2), (a + b))", expression?.ToString());
    }
}
