namespace Durchblick.Tests;

using System.Reflection;
using System.Reflection.Metadata;
using Durchblick.CSharp.Syntax;
using Durchblick.Decompilation;
using Durchblick.IL;

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

    [Theory]
    [Specimen("specimen.Class1", "Calculate10")]
    public void Reconstructs_object_creation(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declaration = body.Statements.OfType<VariableDeclarationStatement>().Single();
        var creation = Assert.IsType<ObjectCreationExpression>(declaration.Declaration.Initializer);
        Assert.Equal("Object", creation.Type.Name);
        Assert.Empty(creation.Arguments);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate11")]
    public void Reconstructs_discarded_call_result(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var statement = body.Statements.OfType<ExpressionStatement>().Single();
        Assert.IsType<CallExpression>(statement.Expression);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate12")]
    public void Reconstructs_static_field_read(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declaration = body.Statements.OfType<VariableDeclarationStatement>().Single();
        var identifier = Assert.IsType<IdentifierExpression>(declaration.Declaration.Initializer);
        Assert.Equal("StaticField", identifier.Name);
        Assert.Equal(SymbolKind.Field, identifier.Symbol.Kind);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate13")]
    public void Reconstructs_instance_field_assignment(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var statement = body.Statements.OfType<ExpressionStatement>().Single();
        var assignment = Assert.IsType<AssignExpression>(statement.Expression);
        var target = Assert.IsType<MemberAccessExpression>(assignment.Target);
        Assert.Equal("_mutableField", target.MemberName);
        Assert.IsType<IdentifierExpression>(assignment.Value);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate14")]
    public void Reconstructs_isinst_as_type_safe_conversion(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var declaration = body.Statements.OfType<VariableDeclarationStatement>().Single();
        var conversion = Assert.IsType<IsInstanceExpression>(declaration.Declaration.Initializer);
        Assert.Equal("String", conversion.Type.Name);
        Assert.Equal("System", conversion.Type.Namespace);
        Assert.IsType<IdentifierExpression>(conversion.Expression);
        Assert.Contains(body.Statements, statement => statement is ReturnStatement);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate15")]
    public void Unsupported_instruction_exception_includes_block_instructions(MethodInfo method)
    {
        var exception = Assert.Throws<UnsupportedInstructionException>(() => Decompiler.DecompileBody(method));

        Assert.Equal(ILOpCode.Throw, exception.Instruction.ILOpCode);
        Assert.Contains(exception.BlockInstructions, instruction => instruction.ILOpCode == ILOpCode.Throw);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate16")]
    public void Coerces_int32_literal_to_char_call_argument(MethodInfo method)
    {
        var body = Decompiler.DecompileBody(method);

        var statement = body.Statements.OfType<ExpressionStatement>().Single();
        var call = Assert.IsType<CallExpression>(statement.Expression);
        var argument = Assert.IsType<LiteralExpression>(Assert.Single(call.Arguments));
        Assert.Equal('}', argument.Value);
        Assert.Equal(BuiltInTypeReferences.Char, argument.Type);
    }
}
