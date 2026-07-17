namespace Durchblick.Decompilation;

using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Durchblick.ControlFlow;
using Durchblick.IL;

public static class Decompiler
{

    public static void RunBlockLeaders(Expression[] locals, Expression[] arguments, Stack<Expression> stack, BasicBlock block)
    {

        //   - Stores: `starg`, `stloc.2/3`, `stloc.s`, `stloc`.
        //   - Arithmetic/logic: `sub` (see A7), `rem`, `neg`, `and`, `or`, `xor`, `shl`, `shr`, `not`, `.ovf`.
        //   - Comparisons: `ceq`, `clt` (see A7), `cgt.un`, `clt.un`.


        foreach (var instruction in block.Instructions)
        {
            var opcode = instruction.OpCode;
            var ilOpCode = opcode.ILOpCode;

            // The block's terminator (branch, conditional branch, return, or throw) is decoded by
            // the caller (ToExpression); the body simulator stops as soon as it reaches it.
            if (Terminators.Contains(opcode.FlowControl))
            {
                return;
            }

            // switch on ILOpCode which is a single enum instead of the structured OpCode type
            switch (ilOpCode)
            {
                case ILOpCode.Nop:
                    break;

                #region loads

                case ILOpCode.Ldarg_0:
                    stack.Push(arguments[0]);
                    break;
                case ILOpCode.Ldarg_1:
                    stack.Push(arguments[1]);
                    break;
                case ILOpCode.Ldarg_2:
                    stack.Push(arguments[2]);
                    break;
                case ILOpCode.Ldarg_3:
                    stack.Push(arguments[3]);
                    break;
                case ILOpCode.Ldarg_s:
                    stack.Push(arguments[instruction.Operand.GetVariableIndex()]);
                    break;

                case ILOpCode.Ldarg:
                    stack.Push(arguments[instruction.Operand.GetVariableIndex()]);
                    break;

                case ILOpCode.Ldloc_0:
                    stack.Push(locals[0]);
                    break;
                case ILOpCode.Ldloc_1:
                    stack.Push(locals[1]);
                    break;
                case ILOpCode.Ldloc_2:
                    stack.Push(locals[2]);
                    break;
                case ILOpCode.Ldloc_3:
                    stack.Push(locals[3]);
                    break;
                case ILOpCode.Ldloc_s:
                case ILOpCode.Ldloc:
                    stack.Push(locals[instruction.Operand.GetVariableIndex()]);
                    break;

                case ILOpCode.Ldc_i4_0:
                    stack.Push(Expression.Constant(0));
                    break;
                case ILOpCode.Ldc_i4_1:
                    stack.Push(Expression.Constant(1));
                    break;
                case ILOpCode.Ldc_i4_2:
                    stack.Push(Expression.Constant(2));
                    break;
                case ILOpCode.Ldc_i4_3:
                    stack.Push(Expression.Constant(3));
                    break;
                case ILOpCode.Ldc_i4_4:
                    stack.Push(Expression.Constant(4));
                    break;
                case ILOpCode.Ldc_i4_5:
                    stack.Push(Expression.Constant(5));
                    break;
                case ILOpCode.Ldc_i4_6:
                    stack.Push(Expression.Constant(6));
                    break;
                case ILOpCode.Ldc_i4_7:
                    stack.Push(Expression.Constant(7));
                    break;
                case ILOpCode.Ldc_i4_8:
                    stack.Push(Expression.Constant(8));
                    break;
                case ILOpCode.Ldc_i4:
                case ILOpCode.Ldc_i4_s:
                    stack.Push(Expression.Constant(instruction.Operand.GetInt32()));
                    break;
                case ILOpCode.Ldc_i8:
                    stack.Push(Expression.Constant(instruction.Operand.GetInt64()));
                    break;
                case ILOpCode.Ldc_i4_m1:
                    stack.Push(Expression.Constant(-1));
                    break;
                case ILOpCode.Ldc_r4:
                    stack.Push(Expression.Constant(Expression.Constant(instruction.Operand.GetFloat32())));
                    break;
                case ILOpCode.Ldc_r8:
                    stack.Push(Expression.Constant(Expression.Constant(instruction.Operand.GetFloat64())));
                    break;
                case ILOpCode.Ldstr:
                    // var token = instruction.Operand.GetMetaDataToken();
                    // var reader = peReader.GetMetadataReader();
                    // var handle = (UserStringHandle)MetadataTokens.Handle(token);
                    // string value = reader.GetUserString(handle);
                    // stack.Push(Expression.Constant(value));
                    break;
                case ILOpCode.Ldnull:
                    stack.Push(Expression.Constant(null));
                    break;
                //  `ldarga`/`ldloca`.
                #endregion

                case ILOpCode.Stloc_0:
                    locals[0] = stack.Pop();
                    break;
                case ILOpCode.Stloc_1:
                    locals[1] = stack.Pop();
                    break;

                case ILOpCode.Add:
                case ILOpCode.Mul:
                case ILOpCode.Div:
                case ILOpCode.Cgt:
                    var op = BinaryOperators[ilOpCode];
                    var right = stack.Pop();
                    var left = stack.Pop();
                    stack.Push(op(left, right));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported IL opcode: {instruction.OpCode.ILOpCode}");
            }
        }
    }

