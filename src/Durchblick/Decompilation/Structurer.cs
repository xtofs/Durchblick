namespace Durchblick.Decompilation;

using System.Reflection;
using System.Reflection.Metadata;
using Durchblick.Collections;
using Durchblick.ControlFlow;
using Durchblick.CSharp.Syntax;
using Durchblick.IL;

/// <summary>
/// Reconstructs a structured method body from a <see cref="ControlFlowGraph"/>: recovers
/// <c>if</c>/<c>else</c> from two-way branches (using the post-dominator as the join), <c>while</c>
/// from natural loops, and sequences from fall-through, emitting <see cref="Statement"/> nodes.
/// </summary>
/// <remarks>
/// Simplifying assumptions (throw <see cref="NotSupportedException"/> when violated): the IL stack
/// is empty at block boundaries except the single value feeding the terminator (branch condition or
/// return value); stores are materialized as assignments rather than inlined; loop headers are pure
/// two-way tests. Exception regions and compiler-lowering patterns are out of scope.
/// </remarks>
internal sealed class Structurer
{
    private enum ExitKind { FallThrough, Return, Branch, ConditionalBranch, Switch }

    private sealed record BlockSim(List<Statement> Statements, ExitKind Exit, Expression? Value, bool BranchOnTrue);

    /// <summary>Sentinel "stop" region boundary meaning "run to the end of the method".</summary>
    private const int NoStop = -1;

    private readonly ControlFlowGraph _graph;
    private readonly DominatorTree _dominators;
    private readonly PostDominatorTree _postDominators;
    private readonly IReadOnlyDictionary<int, NaturalLoop> _loopsByHeader;
    private readonly Expression[] _arguments;
    private readonly LocalSlot[] _locals;

    private Structurer(ControlFlowGraph graph, MethodInfo method)
    {
        _graph = graph;
        _dominators = DominatorTree.Build(graph);
        _postDominators = PostDominatorTree.Build(graph);
        _loopsByHeader = LoopAnalysis.Find(graph, _dominators).ToDictionary(loop => loop.Header);
        _arguments = Decompiler.CreateArgumentExpressions(method);
        _locals = Decompiler.CreateLocalSlots(method);
    }

    public static BlockStatement Structure(ControlFlowGraph graph, MethodInfo method)
    {
        if (graph.Blocks.Count == 0)
        {
            return Statement.Block([]);
        }

        var structurer = new Structurer(graph, method);
        var body = Statement.Block(structurer.StructureRegion(start: 0, stop: NoStop));
        return LocalDeclarations.Insert(body, structurer._locals);
    }

    /// <summary>Emits the statements for the region entered at <paramref name="start"/>, stopping when control reaches <paramref name="stop"/> (exclusive).</summary>
    private List<Statement> StructureRegion(int start, int stop)
    {
        var statements = new List<Statement>();
        var current = start;

        while (current != stop && current >= 0 && current < _graph.Blocks.Count)
        {
            if (_loopsByHeader.TryGetValue(current, out var loop))
            {
                current = EmitLoop(current, loop, statements);
                continue;
            }

            var block = _graph.Blocks[current];
            var sim = SimulateBlock(block);
            statements.AddRange(sim.Statements);

            switch (sim.Exit)
            {
                case ExitKind.Return:
                    statements.Add(sim.Value is null ? new ReturnStatement(null!) : Statement.Return(sim.Value));
                    return statements;

                case ExitKind.Branch:
                    current = block.Successors[0];
                    break;

                case ExitKind.FallThrough:
                    current = block.Successors.Count > 0 ? block.Successors[0] : stop;
                    break;

                case ExitKind.ConditionalBranch:
                    current = EmitIf(current, block, sim, statements, stop);
                    break;

                case ExitKind.Switch:
                    current = EmitSwitch(current, block, sim, statements, stop);
                    break;
            }
        }

        return statements;
    }

    private int EmitIf(int index, BasicBlock block, BlockSim sim, List<Statement> statements, int stop)
    {
        var join = _postDominators.ImmediatePostDominator(index);
        var joinStop = join == _postDominators.VirtualExit ? stop : join;

        var target = block.Successors[0];      // branch taken
        var fallThrough = block.Successors[1]; // branch not taken
        var (thenStart, elseStart) = sim.BranchOnTrue ? (target, fallThrough) : (fallThrough, target);

        var thenBody = StructureRegion(thenStart, joinStop);
        var elseBody = StructureRegion(elseStart, joinStop);

        Statement? elseStatement = elseBody.Count > 0 ? Statement.Block(elseBody) : null;
        statements.Add(Statement.If(sim.Value!, Statement.Block(thenBody), elseStatement));

        return joinStop;
    }

