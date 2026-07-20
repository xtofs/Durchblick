using System.Reflection;
using Durchblick.ControlFlow;

internal class Program
{
    // Disassemble only the specimen methods that live in this namespace of the demo's own assembly.
    private const string SpecimenNamespace = "specimen";

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static void Main()
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type.Namespace == SpecimenNamespace);

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(DeclaredMembers))
            {
                Disassemble(type, method);
            }
        }
    }

    private static void Disassemble(Type type, MethodInfo method)
    {
        var cfg = BasicBlockBuilder.Build(method);

        Console.WriteLine();
        Console.WriteLine($"{type.Name}.{method.Name}");
        foreach (var block in cfg.Blocks)
        {
            Console.WriteLine();
            Console.WriteLine($"    # IL_{cfg.Instructions[block.StartIndex].Offset:X4} -> [{string.Join(", ", from s in block.Successors select string.Format("IL_{0:X4}", cfg.Instructions[cfg.Blocks[s].StartIndex].Offset))}]");
            try
            {
                var result = LinqDecompiler.DecompileBlock(cfg, method, block);
                if (result.Locals.Count > 0 || result.Stack.Count > 0)
                {
                    var locals = result.Locals.Count > 0 ? string.Join(", ", result.Locals.Select(kv => $"loc{kv.Key} := {kv.Value.Format()}")) : "";
                    var stack = result.Stack.Count > 0 ? "; " + string.Join(", ", result.Stack.Select(e => e.Format())) : "";
                    Console.WriteLine($"    # {locals}{stack}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    # {ex.Message}");
            }
            foreach (var instruction in cfg.Instructions[block.StartIndex..(block.EndIndex + 1)])
            {
                Console.WriteLine($"    {instruction}");
            }
        }
    }
}
