namespace Durchblick.Tests;

using System.Reflection.Emit;
using Durchblick.ControlFlow;
using Durchblick.IL;

/// <summary>
/// Data-driven tests over specimen methods. Each test is annotated with the specimen's assembly,
/// type, and method via <see cref="SpecimenAttribute"/> and receives the decoded instruction list
/// (and, where needed, the resolved <see cref="System.Reflection.MethodInfo"/>). Together they cover the pipeline the
/// <c>samples/disassemble</c> project runs: decode → basic blocks → expression reconstruction.
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
            blocks.Select(block => instructions[block.StartIndex].Offset));

        // The entry block ends at the conditional branch that splits the `if`.
        Assert.Equal("brfalse.s", instructions[blocks[0].EndIndex].OpCode.Name);
    }

    [Theory]
    [Specimen("add", "specimen.Class1", "Calculate")]
    [Specimen("add", "specimen.Class1", "Calculate2")]
    [Specimen("add", "specimen.Class1", "Calculate3")]
    public void Successor_count_matches_the_exit_instruction(IReadOnlyList<Instruction> instructions)
    {
        var blocks = BasicBlockBuilder.Build(instructions);

        Assert.All(blocks, block =>
        {
            var expected = instructions[block.EndIndex].OpCode.FlowControl switch
            {
                FlowControl.Branch => 1,                        // unconditional: the target only
                FlowControl.Cond_Branch => 2,                   // the target plus the fall-through
                FlowControl.Return or FlowControl.Throw => 0,   // leaves the method
                _ => 1,                                         // plain fall-through into the next block
            };

            Assert.Equal(expected, block.Successors.Count);
        });
    }
}
