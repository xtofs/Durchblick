namespace Durchblick.Tests;

using Durchblick.CSharp.Formatting;
using Durchblick.CSharp.Syntax;

/// <summary>
/// Tests that <see cref="CodeFormatter"/> parenthesizes expressions according to operator
/// precedence and associativity — only where the tree would otherwise re-parse differently.
/// </summary>
public class CodeFormatterTests
{
    private static readonly Expression A = Id("a");
    private static readonly Expression B = Id("b");
    private static readonly Expression C = Id("c");

    [Fact]
    public void Lower_precedence_left_operand_is_parenthesized()
        => Assert.Equal("(a + b) * 2", Format(Mul(Add(A, B), Literal(2))));

    [Fact]
    public void Lower_precedence_right_operand_is_parenthesized()
        => Assert.Equal("a * (b + c)", Format(Mul(A, Add(B, C))));

    [Fact]
    public void Left_associative_same_precedence_left_operand_keeps_no_parentheses()
        => Assert.Equal("a - b - c", Format(Sub(Sub(A, B), C)));

    [Fact]
    public void Left_associative_same_precedence_right_operand_is_parenthesized()
        => Assert.Equal("a - (b - c)", Format(Sub(A, Sub(B, C))));

    [Fact]
    public void Equal_precedence_needs_no_parentheses_between_add_and_subtract_on_the_left()
        => Assert.Equal("a + b - c", Format(Sub(Add(A, B), C)));

    [Fact]
    public void Logical_and_binds_tighter_than_logical_or()
        => Assert.Equal("a && b || c", Format(Or(And(A, B), C)));

    [Fact]
    public void Logical_or_operand_of_logical_and_is_parenthesized()
        => Assert.Equal("a && (b || c)", Format(And(A, Or(B, C))));

    [Fact]
    public void Runtime_generic_type_reference_formats_generic_arguments()
        => Assert.Equal("IEnumerator<int>", Format(Declaration.TypeRef(typeof(IEnumerator<int>))));

    private static string Format(Expression expression)
    {
        var writer = new StringWriter();
        new CodeFormatter(writer).Format($"{expression}");
        return writer.ToString();
    }

    private static string Format(TypeReference typeReference)
    {
        var writer = new StringWriter();
        new CodeFormatter(writer).Format($"{typeReference}");
        return writer.ToString();
    }

    private static IdentifierExpression Id(string name) => Expression.Identifier(name, new SymbolReference(name, SymbolKind.Local));
    private static LiteralExpression Literal(int value) => Expression.Literal(value, BuiltInTypeReferences.Int);
    private static BinaryExpression Add(Expression l, Expression r) => Expression.Binary(BinaryOperator.Add, l, r);
    private static BinaryExpression Sub(Expression l, Expression r) => Expression.Binary(BinaryOperator.Subtract, l, r);
    private static BinaryExpression Mul(Expression l, Expression r) => Expression.Binary(BinaryOperator.Multiply, l, r);
    private static BinaryExpression And(Expression l, Expression r) => Expression.Binary(BinaryOperator.And, l, r);
    private static BinaryExpression Or(Expression l, Expression r) => Expression.Binary(BinaryOperator.Or, l, r);
}
