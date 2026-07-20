using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using Durchblick.ControlFlow;
using Durchblick.IL;



public record DecompileResult(Dictionary<int, Expression> Locals, IReadOnlyList<Expression> Stack)
{
    public static implicit operator DecompileResult((Dictionary<int, Expression> Locals, IReadOnlyList<Expression> Stack) tuple) => new DecompileResult(tuple.Locals, tuple.Stack);
}

public static class LinqDecompiler
{


    public static DecompileResult DecompileBlock(ControlFlowGraph graph, MethodInfo methodInfo, BasicBlock block)
    {
        var stack = new Stack<Expression>();
        var arguments = CreateArgumentExpressions(methodInfo);
        var locals = CreateLocalsAsExpression(methodInfo);

        for (var instructionIndex = block.StartIndex; instructionIndex <= block.EndIndex; instructionIndex++)
        {
            var instruction = graph.Instructions[instructionIndex];
            switch (instruction.ILOpCode)
            {
                case ILOpCode.Nop:
                    break;

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
                case ILOpCode.Ldarg:
                case ILOpCode.Ldarg_s:
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
                case ILOpCode.Ldloc:
                case ILOpCode.Ldloc_s:
                    stack.Push(locals[instruction.Operand.GetVariableIndex()]);
                    break;

                case ILOpCode.Stloc_0:
                case ILOpCode.Stloc_1:
                case ILOpCode.Stloc_2:
                case ILOpCode.Stloc_3:
                    var ix = instruction.ILOpCode - ILOpCode.Stloc_0;
                    locals[ix] = stack.Pop();
                    break;
                case ILOpCode.Stloc:
                case ILOpCode.Stloc_s:
                    locals[instruction.Operand.GetVariableIndex()] = stack.Pop();
                    break;

                case ILOpCode.Ldc_i4_0:
                case ILOpCode.Ldc_i4_1:
                case ILOpCode.Ldc_i4_2:
                case ILOpCode.Ldc_i4_3:
                case ILOpCode.Ldc_i4_4:
                case ILOpCode.Ldc_i4_5:
                case ILOpCode.Ldc_i4_6:
                case ILOpCode.Ldc_i4_7:
                case ILOpCode.Ldc_i4_8:
                    var constant = (int)instruction.ILOpCode - (int)ILOpCode.Ldc_i4_0;
                    stack.Push(Expression.Constant(constant));
                    break;
                case ILOpCode.Ldc_i4_m1:
                    stack.Push(Expression.Constant(-1));
                    break;
                case ILOpCode.Ldc_i4:
                case ILOpCode.Ldc_i4_s:
                    stack.Push(Expression.Constant(instruction.Operand.GetInt32()));
                    break;
                case ILOpCode.Ldc_i8:
                    stack.Push(Expression.Constant(instruction.Operand.GetInt64()));
                    break;
                case ILOpCode.Ldc_r4:
                    stack.Push(Expression.Constant(instruction.Operand.GetFloat32(), typeof(float)));
                    break;
                case ILOpCode.Ldc_r8:
                    stack.Push(Expression.Constant(instruction.Operand.GetFloat64(), typeof(double)));
                    break;
                case ILOpCode.Ldnull:
                    stack.Push(Expression.Constant(null!, typeof(object)));
                    break;

                case ILOpCode.Add:
                case ILOpCode.Sub:
                case ILOpCode.Mul:
                case ILOpCode.Div:
                case ILOpCode.Ceq:
                case ILOpCode.Cgt:
                case ILOpCode.Clt:
                    var op = BinaryOperators[instruction.ILOpCode];
                    var right = stack.Pop();
                    var left = stack.Pop();
                    stack.Push(Expression.MakeBinary(op, left, right));
                    break;

                case var _ when instruction.OpCode.FlowControl is System.Reflection.Emit.FlowControl.Branch:
                case var _ when instruction.OpCode.FlowControl is System.Reflection.Emit.FlowControl.Cond_Branch:
                case ILOpCode.Ret:
                    for (var i = 0; i < instruction.OpCode.StackBehaviourPop.NumberOfPops(); i++)
                    {
                        stack.Pop();
                    }


                    goto Done;


                default:
                    throw Unsupported(instruction);
            }
        }
    Done:

        var localValues = new Dictionary<int, Expression>();
        foreach (var (i, local) in locals.Select((l, i) => (i, l)))
        {
            if (local is not ParameterExpression)
            {
                localValues[i] = local;
            }
            continue;
        }

        return (localValues, stack.AsEnumerable().ToList());
    }

    private static readonly Dictionary<ILOpCode, ExpressionType> BinaryOperators = new()
    {
        [ILOpCode.Add] = ExpressionType.Add,
        [ILOpCode.Sub] = ExpressionType.Subtract,
        [ILOpCode.Mul] = ExpressionType.Multiply,
        [ILOpCode.Div] = ExpressionType.Divide,
        [ILOpCode.Ceq] = ExpressionType.Equal,
        [ILOpCode.Cgt] = ExpressionType.GreaterThan,
        [ILOpCode.Clt] = ExpressionType.LessThan,
    };

    private static Expression[] CreateArgumentExpressions(MethodInfo methodInfo)
    {
        var methodParameters = methodInfo.GetParameters();
        var arguments = new List<Expression>(); // IL calls it arg

        if (!methodInfo.IsStatic)
        {
            arguments.Add(Expression.Variable(methodInfo.DeclaringType!, "this"));
        }

        arguments.AddRange(methodParameters.Select(parameter =>
        {
            var name = parameter.Name ?? $"arg{parameter.Position}";
            return Expression.Variable(parameter.ParameterType, name);
        }));

        return [.. arguments];
    }

    private static Expression[] CreateLocalsAsExpression(MethodInfo methodInfo)
    {
        var body = methodInfo.GetMethodBody();
        if (body is null)
        {
            return [];
        }

        return [.. body.LocalVariables.Select(local =>
        {
            return Expression.Variable(local.LocalType, $"loc{local.LocalIndex}");
        })];
    }

    private static NotSupportedException Unsupported(Instruction instruction) =>
        new($"Unsupported IL opcode for expression decompilation at IL_{instruction.Offset:X4}: {instruction.OpCode}.");
}
