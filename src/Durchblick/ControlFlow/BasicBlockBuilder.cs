namespace Durchblick.ControlFlow;

using System.Reflection;
using System.Reflection.Emit;
using Durchblick.IL;

/// <summary>
/// A maximal straight-line run of instructions, identified by inclusive instruction indices into
/// <see cref="ControlFlowGraph.Instructions"/>. <see cref="Successors"/> contains block indices
/// into <see cref="ControlFlowGraph.Blocks"/>.
/// </summary>
/// <remarks>
/// Successors are ordered edges, not a set: branch target(s) come first (for <c>switch</c>, in
/// case order), the fall-through comes last. Two edges may lead to the same block.
/// </remarks>
public readonly record struct BasicBlock(int StartIndex, int EndIndex, IReadOnlyList<int> Successors);

public record ControlFlowGraph(Instruction[] Instructions, IReadOnlyList<BasicBlock> Blocks);

/// <summary>
/// Builds <see cref="BasicBlock"/>s from a method's instructions using the classic
/// leader algorithm.
/// </summary>
/// <remarks>Exception regions are not considered yet; handler entries are not treated as leaders.</remarks>
public static class BasicBlockBuilder
{

    /// <summary>
    /// Decodes the method body into a linear instruction list and partitions it into basic blocks.
    /// </summary>
    public static ControlFlowGraph Build(MethodInfo methodInfo)
    {
        var reader = new ILReader(methodInfo);
        var instructions = reader.ToInstructions().ToArray();
        var blocks = Build(instructions);
        return new ControlFlowGraph(instructions, blocks);
    }

    public static IReadOnlyList<BasicBlock> Build(IReadOnlyList<Instruction> instructions)
    {
        if (instructions.Count == 0)
        {
            return [];
        }

        // Branch operands carry byte offsets; everything below works in instruction indices.
        var offsetToIndex = Enumerable.Range(0, instructions.Count)
            .ToDictionary(i => instructions[i].Offset, i => i);

        // Step 1 — find the leaders: instruction indices that start a block (ascending).
        var leaders = FindLeaders(instructions, offsetToIndex);

        // Step 2 — form blocks by slicing the instruction list between leaders.
        var starts = leaders.ToArray();
        var blockIndexByLeader = starts
            .Select((leader, blockIndex) => (leader, blockIndex))
            .ToDictionary(pair => pair.leader, pair => pair.blockIndex);

        // Step 3 — compute successors (as block indices) and construct the blocks.
        var blocks = new List<BasicBlock>(starts.Length);
        for (var blockIndex = 0; blockIndex < starts.Length; blockIndex++)
        {
            var start = starts[blockIndex];
            var end = blockIndex + 1 < starts.Length ? starts[blockIndex + 1] - 1 : instructions.Count - 1;
            blocks.Add(new BasicBlock(start, end, FindSuccessors(instructions[end], blockIndex)));
        }

        return blocks;

        IReadOnlyList<int> FindSuccessors(Instruction exit, int blockIndex)
        {
            var hasFallThrough = blockIndex + 1 < starts.Length;
            switch (exit.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    return [blockIndexByLeader[offsetToIndex[exit.Operand.GetBranchTarget()]]];

                case FlowControl.Cond_Branch:
                    // Successors are edges, not a set: branch target(s) first (for switch, in case
                    // order), then the fall-through. A target equal to the fall-through block
                    // produces two entries on purpose.
                    var targets = BranchTargetOffsets(exit)
                        .Select(offset => blockIndexByLeader[offsetToIndex[offset]]);
                    if (hasFallThrough)
                    {
                        targets = targets.Append(blockIndex + 1);
                    }
                    return targets.ToArray();

                case FlowControl.Return:
                case FlowControl.Throw:
                    return [];

                default:
                    return hasFallThrough ? [blockIndex + 1] : [];
            }
        }
    }

    private static SortedSet<int> FindLeaders(IReadOnlyList<Instruction> instructions, Dictionary<int, int> offsetToIndex)
    {
        var leaders = new SortedSet<int> { 0 };

        for (var i = 0; i < instructions.Count; i++)
        {
            switch (instructions[i].OpCode.FlowControl)
            {
                case FlowControl.Branch:
                case FlowControl.Cond_Branch:
                    foreach (var offset in BranchTargetOffsets(instructions[i]))
                    {
                        leaders.Add(offsetToIndex[offset]);
                    }

                    AddNextAsLeader(i);
                    break;

                case FlowControl.Return:
                case FlowControl.Throw:
                    AddNextAsLeader(i);
                    break;
            }
        }

        return leaders;

        void AddNextAsLeader(int i)
        {
            if (i + 1 < instructions.Count)
            {
                leaders.Add(i + 1);
            }
        }
    }

    /// <summary>Branch target byte offsets of a branch or switch instruction.</summary>
    private static int[] BranchTargetOffsets(Instruction instruction) =>
        instruction.Operand.OperandType == OperandType.InlineSwitch
            ? instruction.Operand.GetSwitchTargets()
            : [instruction.Operand.GetBranchTarget()];
}
