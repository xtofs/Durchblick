using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

internal class Program
{
    private static void Main(string[] args)
    {
        var methodInfo = CompileAndLoadSpecimen();

        if (methodInfo == null)
        {
            Console.WriteLine("No IL body available.");
            return;
        }

        var dump = new ILReader(methodInfo);
        while (dump.Read())
        {
            var operand = dump.OperandDisplay;
            Console.WriteLine(operand is null
                ? $"IL_{dump.Offset:X4}: {dump.OpCode}"
                : $"IL_{dump.Offset:X4}: {dump.OpCode} {operand}");
        }

        var expr = BuildExpression(methodInfo, new ILReader(methodInfo));
        Console.WriteLine("expr={0}", expr);
    }

    private static Expression? BuildExpression(MethodInfo methodInfo, ILReader reader)
    {
        // var isStatic = methodInfo.IsStatic;
        var locals = CreateLocals(methodInfo);

        var stack = new Stack<Expression>();
        while (reader.Read())
        {
            var opcode = reader.OpCode;

            // switch on ILOpCode which is a single enum instead of the structured OpCode type
            switch (opcode.ILOpCode)
            {
                case ILOpCode.Nop:
                    break;

                case ILOpCode.Ldarg_0:
                    stack.Push(locals[0]);
                    break;
                case ILOpCode.Ldarg_1:
                    stack.Push(locals[1]);
                    break;
                case ILOpCode.Ldarg_2:
                    stack.Push(locals[2]);
                    break;

                case ILOpCode.Ldloc_0:
                    stack.Push(locals[0]);
                    break;
                case ILOpCode.Ldloc_1:
                    stack.Push(locals[1]);
                    break;
                case ILOpCode.Stloc_0:
                    // var _ = stack.Pop();
                    locals[0] = stack.Pop();
                    break;

                case ILOpCode.Br_s:
                    //throw new NotSupportedException("Branch instructions are not supported in this simulation.");
                    // return stack.Count == 1 ? stack.Pop() : null;
                    goto outer_end;

                case ILOpCode.Add:
                    var right = stack.Pop();
                    var left = stack.Pop();
                    stack.Push(Expression.Add(left, right));
                    break;
            }


            // switch (opcode.Value)
            // {
            //     case OpCodeValues.Nop:
            //         // case OpCodeValues.
            //         break;

            //     case OpCodeValues.Ldarg_0:
            //         stack.Push(locals[0]);
            //         break;
            //     case OpCodeValues.Ldarg_1:
            //         stack.Push(locals[1]);
            //         break;
            //     case OpCodeValues.Ldarg_2:
            //         stack.Push(locals[2]);
            //         break;

            //     case OpCodeValues.Ldloc_0:
            //         stack.Push(locals[0]);
            //         break;
            //     case OpCodeValues.Ldloc_1:
            //         stack.Push(locals[1]);
            //         break;
            //     case OpCodeValues.Stloc_0:
            //         // var _ = stack.Pop();
            //         locals[0] = stack.Pop();
            //         break;

            //     case OpCodeValues.Br_S:
            //         //throw new NotSupportedException("Branch instructions are not supported in this simulation.");
            //         // return stack.Count == 1 ? stack.Pop() : null;
            //         goto outer_end;

            //     case OpCodeValues.Add:
            //         var right = stack.Pop();
            //         var left = stack.Pop();
            //         stack.Push(Expression.Add(left, right));
            //         break;

            //     default:
            //         throw new NotSupportedException($"{opcode} {opcode.OperandType}");
            // }
        }
    outer_end:

        return stack.Count == 1 ? stack.Pop() : null;

        static Expression[] CreateLocals(MethodInfo methodInfo)
        {
            var args = methodInfo.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var ret = Expression.Variable(methodInfo.ReturnType, "ret");
            return [ret, .. args];
        }


    }

    private static MethodInfo? CompileAndLoadSpecimen()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "specimen", "specimen.csproj"));

        var build = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (build is null)
        {
            throw new InvalidOperationException("Failed to start dotnet build.");
        }

        var stdout = build.StandardOutput.ReadToEnd();
        var stderr = build.StandardError.ReadToEnd();
        build.WaitForExit();

        var assemblyPath = ExtractAssemblyPath(stdout);
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var calculateMethod = assembly.GetType("specimen.Class1")?.GetMethod(
            "Calculate",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        Console.WriteLine($"assembly={assembly.FullName}");
        Console.WriteLine($"method={calculateMethod}");
        return calculateMethod;
    }

    private static string ExtractAssemblyPath(string stdout)
    {
        var matches = Regex.Matches(stdout, @"(?<path>\/.*?\.dll)", RegexOptions.Multiline);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Could not find assembly path in build output.{Environment.NewLine}{stdout}");
        }

        return matches[^1].Groups["path"].Value;
    }
}


static class OpCodeExtensions
{
    extension(OpCode opcode)
    {
        public ILOpCode ILOpCode => (ILOpCode)(ushort)opcode.Value;
    }
}