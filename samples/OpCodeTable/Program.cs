using System.Reflection.Emit;
using System.Reflection.Metadata;



string[] header = ["Name", "Flow", "Operand", "Pop", "Push"];
var rows = OpCode.Enumerate()
    .OrderBy(opcode => opcode.ILOpCode)
    // .Where(opcode => opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch)
    .Select(opcode => (Opcode: opcode, Effect: opcode.GetStackEffect()))
    .Select(opcode => new object[] {
        opcode.Opcode.Name!,
        opcode.Opcode.FlowControl, opcode.Opcode.OperandType,
        opcode.Effect.PopCount?.ToString() ?? "var", opcode.Effect.PushCount?.ToString() ?? "var" })
// .OrderBy(row => row[1])
;
Markdown.FormatAsTable(header, rows);