    private int EmitSwitch(int index, BasicBlock block, BlockSim sim, List<Statement> statements, int stop)
    {
        var join = _postDominators.ImmediatePostDominator(index);
        var joinStop = join == _postDominators.VirtualExit ? stop : join;

        // The CFG orders switch successors as [case 0, case 1, …, default/fall-through].
        var caseCount = block.Successors.Count - 1;
        var defaultTarget = block.Successors[caseCount];

        var cases = new List<SwitchCase>();
        for (var value = 0; value < caseCount; value++)
        {
            var pattern = Pattern.Constant(Expression.Literal(value, BuiltInTypeReferences.Int));
            cases.Add(new SwitchCase(pattern, CaseBody(block.Successors[value], joinStop)));
        }

        // A default that is not simply the join contributes its own case.
        if (defaultTarget != joinStop)
        {
            cases.Add(new SwitchCase(Pattern.Discard(), CaseBody(defaultTarget, joinStop)));
        }

        statements.Add(Statement.Switch(sim.Value!, cases));
        return joinStop;
    }

    /// <summary>Structures a switch case body and terminates it with <c>break</c> when it falls out to the join.</summary>
    private ImmutableCollection<Statement> CaseBody(int start, int joinStop)
    {
        var body = StructureRegion(start, joinStop);
        if (body.Count == 0 || body[^1] is not (ReturnStatement or ThrowStatement or BreakStatement or ContinueStatement))
        {
            body.Add(Statement.Break());
        }

        return [.. body];
    }

    private int EmitLoop(int headerIndex, NaturalLoop loop, List<Statement> statements)
    {
        var header = _graph.Blocks[headerIndex];
        var sim = SimulateBlock(header);
        if (sim.Exit != ExitKind.ConditionalBranch)
        {
            throw new NotSupportedException($"Only while-style loops with a two-way test header are supported (block {headerIndex}).");
        }

        var target = header.Successors[0];      // branch taken
        var fallThrough = header.Successors[1]; // branch not taken
        var inLoop = loop.Body.Contains(target) ? target : fallThrough;
        var follow = loop.Body.Contains(target) ? fallThrough : target;

        // Normalize so the condition is true exactly when control stays in the loop.
        var takenWhenTrue = sim.BranchOnTrue ? target : fallThrough;
        var stayCondition = inLoop == takenWhenTrue ? sim.Value! : Expression.Unary(UnaryOperator.Not, sim.Value!);

        var body = StructureRegion(inLoop, headerIndex);

        if (sim.Statements.Count == 0)
        {
            // Pure test header → `while (cond) { body }`.
            statements.Add(Statement.While(stayCondition, Statement.Block(body)));
        }
        else
        {
            // The header carries the test (e.g. a debug-build comparison temp), re-evaluated each
            // iteration → `while (true) { test; if (!cond) break; body }`.
            var loopBody = new List<Statement>(sim.Statements)
            {
                Statement.If(Expression.Unary(UnaryOperator.Not, stayCondition), Statement.Block([Statement.Break()]), null),
            };
            loopBody.AddRange(body);
            statements.Add(Statement.While(Expression.Literal(true, BuiltInTypeReferences.Bool), Statement.Block(loopBody)));
        }

        return follow;
    }

