namespace Durchblick.Decompilation;

using Durchblick.IL;

public sealed class UnsupportedInstructionException : NotSupportedException
{
    public UnsupportedInstructionException(
        string message,
        Instruction instruction,
        IReadOnlyList<Instruction> blockInstructions)
        : base(message)
    {
        Instruction = instruction;
        BlockInstructions = blockInstructions;
    }

    public Instruction Instruction { get; }

    public IReadOnlyList<Instruction> BlockInstructions { get; }
}