namespace Durchblick;


/// <summary>
/// A maximal straight-line sequence of instructions: control enters only at the first
/// instruction and leaves only at the last. Pure data; construction lives in
/// <see cref="BasicBlockBuilder"/>.
/// </summary>
public sealed record BasicBlock(int StartOffset, IReadOnlyList<Instruction> Instructions)
{
    public Instruction Exit => this.Instructions[^1];
    public IReadOnlyList<BasicBlock> Targets { get; internal set; } = [];
}