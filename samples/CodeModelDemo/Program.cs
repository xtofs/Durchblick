using CSharpCodeModel;
using CSharpCodeModel.Syntax;
using CSharpCodeModel.Semantics;


internal class Program
{
    private static void Main(string[] args)
    {
        var intType = BuiltInTypeReferences.Int;

        var enumTypeDecl = Declaration.Enum(
            name: "MyEnum",
            members: [
                Declaration.Field("MyEnumMember", intType)
            ]);

        var classTypeDecl = Declaration.Class(
            name: "MyClass",
            members: [
                Declaration.Method(
                    name: "GetNumber",
                    returnType: intType,
                    body: Statement.Block([
                        Statement.Return(
                            Expression.Binary(
                                BinaryOperator.Add,
                                Expression.Literal(1, intType),
                                Expression.Literal(2, intType)))
                    ]), modifiers: [Modifier.Public]),
                Declaration.Property("MyProperty", Declaration.TypeRef("MyEnum"))
            ]);

        var unit = Declaration.CompilationUnit([
            Declaration.Namespace("MyNamespace", [enumTypeDecl, classTypeDecl])
        ]);


        // 
        var f = new CodeFormatter(Console.Out);
        f.Format(unit);

        // ///////////////////////////////////////////////////////
        // Bind the compilation unit and create the semantic model

        var binder = new Binder();
        var model = binder.Bind(unit);

        Console.WriteLine($"Expression infos: {model.ExpressionInfos.Count}");
        Console.WriteLine($"Statement infos: {model.StatementInfos.Count}");
        Console.WriteLine($"Pattern infos: {model.PatternInfos.Count}");
        Console.WriteLine($"Diagnostics: {model.Diagnostics.Length}");

        foreach (var d in model.Diagnostics)
        {
            Console.WriteLine("Error: {0}", d);
        }
    }
}