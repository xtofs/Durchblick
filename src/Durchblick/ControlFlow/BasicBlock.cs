namespace Durchblick.ControlFlow;

using Durchblick.IL;


/// <summary>
/// A maximal straight-line sequence of instructions: control enters only at the first
/// instruction and leaves only at the last. Pure data; construction lives in
/// <see cref="BasicBlockBuilder"/>.
/// </summary>
public sealed record BasicBlock
{
    public BasicBlock(int StartOffset, IReadOnlyList<Instruction> instructions)
    {
        this.StartOffset = StartOffset;
        var last = instructions[^1];
        switch (last)
        {
            case ILCode.Branch:
                this.Exit = new Branch(last.TrueTarget, last.FalseTarget);
                this.Instructions = instructions[..^1];
                break;
            case ILCode.Goto:
                this.Exit = new Goto();
                this.Instructions = instructions[..^1];
                break;
            case ILCode.Fallthrough:
                this.Exit = new Fallthrough();
                this.Instructions = instructions;
                break;
            case ILCode.Return:
                this.Exit = new Return();
                this.Instructions = instructions[..^1];
                break;
        }
        this.Instructions = instructions;
    }


    public Terminator Exit { get; set; }

    public IReadOnlyList<BasicBlock> Targets()
    {
        return this.Exit switch
        {
            Branch branch => [branch.TrueTarget, branch.FalseTarget],
            Goto goto_ => [goto_.Target],
            Fallthrough fallthrough => [fallthrough.Target],
            Return => [],
            _ => throw new InvalidOperationException("Unknown terminator type")
        };
    }
}

public abstract class Terminator { }

public class Branch : Terminator
{
    public Block TrueTarget;
    public Block FalseTarget;
}

public class Goto : Terminator
{
    public Block Target;
}

public class Return : Terminator { }

public class Fallthrough : Terminator
{
    public Block Target;
}