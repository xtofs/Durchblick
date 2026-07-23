namespace Durchblick.Decompilation;

using Durchblick.CSharp.Syntax;

/// <summary>A method's IL local slot: its generated name, declared type, CLR type, and the identifier used to reference it.</summary>
internal readonly record struct LocalSlot(string Name, TypeReference Type, Type RuntimeType, IdentifierExpression Reference);

/// <summary>
/// Turns the bare assignments the <see cref="Structurer"/> emits for <c>stloc</c> into proper C#
/// local declarations: each local is declared in the innermost statement list that contains all of
/// its occurrences, merging the first assignment into the declaration when possible.
/// </summary>
/// <remarks>
/// Placement is scope-driven rather than lexical, because "declare at the first assignment" is wrong
/// whenever a local is assigned in both arms of an <c>if</c>, or assigned in a loop and read after it.
/// A local whose occurrences are all inside a single loop body is declared inside that body; if such a
/// local also carries its value across iterations, the declaration is emitted without an initializer,
/// which C# rejects as unassigned — that case needs the value-flow analysis we do not have yet.
/// </remarks>
internal static class LocalDeclarations
{
    public static BlockStatement Insert(BlockStatement body, IReadOnlyList<LocalSlot> locals)
        => locals.Count == 0 ? body : Statement.Block(RewriteList(body.Statements, locals));

    /// <summary>
    /// Rewrites one statement list, declaring the <paramref name="pending"/> locals whose scope ends
    /// here and forwarding the rest to the nested lists that contain them.
    /// </summary>
    private static List<Statement> RewriteList(IEnumerable<Statement> source, IReadOnlyList<LocalSlot> pending)
    {
        var statements = source.ToList();
        var declareHere = new List<LocalSlot>();
        var forward = new List<LocalSlot>();

        foreach (var slot in pending)
        {
            if (statements.Any(statement => MentionsShallow(statement, slot.Name)))
            {
                // Used directly at this level, so this list is the innermost one that can hold it.
                declareHere.Add(slot);
                continue;
            }

            var mentioningLists = statements
                .SelectMany(NestedLists)
                .Count(list => list.Any(statement => Mentions(statement, slot.Name)));

            switch (mentioningLists)
            {
                case 0:
                    break; // not used in this subtree at all
                case 1:
                    forward.Add(slot); // entirely contained in one nested list — declare it down there
                    break;
                default:
                    declareHere.Add(slot); // spans several branches — must be declared before them
                    break;
            }
        }

        var rewritten = statements.Select(statement => RewriteStatement(statement, forward)).ToList();

        // Order by first use so declarations appear in the order the body reads.
        foreach (var slot in declareHere.OrderBy(slot => FirstMention(rewritten, slot.Name)))
        {
            Declare(rewritten, slot);
        }

        return rewritten;
    }

    /// <summary>Introduces <paramref name="slot"/>'s declaration at its first use, merging the assignment into it when that is safe.</summary>
    private static void Declare(List<Statement> statements, LocalSlot slot)
    {
        var index = FirstMention(statements, slot.Name);
        if (index < 0)
        {
            return;
        }

        // `local = value;` becomes `int local = value;` — unless the value reads the local itself.
        if (statements[index] is ExpressionStatement { Expression: AssignExpression assignment }
            && assignment.Target is IdentifierExpression target
            && target.Name == slot.Name
            && !Mentions(assignment.Value, slot.Name))
        {
            statements[index] = Statement.Var(new VariableDecl(slot.Type, slot.Name, assignment.Value));
            return;
        }

        statements.Insert(index, Statement.Var(new VariableDecl(slot.Type, slot.Name, null)));
    }

    private static int FirstMention(IReadOnlyList<Statement> statements, string name)
    {
        for (var index = 0; index < statements.Count; index++)
        {
            if (Mentions(statements[index], name))
            {
                return index;
            }
        }

        return -1;
    }

    private static Statement RewriteStatement(Statement statement, IReadOnlyList<LocalSlot> pending)
    {
        if (pending.Count == 0)
        {
            return statement;
        }

        return statement switch
        {
            BlockStatement block => Statement.Block(RewriteList(block.Statements, pending)),
            IfStatement @if => Statement.If(
                @if.Condition,
                RewriteChild(@if.Then, pending),
                @if.Else is null ? null : RewriteChild(@if.Else, pending)),
            WhileStatement loop => Statement.While(loop.Condition, RewriteChild(loop.Body, pending)),
            ForStatement loop => Statement.For(
                RewriteList(loop.Initializer, pending),
                loop.Condition,
                RewriteList(loop.Iterator, pending),
                RewriteChild(loop.Body, pending)),
            ForEachStatement loop => Statement.ForEach(loop.Variable, loop.Collection, RewriteChild(loop.Body, pending)),
            SwitchStatement @switch => Statement.Switch(
                @switch.Expression,
                @switch.Cases.Select(@case => new SwitchCase(@case.Pattern, [.. RewriteList(@case.Body, pending)]))),
            TryStatement @try => Statement.Try(
                RewriteChild(@try.Body, pending),
                @try.Catches.Select(@catch => @catch with { Body = RewriteChild(@catch.Body, pending) }),
                @try.Finally is null ? null : RewriteChild(@try.Finally, pending)),
            _ => statement,
        };
    }

