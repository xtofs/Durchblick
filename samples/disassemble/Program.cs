using System.Reflection;
using Durchblick.ControlFlow;

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

                Console.WriteLine();
                Console.WriteLine($"{type.Name}.{method.Name}");
                foreach (var block in cfg.Blocks)
                {
                    Console.WriteLine($"    Block IL_{cfg.Instructions[block.StartIndex].Offset:X4}  [{string.Join(", ", from s in block.Successors select string.Format("IL_{0:X4}", cfg.Instructions[cfg.Blocks[s].StartIndex].Offset))}]");
                    try
                    {
                        var result = LinqDecompiler.DecompileExpression(cfg, method, block);
                        var locals = result.Locals.Count > 0 ? string.Join(", ", result.Locals.Select(kv => $"{kv.Key}: {kv.Value.Format()}")) : "";
                        Console.WriteLine($"        # {result.Expression.Format()} [{locals}]");
                    }
                    catch (NotSupportedException ex)
                    {
                        Console.WriteLine($"        # {ex.Message}");
                    }
                    catch (System.InvalidOperationException ex)
                    {
                        Console.WriteLine($"        # {ex.Message}");
                    }
                    foreach (var instruction in cfg.Instructions[block.StartIndex..(block.EndIndex + 1)])
                    {
                        Console.WriteLine($"        {instruction}");
                    }
                }
            }
        }
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
