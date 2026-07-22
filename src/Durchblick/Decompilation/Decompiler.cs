namespace Durchblick.Decompilation;

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using Durchblick.CSharp.Syntax;
using Durchblick.ControlFlow;
using Durchblick.IL;

public static class Decompiler
{
    public static Expression? DecompileExpression(MethodInfo methodInfo)
    {
        var graph = BasicBlockBuilder.Build(methodInfo);
        return DecompileExpression(graph, methodInfo);
    }

    /// <summary>
    /// Reconstructs a structured method body (statements, with <c>if</c>/loops recovered from the
    /// CFG) as a <see cref="BlockStatement"/>.
    /// </summary>
    public static BlockStatement DecompileBody(MethodInfo methodInfo)
    {
        var graph = BasicBlockBuilder.Build(methodInfo);
        return Structurer.Structure(graph, methodInfo);
    }

    public static Expression? DecompileExpression(ControlFlowGraph graph, MethodInfo methodInfo)
    {
        var stack = new Stack<Expression>();
        var arguments = CreateArgumentExpressions(methodInfo);
        var locals = CreateLocalExpressions(methodInfo);
        var visitedBlocks = new HashSet<int>();
        var blockIndex = 0;

        while (true)
        {
            if (!visitedBlocks.Add(blockIndex))
            {
                throw new NotSupportedException("Loops are not supported by expression decompilation yet.");
            }

            var block = graph.Blocks[blockIndex];
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
                        locals[0] = stack.Pop();
                        break;
                    case ILOpCode.Stloc_1:
                        locals[1] = stack.Pop();
                        break;
                    case ILOpCode.Stloc_2:
                        locals[2] = stack.Pop();
                        break;
                    case ILOpCode.Stloc_3:
                        locals[3] = stack.Pop();
                        break;
                    case ILOpCode.Stloc:
                    case ILOpCode.Stloc_s:
                        locals[instruction.Operand.GetVariableIndex()] = stack.Pop();
                        break;

                    case ILOpCode.Ldc_i4_m1:
                        stack.Push(Expression.Literal(-1, BuiltInTypeReferences.Int));
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
                        stack.Push(Expression.Literal((int)instruction.ILOpCode - (int)ILOpCode.Ldc_i4_0, BuiltInTypeReferences.Int));
                        break;
                    case ILOpCode.Ldc_i4:
                    case ILOpCode.Ldc_i4_s:
                        stack.Push(Expression.Literal(instruction.Operand.GetInt32(), BuiltInTypeReferences.Int));
                        break;
                    case ILOpCode.Ldc_i8:
                        stack.Push(Expression.Literal(instruction.Operand.GetInt64(), BuiltInTypeReferences.Long));
                        break;
                    case ILOpCode.Ldc_r4:
                        stack.Push(Expression.Literal(instruction.Operand.GetFloat32(), BuiltInTypeReferences.Float));
                        break;
                    case ILOpCode.Ldc_r8:
                        stack.Push(Expression.Literal(instruction.Operand.GetFloat64(), BuiltInTypeReferences.Double));
                        break;
                    case ILOpCode.Ldnull:
                        stack.Push(Expression.Literal(null!, BuiltInTypeReferences.Object));
                        break;

                    case ILOpCode.Add:
                    case ILOpCode.Sub:
                    case ILOpCode.Mul:
                    case ILOpCode.Div:
                    case ILOpCode.Ceq:
                    case ILOpCode.Cgt:
                    case ILOpCode.Clt:
                        PushBinaryExpression(stack, BinaryOperators[instruction.ILOpCode]);
                        break;

                    case ILOpCode.Br:
                    case ILOpCode.Br_s:
                        blockIndex = SingleSuccessor(block, instruction);
                        goto NextBlock;

                    case ILOpCode.Ret:
                        return stack.Count == 0 ? null : stack.Pop();

                    default:
                        throw Unsupported(instruction);
                }
            }

            if (block.Successors.Count != 1)
            {
                throw new NotSupportedException($"Expression decompilation requires straight-line control flow at block {blockIndex}.");
            }

            blockIndex = block.Successors[0];

        NextBlock:
            continue;
        }
    }

    internal static readonly Dictionary<ILOpCode, BinaryOperator> BinaryOperators = new()
    {
        [ILOpCode.Add] = BinaryOperator.Add,
        [ILOpCode.Sub] = BinaryOperator.Subtract,
        [ILOpCode.Mul] = BinaryOperator.Multiply,
        [ILOpCode.Div] = BinaryOperator.Divide,
        [ILOpCode.Ceq] = BinaryOperator.Equals,
        [ILOpCode.Cgt] = BinaryOperator.Greater,
        [ILOpCode.Clt] = BinaryOperator.Less,
    };

    internal static Expression[] CreateArgumentExpressions(MethodInfo methodInfo)
    {
        var parameters = methodInfo.GetParameters();
        var arguments = new List<Expression>();

        if (!methodInfo.IsStatic)
        {
            arguments.Add(Expression.Identifier("this", new SymbolReference("this", SymbolKind.Parameter)));
        }

        arguments.AddRange(parameters.Select(parameter =>
        {
            var name = parameter.Name ?? $"arg{parameter.Position}";
            return Expression.Identifier(name, new SymbolReference(name, SymbolKind.Parameter));
        }));

        return [.. arguments];
    }

    internal static Expression[] CreateLocalExpressions(MethodInfo methodInfo)
        => [.. CreateLocalSlots(methodInfo).Select(slot => slot.Reference)];

    /// <summary>Reads the method's local variable slots, keeping the declared type for later declaration insertion.</summary>
    internal static LocalSlot[] CreateLocalSlots(MethodInfo methodInfo)
    {
        var body = methodInfo.GetMethodBody();
        if (body is null)
        {
            return [];
        }

        return [.. body.LocalVariables.Select(local =>
        {
            var name = $"local{local.LocalIndex}";
            var reference = Expression.Identifier(name, new SymbolReference(name, SymbolKind.Local));
            return new LocalSlot(name, Declaration.TypeRef(local.LocalType), reference);
        })];
    }

    private static void PushBinaryExpression(Stack<Expression> stack, BinaryOperator op)
    {
        var right = stack.Pop();
        var left = stack.Pop();
        stack.Push(Expression.Binary(op, left, right));
    }

    private static int SingleSuccessor(BasicBlock block, Instruction instruction) =>
        block.Successors.Count == 1
            ? block.Successors[0]
            : throw new NotSupportedException($"Branch at IL_{instruction.Offset:X4} does not have exactly one successor in the control flow graph.");

    private static NotSupportedException Unsupported(Instruction instruction) =>
        new($"Unsupported IL opcode for expression decompilation at IL_{instruction.Offset:X4}: {instruction.OpCode}.");
}