    /// <summary>Simulates a block's straight-line body into statements plus a terminator description.</summary>
    private BlockSim SimulateBlock(BasicBlock block)
    {
        var stack = new Stack<Expression>();
        var statements = new List<Statement>();

        for (var i = block.StartIndex; i <= block.EndIndex; i++)
        {
            var instruction = _graph.Instructions[i];
            switch (instruction.ILOpCode)
            {
                case ILOpCode.Nop:
                    break;

                case ILOpCode.Ldarg_0:
                    stack.Push(_arguments[0]);
                    break;
                case ILOpCode.Ldarg_1:
                    stack.Push(_arguments[1]);
                    break;
                case ILOpCode.Ldarg_2:
                    stack.Push(_arguments[2]);
                    break;
                case ILOpCode.Ldarg_3:
                    stack.Push(_arguments[3]);
                    break;
                case ILOpCode.Ldarg:
                case ILOpCode.Ldarg_s:
                    stack.Push(_arguments[instruction.Operand.GetVariableIndex()]);
                    break;

                case ILOpCode.Ldloc_0:
                    stack.Push(_locals[0].Reference);
                    break;
                case ILOpCode.Ldloc_1:
                    stack.Push(_locals[1].Reference);
                    break;
                case ILOpCode.Ldloc_2:
                    stack.Push(_locals[2].Reference);
                    break;
                case ILOpCode.Ldloc_3:
                    stack.Push(_locals[3].Reference);
                    break;
                case ILOpCode.Ldloc:
                case ILOpCode.Ldloc_s:
                    stack.Push(_locals[instruction.Operand.GetVariableIndex()].Reference);
                    break;

                case ILOpCode.Stloc_0:
                    statements.Add(Statement.Expr(Expression.Assign(_locals[0].Reference, stack.Pop())));
                    break;
                case ILOpCode.Stloc_1:
                    statements.Add(Statement.Expr(Expression.Assign(_locals[1].Reference, stack.Pop())));
                    break;
                case ILOpCode.Stloc_2:
                    statements.Add(Statement.Expr(Expression.Assign(_locals[2].Reference, stack.Pop())));
                    break;
                case ILOpCode.Stloc_3:
                    statements.Add(Statement.Expr(Expression.Assign(_locals[3].Reference, stack.Pop())));
                    break;
                case ILOpCode.Stloc:
                case ILOpCode.Stloc_s:
                    statements.Add(Statement.Expr(Expression.Assign(_locals[instruction.Operand.GetVariableIndex()].Reference, stack.Pop())));
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

                case ILOpCode.Ldnull:
                    stack.Push(Expression.Literal(null!, BuiltInTypeReferences.Object));
                    break;

                case ILOpCode.Ldstr:
                    stack.Push(Expression.Literal(instruction.Operand.GetString(), BuiltInTypeReferences.String));
                    break;

                case ILOpCode.Ldfld:
                    var field = instruction.Operand.GetField();
                    stack.Push(Expression.Member(stack.Pop(), field.Name, new SymbolReference(field.Name, SymbolKind.Field)));
                    break;


                case ILOpCode.Add:
                case ILOpCode.Sub:
                case ILOpCode.Mul:
                case ILOpCode.Div:
                case ILOpCode.Ceq:
                case ILOpCode.Cgt:
                case ILOpCode.Clt:
                    var right = stack.Pop();
                    var left = stack.Pop();
                    stack.Push(Expression.Binary(Decompiler.BinaryOperators[instruction.ILOpCode], left, right));
                    break;

                case ILOpCode.Call:
                case ILOpCode.Callvirt:
                    PushCallExpression(stack, instruction.Operand.GetMethod());
                    break;

                // branching instructions

                case ILOpCode.Br:
                case ILOpCode.Br_s:
                    return new BlockSim(statements, ExitKind.Branch, null, false);

                case ILOpCode.Brtrue:
                case ILOpCode.Brtrue_s:
                    return new BlockSim(statements, ExitKind.ConditionalBranch, stack.Pop(), BranchOnTrue: true);

                case ILOpCode.Brfalse:
                case ILOpCode.Brfalse_s:
                    return new BlockSim(statements, ExitKind.ConditionalBranch, stack.Pop(), BranchOnTrue: false);

                case ILOpCode.Beq:
                case ILOpCode.Beq_s:
                    var equalityRight = stack.Pop();
                    var equalityLeft = stack.Pop();
                    return new BlockSim(
                        statements,
                        ExitKind.ConditionalBranch,
                        Expression.Binary(BinaryOperator.Equals, equalityLeft, equalityRight),
                        BranchOnTrue: true);

                case ILOpCode.Switch:
                    return new BlockSim(statements, ExitKind.Switch, stack.Pop(), false);

                case ILOpCode.Ret:
                    return new BlockSim(statements, ExitKind.Return, stack.Count > 0 ? stack.Pop() : null, false);

                default:
                    throw new NotSupportedException($"Unsupported IL opcode for body reconstruction at IL_{instruction.Offset:X4}: {instruction.OpCode}.");
            }
        }

        return new BlockSim(statements, ExitKind.FallThrough, null, false);
    }

    private static void PushCallExpression(Stack<Expression> stack, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var arguments = new Expression[parameters.Length];
        for (var parameterIndex = parameters.Length - 1; parameterIndex >= 0; parameterIndex--)
        {
            arguments[parameterIndex] = stack.Pop();
        }

        if (arguments.Length == 0 && TryGetGetterProperty(method, out var property))
        {
            var propertySymbol = new SymbolReference(property.Name, SymbolKind.Property);
            stack.Push(method.IsStatic
                ? Expression.Identifier(property.Name, propertySymbol)
                : Expression.Member(stack.Pop(), property.Name, propertySymbol));
            return;
        }

        var symbol = new SymbolReference(method.Name, SymbolKind.Method);
        Expression target = method.IsStatic
            ? Expression.Identifier(method.Name, symbol)
            : Expression.Member(stack.Pop(), method.Name, symbol);
        stack.Push(Expression.Call(target, symbol, arguments));
    }

    private static bool TryGetGetterProperty(MethodInfo method, out PropertyInfo property)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        property = method.DeclaringType?
            .GetProperties(flags)
            .SingleOrDefault(candidate => candidate.GetMethod == method)!;
        return property is not null;
    }
}
