namespace Durchblick.Tests;

using System.Reflection;
using Durchblick.CSharp.Syntax;
using Durchblick.Decompilation;

/// <summary>
/// Golden tests for structured method-body reconstruction (control-flow recovery): a two-way
/// branch becomes an <see cref="IfStatement"/>, a natural loop becomes a <see cref="WhileStatement"/>.
/// Assertions are on the reconstructed AST shape (there is no C# text formatter for bodies yet).
/// </summary>
public class DecompilerBodyTests
{
    [Theory]
    [Specimen("specimen.Class1", "Calculate3")]
    public void Reconstructs_if_from_conditional_branch(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var ifStatement = body.Statements.OfType<IfStatement>().Single();
        Assert.NotNull(ifStatement.Else); // `if (…) { … } else { … }`

        // Each arm assigns the result temp; the method then returns it.
        Assert.IsType<BlockStatement>(ifStatement.Then);
        Assert.IsType<BlockStatement>(ifStatement.Else);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate4")]
    public void Reconstructs_while_from_loop(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var whileStatement = body.Statements.OfType<WhileStatement>().Single();

        // The debug `for` loop stores its comparison to a temp in the header, so the loop is
        // recovered in canonical form: `while (true) { …; if (!cond) break; … }`.
        var loopBody = Assert.IsType<BlockStatement>(whileStatement.Body);
        var exitGuard = loopBody.Statements.OfType<IfStatement>().First();
        Assert.Equal(UnaryOperator.Not, Assert.IsType<UnaryExpression>(exitGuard.Condition).Operator);
        var guardThen = Assert.IsType<BlockStatement>(exitGuard.Then);
        Assert.Contains(guardThen.Statements, statement => statement is BreakStatement);

        // The method returns after the loop.
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate5")]
    public void Reconstructs_switch_from_jump_table(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var switchStatement = body.Statements.OfType<SwitchStatement>().Single();

        // Four constant cases (labels 0..3) plus a default.
        var labels = switchStatement.Cases
            .Select(c => c.Pattern).OfType<ConstantPattern>()
            .Select(pattern => (int)pattern.Value.Value);
        Assert.Equal([0, 1, 2, 3], labels);
        Assert.Contains(switchStatement.Cases, c => c.Pattern is DiscardPattern);

        // Each case assigns the result temp and breaks; the method returns after the switch.
        Assert.All(switchStatement.Cases, @case => Assert.Contains(@case.Body, s => s is BreakStatement));
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate6")]
    public void Reconstructs_loop_with_method_calls(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var whileStatement = body.Statements.OfType<WhileStatement>().Single();
        var loopBody = Assert.IsType<BlockStatement>(whileStatement.Body);
        Assert.Contains(loopBody.Statements, statement => statement is VariableDeclarationStatement { Declaration.Initializer: CallExpression });
        Assert.Contains(loopBody.Statements, statement => statement is VariableDeclarationStatement { Declaration.Initializer: MemberAccessExpression { MemberName: "Current" } });
        Assert.Contains(loopBody.Statements, statement => statement is IfStatement);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate7")]
    public void Reconstructs_if_from_equality_branch(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var ifStatement = body.Statements.OfType<IfStatement>().Single();
        Assert.IsType<IdentifierExpression>(ifStatement.Condition);
        var declaration = body.Statements
            .OfType<VariableDeclarationStatement>()
            .Single(statement => statement.Declaration.Initializer is BinaryExpression { Operator: BinaryOperator.Equals });
        var condition = Assert.IsType<BinaryExpression>(declaration.Declaration.Initializer);
        Assert.Equal(BinaryOperator.Equals, condition.Operator);
        Assert.IsType<IdentifierExpression>(condition.Left);
        Assert.IsType<IdentifierExpression>(condition.Right);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate8")]
    public void Reconstructs_string_literal(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declaration = body.Statements.OfType<VariableDeclarationStatement>().Single();
        var literal = Assert.IsType<LiteralExpression>(declaration.Declaration.Initializer);
        Assert.Equal("hello", literal.Value);
        Assert.Equal(BuiltInTypeReferences.String, literal.Type);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate9")]
    public void Reconstructs_instance_field_read(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declaration = body.Statements.OfType<VariableDeclarationStatement>().Single();
        var memberAccess = Assert.IsType<MemberAccessExpression>(declaration.Declaration.Initializer);
        Assert.Equal("_field", memberAccess.MemberName);
        Assert.IsType<IdentifierExpression>(memberAccess.Target);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }
}
