namespace Durchblick;

using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

public static class Decompiler
{

    public static void RunBlockLeaders(Expression[] locals, Expression[] arguments, Stack<Expression> stack, BasicBlock block)
    {

        foreach (var instruction in block.Instructions)
        {
            var opcode = instruction.OpCode;
            // switch on ILOpCode which is a single enum instead of the structured OpCode type
            switch (opcode.ILOpCode)
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

                case ILOpCode.Ldloc_0:
                    stack.Push(locals[0]);
                    break;
                case ILOpCode.Ldloc_1:
                    stack.Push(locals[1]);
                    break;

                case ILOpCode.Ldc_i4_2:
                    stack.Push(Expression.Constant(2));
                    break;

                case ILOpCode.Stloc_0:
                    locals[0] = stack.Pop();
                    break;
                case ILOpCode.Stloc_1:
                    locals[1] = stack.Pop();
                    break;


                case ILOpCode.Add:
                    var right = stack.Pop();
                    var left = stack.Pop();
                    stack.Push(Expression.Add(left, right));
                    break;
                case ILOpCode.Mul:
                    right = stack.Pop();
                    left = stack.Pop();
                    stack.Push(Expression.Multiply(left, right));
                    break;

                case ILOpCode.Br_s:
                case ILOpCode.Ret:
                    return;
                default:
                    throw new NotSupportedException($"Unsupported IL opcode: {instruction.OpCode.ILOpCode}");
            }
        }
    }

    public static void GetParametersAndLocals(MethodInfo methodInfo, out ParameterExpression[] parameters, out Expression[] locals)
    {
        var @this = Expression.Parameter(methodInfo.DeclaringType!, "this");
        parameters = [@this, .. methodInfo.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name))];

        var mb = methodInfo.GetMethodBody()!;
        locals = [.. mb.LocalVariables.Select(v => Expression.Variable(v.LocalType, v.ToString()))];
    }


    public static Expression? ToExpression( Dictionary<int, BasicBlock> blocks, Expression[] parameters, Expression[] locals)
    {
        var stack = new Stack<Expression>();
        var block = blocks[0];

        while (block != null)
        {
            RunBlockLeaders(locals, parameters, stack, block);

            var exit = block.Instructions[^1];
            switch (exit.OpCode.ILOpCode)
            {
                case ILOpCode.Ret:
                    return stack.Count == 1 ? stack.Pop() : null;

                case ILOpCode.Br_s:
                    var target = exit.Operand.GetBranchTarget();
                    block = blocks[target];
                    break;
            }
        }
        return null;
    }

    private static void Trace(Level level, string message)
    {
        Console.Error.WriteLine($"{level}: {message}");
    }
}

internal enum Level
{
    Warning,
    Error,
}