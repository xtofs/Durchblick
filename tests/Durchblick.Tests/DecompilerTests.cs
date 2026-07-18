namespace Durchblick.Tests;

using System.Reflection.Emit;
using Durchblick.ControlFlow;
using Durchblick.IL;

/// <summary>
/// Data-driven tests over specimen methods. Each test is annotated with the specimen's assembly,
/// type, and method via <see cref="SpecimenAttribute"/> and receives the decoded instruction list
/// (and, where needed, the resolved <see cref="System.Reflection.MethodInfo"/>). Together they cover
/// decoded IL, basic block partitioning, and CFG edge behavior.
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

    [Fact]
    public void Conditional_branch_preserves_duplicate_successor_edges()
    {
        Instruction[] instructions =
        [
            new(0, OpCodes.Ldc_I4_0, Operand.None),
            new(1, OpCodes.Brfalse_S, Operand.ForBranchTarget(2, OperandType.ShortInlineBrTarget)),
            new(2, OpCodes.Ret, Operand.None),
        ];

        var blocks = BasicBlockBuilder.Build(instructions);

        Assert.Equal([1, 1], blocks[0].Successors);
    }

    [Fact]
    public void Switch_successors_follow_case_order_then_fallthrough()
    {
        Instruction[] instructions =
        [
            new(0, OpCodes.Ldc_I4_0, Operand.None),
            new(1, OpCodes.Switch, new Operand([6, 4])),
            new(2, OpCodes.Ret, Operand.None),
            new(4, OpCodes.Ret, Operand.None),
            new(6, OpCodes.Ret, Operand.None),
        ];

        var blocks = BasicBlockBuilder.Build(instructions);
        var successorOffsets = blocks[0].Successors
            .Select(successor => instructions[blocks[successor].StartIndex].Offset);

        Assert.Equal([6, 4, 2], successorOffsets);
    }
}
