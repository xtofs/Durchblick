using System.IO;
using System.Reflection;
using Durchblick.CSharp.Syntax;
using Durchblick.ControlFlow;
using Durchblick.Decompilation;

internal class Program
{
    private static void Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "specimens", "add", "add.csproj");
        var assembly = Dotnet.CompileAndLoad(projectPath, out var assemblyPath);

        foreach (var type in assembly!.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var cfg = BasicBlockBuilder.Build(method);

                Console.WriteLine($"{type.Name}.{method.Name}");
                foreach (var block in cfg.Blocks)
                {
                    Console.WriteLine($"    block IL_{cfg.Instructions[block.StartIndex].Offset:X4}  [{string.Join(", ", from s in block.Successors select string.Format("IL_{0:X4}", cfg.Instructions[cfg.Blocks[s].StartIndex].Offset))}]");
                    foreach (var instruction in cfg.Instructions[block.StartIndex..(block.EndIndex + 1)])
                    {
                        Console.WriteLine($"        {instruction}");
                    }
                }

                try
                {
                    var expression = Decompiler.DecompileExpression(cfg, method);
                    Console.WriteLine($"    expression: {FormatExpression(expression)}");
                }
                catch (NotSupportedException ex)
                {
                    Console.WriteLine($"    expression: <unsupported: {ex.Message}>");
                }
            }
        }
    }

    private static string FormatExpression(Expression? expression) => expression switch
    {
        null => "<void>",
        IdentifierExpression identifier => identifier.Name,
        LiteralExpression literal => literal.Value?.ToString() ?? "null",
        BinaryExpression binary => $"({FormatExpression(binary.Left)} {FormatBinaryOperator(binary.Operator)} {FormatExpression(binary.Right)})",
        _ => expression.ToString(),
    };

    private static string FormatBinaryOperator(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.And => "&",
        BinaryOperator.Or => "|",
        BinaryOperator.Equals => "==",
        BinaryOperator.NotEquals => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.Greater => ">",
        _ => op.ToString(),
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "specimens")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root (folder containing 'specimens').");
    }

}
