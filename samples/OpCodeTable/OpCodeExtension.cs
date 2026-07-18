using System.Reflection;
using System.Reflection.Emit;

public static class OpCodeExtension
{
    extension(OpCode)
    {
        public static IEnumerable<OpCode> Enumerate()
        {
            foreach (var property in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var opcode = (OpCode)property.GetValue(null)!;
                yield return opcode;
            }
        }
    }
}