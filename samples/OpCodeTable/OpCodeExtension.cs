using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

public static class OpCodeExtensions
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

    extension(OpCode opcode)
    {
        public ILOpCode ILOpCode => (ILOpCode)(ushort)opcode.Value;

        public (int? PopCount, int? PushCount) GetStackEffect()
        {
            // Defensive approach to calculate stack effect 
            // by separately calculating the stack effect of the pop and push behaviors.
            // the typically return 0 for the oposite behavior.
            var popEffect = GetStackEffect(opcode.StackBehaviourPop);
            var pushEffect = GetStackEffect(opcode.StackBehaviourPush);
            return (popEffect.PopCount + pushEffect.PopCount, popEffect.PushCount + pushEffect.PushCount);
        }
    }

    extension(StackBehaviour stackBehaviour)
    {
        public (int? PopCount, int? PushCount) GetStackEffect()
        {
            return stackBehaviour switch
            {
                StackBehaviour.Pop0 => (0, 0),
                StackBehaviour.Pop1 => (1, 0),
                StackBehaviour.Pop1_pop1 => (2, 0),
                StackBehaviour.Popi_pop1 => (2, 0),
                StackBehaviour.Popi_popi => (2, 0),
                StackBehaviour.Popi_popi8 => (2, 0),
                StackBehaviour.Popi_popi_popi => (3, 0),

                StackBehaviour.Popi => (1, 0),
                StackBehaviour.Popi_popr4 => (1, 0),
                StackBehaviour.Popi_popr8 => (1, 0),
                StackBehaviour.Popref => (1, 0),
                StackBehaviour.Popref_pop1 => (1, 0),
                StackBehaviour.Popref_popi => (1, 0),
                StackBehaviour.Popref_popi_popi => (1, 0),
                StackBehaviour.Popref_popi_popi8 => (1, 0),
                StackBehaviour.Popref_popi_popr4 => (1, 0),
                StackBehaviour.Popref_popi_popr8 => (1, 0),
                StackBehaviour.Popref_popi_popref => (1, 0),
                StackBehaviour.Popref_popi_pop1 => (3, 0),

                StackBehaviour.Push0 => (0, 0),
                StackBehaviour.Push1 => (0, 1),
                StackBehaviour.Push1_push1 => (0, 2),
                StackBehaviour.Pushi => (0, 1),
                StackBehaviour.Pushi8 => (0, 1),
                StackBehaviour.Pushr4 => (0, 1),
                StackBehaviour.Pushr8 => (0, 1),
                StackBehaviour.Pushref => (0, 1),

                StackBehaviour.Varpop => (null, 0),
                StackBehaviour.Varpush => (0, null),

                _ => throw new InvalidCastException($"unknown StackBehaviour {stackBehaviour}")
            };
        }
    }
}