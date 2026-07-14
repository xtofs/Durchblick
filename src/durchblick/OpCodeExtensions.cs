namespace Durchblick;

using System.Reflection.Emit;
using System.Reflection.Metadata;

static class OpCodeExtensions
{
    extension(OpCode opcode)
    {
        public ILOpCode ILOpCode => (ILOpCode)(ushort)opcode.Value;
    }
}