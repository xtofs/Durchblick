namespace Durchblick.Tests;

using System.Reflection;
using Durchblick.CSharp.Syntax;
using Durchblick.Decompilation;

public class DecompilerExpressionTests
{
    [Theory]
    [Specimen("specimen.Class1", "Calculate1")]
    public void Decompiles_simple_return_expression(MethodInfo methodInfo)
    {
        var expression = Decompiler.DecompileExpression(methodInfo);

        var binary = Assert.IsType<BinaryExpression>(expression);
        Assert.Equal(BinaryOperator.Add, binary.Operator);
        AssertIdentifier("a", binary.Left);
        AssertIdentifier("b", binary.Right);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate2")]
    public void Decompiles_return_expression_through_locals(MethodInfo methodInfo)
    {
        var expression = Decompiler.DecompileExpression(methodInfo);

        var multiply = Assert.IsType<BinaryExpression>(expression);
        Assert.Equal(BinaryOperator.Multiply, multiply.Operator);

        var add = Assert.IsType<BinaryExpression>(multiply.Left);
        Assert.Equal(BinaryOperator.Add, add.Operator);
        AssertIdentifier("a", add.Left);
        AssertIdentifier("b", add.Right);

        var literal = Assert.IsType<LiteralExpression>(multiply.Right);
        Assert.Equal(2, literal.Value);
        Assert.Equal(BuiltInTypeReferences.Int, literal.Type);
    }

    [Theory]
    [Specimen("specimen.Class1", "Calculate3")]
    public void Rejects_branching_control_flow_for_expression_decompilation(MethodInfo methodInfo)
    {
        Assert.Throws<NotSupportedException>(() => Decompiler.DecompileExpression(methodInfo));
    }

    private static void AssertIdentifier(string name, Expression expression)
    {
        var identifier = Assert.IsType<IdentifierExpression>(expression);
        Assert.Equal(name, identifier.Name);
        Assert.Equal(SymbolKind.Parameter, identifier.Symbol.Kind);
    }
}