    /// <summary>Rewrites a nested statement, keeping a block a block and wrapping a bare statement only if a declaration was inserted before it.</summary>
    private static Statement RewriteChild(Statement child, IReadOnlyList<LocalSlot> pending)
    {
        if (child is BlockStatement block)
        {
            return Statement.Block(RewriteList(block.Statements, pending));
        }

        var rewritten = RewriteList([child], pending);
        return rewritten.Count == 1 ? rewritten[0] : Statement.Block(rewritten);
    }

    /// <summary>The statement lists nested directly inside <paramref name="statement"/>; a bare nested statement counts as a list of one.</summary>
    private static IEnumerable<IReadOnlyList<Statement>> NestedLists(Statement statement)
    {
        switch (statement)
        {
            case BlockStatement block:
                yield return block.Statements.ToList();
                break;
            case IfStatement @if:
                yield return AsList(@if.Then);
                if (@if.Else is not null)
                {
                    yield return AsList(@if.Else);
                }
                break;
            case WhileStatement loop:
                yield return AsList(loop.Body);
                break;
            case ForStatement loop:
                yield return loop.Initializer.ToList();
                yield return loop.Iterator.ToList();
                yield return AsList(loop.Body);
                break;
            case ForEachStatement loop:
                yield return AsList(loop.Body);
                break;
            case SwitchStatement @switch:
                foreach (var @case in @switch.Cases)
                {
                    yield return @case.Body.ToList();
                }
                break;
            case TryStatement @try:
                yield return AsList(@try.Body);
                foreach (var @catch in @try.Catches)
                {
                    yield return AsList(@catch.Body);
                }
                if (@try.Finally is not null)
                {
                    yield return AsList(@try.Finally);
                }
                break;
        }
    }

    private static IReadOnlyList<Statement> AsList(Statement statement)
        => statement is BlockStatement block ? block.Statements.ToList() : [statement];

    /// <summary>Whether the statement's own expressions read or write <paramref name="name"/>, ignoring nested statements.</summary>
    private static bool MentionsShallow(Statement statement, string name) => statement switch
    {
        ExpressionStatement expression => Mentions(expression.Expression, name),
        ReturnStatement @return => Mentions(@return.Expression, name),
        ThrowStatement @throw => Mentions(@throw.Expression, name),
        IfStatement @if => Mentions(@if.Condition, name),
        WhileStatement loop => Mentions(loop.Condition, name),
        ForStatement loop => Mentions(loop.Condition, name),
        ForEachStatement loop => Mentions(loop.Collection, name),
        SwitchStatement @switch => Mentions(@switch.Expression, name),
        VariableDeclarationStatement declaration => Mentions(declaration.Declaration.Initializer, name),
        _ => false,
    };

    private static bool Mentions(Statement statement, string name)
        => MentionsShallow(statement, name)
        || NestedLists(statement).Any(list => list.Any(nested => Mentions(nested, name)));

    private static bool Mentions(Expression? expression, string name) => expression switch
    {
        IdentifierExpression identifier => identifier.Name == name,
        UnaryExpression unary => Mentions(unary.Operand, name),
        BinaryExpression binary => Mentions(binary.Left, name) || Mentions(binary.Right, name),
        ConditionalExpression conditional => Mentions(conditional.Condition, name) || Mentions(conditional.Then, name) || Mentions(conditional.Else, name),
        CallExpression call => Mentions(call.Target, name) || call.Arguments.Any(argument => Mentions(argument, name)),
        MemberAccessExpression member => Mentions(member.Target, name),
        IndexAccessExpression index => Mentions(index.Target, name) || index.Indices.Any(i => Mentions(i, name)),
        ObjectCreationExpression creation => creation.Arguments.Any(argument => Mentions(argument, name))
            || creation.Initializer.Any(initializer => Mentions(initializer.Value, name)),
        TupleExpression tuple => tuple.Elements.Any(element => Mentions(element, name)),
        CastExpression cast => Mentions(cast.Expression, name),
        IsInstanceExpression isInstance => Mentions(isInstance.Expression, name),
        AwaitExpression @await => Mentions(@await.Expression, name),
        AssignExpression assignment => Mentions(assignment.Target, name) || Mentions(assignment.Value, name),
        LambdaExpression lambda => lambda.Body switch
        {
            ExprBody body => Mentions(body.Value, name),
            BlockBody body => Mentions(body.Block, name),
            _ => false,
        },
        _ => false,
    };
}
