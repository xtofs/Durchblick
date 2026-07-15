namespace Durchblick;

using System.Reflection;
using System.Reflection.Emit;

/// <summary>
/// Builds <see cref="BasicBlock"/>s from a method's instructions using the classic
/// leader algorithm.
/// </summary>
/// <remarks>Exception regions are not considered yet; handler entries are not treated as leaders.</remarks>
public static class BasicBlockBuilder
{
    public static IReadOnlyList<BasicBlock> Build(MethodInfo methodInfo)
    {
        var reader = new ILReader(methodInfo);
        var instructions = reader.ToInstructions().ToList();
        return Build(instructions);
    }



    public static IReadOnlyList<BasicBlock> Build(IReadOnlyList<Instruction> instructions)
    {
        // var leaders = FindLeaders(instructions);

        var blocks = new List<BasicBlock>();
        var current = new List<Instruction>();

        for (var i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];
            if (BranchingFlowControl.Contains(instruction.OpCode.FlowControl))
            {
                var block = new BasicBlock(current[0].Offset, [.. current, instruction]);
                blocks.Add(block);
                current = [];
            }
            else
            {
                current.Add(instruction);
            }
        }

        foreach (var block in blocks)
        {
            block.Targets = FindSuccessorBlocks(block, blocks).ToList();
        }

        return blocks;
    }
    private static HashSet<FlowControl> BranchingFlowControl = [FlowControl.Branch, FlowControl.Cond_Branch, FlowControl.Return, FlowControl.Throw];

    private static IEnumerable<BasicBlock> FindSuccessorBlocks(BasicBlock block, List<BasicBlock> blocks)
    {
        switch (block.Exit.OpCode.FlowControl)
        {
            case FlowControl.Branch:
                var targetOffset = block.Instructions[^1].Operand.GetBranchTarget();
                return blocks.Where(b => b.StartOffset == targetOffset);
            case FlowControl.Cond_Branch:
                targetOffset = block.Instructions[^1].Operand.GetBranchTarget();
                return blocks.Where(b => b.StartOffset == targetOffset);
            case FlowControl.Return:
            case FlowControl.Throw:
                return [];
            case FlowControl.Next:
                return [];
            default:
                throw new InvalidOperationException($"Unexpected flow control: {block.Instructions[^1].OpCode.FlowControl}");
        }
    }

    private static HashSet<int> FindLeaders(IReadOnlyList<Instruction> instructions)
    {
        var leaders = new HashSet<int>();
        if (instructions.Count == 0)
        {
            return leaders;
        }

        leaders.Add(instructions[0].Offset);

        for (var i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];
            switch (instruction.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                case FlowControl.Cond_Branch:
                    if (instruction.Operand.OperandType == OperandType.InlineSwitch)
                    {
                        leaders.UnionWith(instruction.Operand.GetSwitchTargets());
                    }
                    else
                    {
                        leaders.Add(instruction.Operand.GetBranchTarget());
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
                leaders.Add(instructions[i + 1].Offset);
            }
        }
    }
}