    static readonly Dictionary<ILOpCode, Func<Expression, Expression, Expression>> BinaryOperators = new()
    {
        { ILOpCode.Add, Expression.Add },
        { ILOpCode.Mul, Expression.Multiply },
        { ILOpCode.Div, Expression.Divide },
        { ILOpCode.Sub, Expression.Subtract },

        { ILOpCode.Ceq, Expression.Equal },
        { ILOpCode.Cgt, Expression.GreaterThan },
        { ILOpCode.Clt, Expression.LessThan },
    };

    // Flow-control kinds that end a basic block; RunBlockLeaders stops the body simulation here
    // and lets ToExpression decode the terminator.
    private static readonly HashSet<FlowControl> Terminators =
        [FlowControl.Branch, FlowControl.Cond_Branch, FlowControl.Return, FlowControl.Throw];

    public static void GetParametersAndLocals(MethodInfo methodInfo, out ParameterExpression[] parameters, out Expression[] locals)
    {
        parameters = [.. methodInfo.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name))];
        if (!methodInfo.IsStatic)
        {
            var @this = Expression.Parameter(methodInfo.DeclaringType!, "this");
            parameters = [@this, .. parameters];
        }

        var mb = methodInfo.GetMethodBody()!;
        locals = [.. mb.LocalVariables.Select(v => Expression.Variable(v.LocalType, v.ToString()))];
    }


    public static Expression? ToExpression(Dictionary<int, BasicBlock> blocks, Expression[] parameters, Expression[] locals)
    {
        return Eval(blocks[0], new Stack<Expression>());

        Expression? Eval(BasicBlock block, Stack<Expression> stack)
        {
            RunBlockLeaders(locals, parameters, stack, block);

            var exit = block.Exit;
            switch (exit.OpCode.ILOpCode)
            {
                case ILOpCode.Ret:
                    return stack.Count > 0 ? stack.Pop() : null;

                case ILOpCode.Br:
                case ILOpCode.Br_s:
                    return Eval(blocks[exit.Operand.GetBranchTarget()], stack);

                // brfalse jumps to its target when the value is zero/false and falls through
                // otherwise; brtrue is the mirror image. Both reconstruct to a ternary.
                case ILOpCode.Brfalse:
                case ILOpCode.Brfalse_s:
                    return Conditional(stack, whenTrue: FallThrough(block), whenFalse: exit.Operand.GetBranchTarget());

                case ILOpCode.Brtrue:
                case ILOpCode.Brtrue_s:
                    return Conditional(stack, whenTrue: exit.Operand.GetBranchTarget(), whenFalse: FallThrough(block));

                default:
                    throw new NotSupportedException($"Unsupported terminator opcode: {exit.OpCode.ILOpCode}");
            }
        }

        Expression? Conditional(Stack<Expression> stack, int whenTrue, int whenFalse)
        {
            var condition = AsBoolean(stack.Pop());
            var ifTrue = Eval(blocks[whenTrue], Clone(stack));
            var ifFalse = Eval(blocks[whenFalse], Clone(stack));
            return ifTrue is null || ifFalse is null
                ? null
                : Expression.Condition(condition, ifTrue, ifFalse);
        }

        // The fall-through of a conditional branch is the block that begins immediately after it.
        // Blocks tile the IL contiguously, so that is the next block by start offset.
        int FallThrough(BasicBlock block)
        {
            var next = blocks.Keys.Where(offset => offset > block.StartOffset).ToList();
            return next.Count > 0
                ? next.Min()
                : throw new InvalidOperationException($"No fall-through block after IL_{block.StartOffset:X4}.");
        }

        static Expression AsBoolean(Expression condition) =>
            condition.Type == typeof(bool) ? condition : Expression.NotEqual(condition, Expression.Constant(0));

        static Stack<Expression> Clone(Stack<Expression> stack) => new(stack.Reverse());
    }
}