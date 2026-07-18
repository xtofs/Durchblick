using System.Reflection.Emit;
using System.Reflection.Metadata;



string[] header = ["Name", "Flow", "Operand"];
var rows = OpCode.Enumerate()
    .OrderBy(opcode => opcode.ILOpCode)
    // .Where(opcode => opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch)
    .Select(opcode => new object[] { opcode.Name!, opcode.FlowControl, opcode.OperandType })
// .OrderBy(row => row[1])
;
Markdown.FormatAsTable(header, rows);


static class OpCodeExtensions
{
    extension(OpCode opcode)
    {
        public ILOpCode ILOpCode => (ILOpCode)(ushort)opcode.Value;
    }
}