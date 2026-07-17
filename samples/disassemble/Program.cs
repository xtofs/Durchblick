using System.IO;
using System.Linq.Expressions;
using Durchblick.ControlFlow;
using Durchblick.Decompilation;

internal class Program
{
    private static void Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "specimens", "add", "add.csproj");
        var methodInfo = Dotnet.CompileAndLoad(projectPath, "specimen.Class1", "Calculate3", out var assemblyPath);

        if (methodInfo == null)
        {
            Console.WriteLine("No IL body available.");
            return;
        }

        Console.WriteLine();

        var blocks = BasicBlockBuilder.Build(methodInfo);

        foreach (var block in blocks)
        {
            Console.WriteLine($"block IL_{block.StartOffset:X4}  [{string.Join(", ", from e in block.Targets select string.Format("IL_{0:X4}", e.StartOffset))}]");
            foreach (var instruction in block.Instructions)
            {
                Console.WriteLine($"    {instruction}");
            }
        }

        var map = blocks.ToDictionary(b => b.StartOffset);
        Decompiler.GetParametersAndLocals(methodInfo, out var parameters, out var locals);

        var expr = Decompiler.ToExpression(map, parameters, locals);
        Console.WriteLine(expr);

    }

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
