namespace Durchblick.Tests;

using System.Reflection;
using Durchblick.CSharp.Syntax;
using Durchblick.Decompilation;

/// <summary>
/// Tests that every IL local is declared exactly once, in the innermost statement list that contains
/// all of its occurrences, with the first assignment merged into the declaration where that is safe.
/// </summary>
public class LocalDeclarationTests
{
    [Theory]
    [Specimen("specimen.Class1", "Calculate2")]
    public void Merges_the_first_assignment_into_the_declaration(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declaration = body.Statements.OfType<VariableDeclarationStatement>().First().Declaration;
        Assert.Equal("int", declaration.TypeReference.Name);
        Assert.NotNull(declaration.Initializer);

        // The store it replaced is gone: nothing assigns that name any more.
        Assert.DoesNotContain(Assignments(body), name => name == declaration.Name);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate4")]
    public void Declares_a_loop_carried_local_before_the_loop(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        // `accu` and `i` are read inside the loop and written before it, so they belong to the
        // method's own block — declared ahead of the `while`, not inside it.
        var whileIndex = body.Statements.ToList().FindIndex(statement => statement is WhileStatement);
        var declaredBeforeLoop = body.Statements.Take(whileIndex).OfType<VariableDeclarationStatement>().ToList();
        Assert.Equal(2, declaredBeforeLoop.Count);
        Assert.All(declaredBeforeLoop, declaration => Assert.NotNull(declaration.Declaration.Initializer));

        // The updates inside the loop stay plain assignments.
        var loopBody = Assert.IsType<BlockStatement>(body.Statements.OfType<WhileStatement>().Single().Body);
        Assert.All(
            declaredBeforeLoop.Select(declaration => declaration.Declaration.Name),
            name => Assert.Contains(Assignments(loopBody), assigned => assigned == name));
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate6")]
    public void Declares_a_local_used_only_inside_a_loop_in_the_loop_body(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var loopBody = Assert.IsType<BlockStatement>(body.Statements.OfType<WhileStatement>().Single().Body);

        // `var x = enumerator.Current;` is used nowhere else, so it declares inside the loop.
        Assert.Contains(loopBody.Statements, statement => statement is VariableDeclarationStatement { Declaration.Initializer: CallExpression });
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate5")]
    public void Declares_a_local_assigned_in_every_switch_case_before_the_switch(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var switchIndex = body.Statements.ToList().FindIndex(statement => statement is SwitchStatement);
        var result = body.Statements.Take(switchIndex).OfType<VariableDeclarationStatement>().Last().Declaration;

        // Assigned in each case, so it cannot be declared in any one of them — and has no initializer.
        Assert.Null(result.Initializer);
        Assert.All(
            body.Statements.OfType<SwitchStatement>().Single().Cases,
            @case => Assert.Contains(@case.Body.SelectMany(Assignments), name => name == result.Name));
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate1")]
    [Specimen("specimen.Class1", "Calculate2")]
    [Specimen("specimen.Class1", "Calculate3")]
    [Specimen("specimen.Class1", "Calculate4")]
    [Specimen("specimen.Class1", "Calculate5")]
    [Specimen("specimen.Class1", "Calculate6")]
    public void Declares_every_assigned_local_exactly_once(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declared = Declarations(body).ToList();
        Assert.Equal(declared.Count, declared.Distinct().Count());
        Assert.All(Assignments(body).Distinct(), name => Assert.Contains(name, declared));
    }

    /// <summary>The names assigned by <c>name = …;</c> statements anywhere in the subtree.</summary>
    private static IEnumerable<string> Assignments(Statement statement) => statement switch
    {
        ExpressionStatement { Expression: AssignExpression { Target: IdentifierExpression target } } => [target.Name],
        BlockStatement block => block.Statements.SelectMany(Assignments),
        IfStatement @if => Assignments(@if.Then).Concat(@if.Else is null ? [] : Assignments(@if.Else)),
        WhileStatement loop => Assignments(loop.Body),
        SwitchStatement @switch => @switch.Cases.SelectMany(@case => @case.Body.SelectMany(Assignments)),
        _ => [],
    };

    /// <summary>The names introduced by declarations anywhere in the subtree.</summary>
    private static IEnumerable<string> Declarations(Statement statement) => statement switch
    {
        VariableDeclarationStatement declaration => [declaration.Declaration.Name],
        BlockStatement block => block.Statements.SelectMany(Declarations),
        IfStatement @if => Declarations(@if.Then).Concat(@if.Else is null ? [] : Declarations(@if.Else)),
        WhileStatement loop => Declarations(loop.Body),
        SwitchStatement @switch => @switch.Cases.SelectMany(@case => @case.Body.SelectMany(Declarations)),
        _ => [],
    };
}
