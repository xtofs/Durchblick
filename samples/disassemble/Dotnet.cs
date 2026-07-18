using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

class Dotnet
{

    public static MethodInfo? GetMethod(Assembly assembly, string typeName, string methodName)
    {
        const BindingFlags Core = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        return assembly.GetType(typeName.Trim())?.GetMethod(methodName.Trim(), Core);
    }


    public const BindingFlags BindingFlagsAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static Assembly? CompileAndLoad(string projectPath, out string assemblyPath)
    {
        string stdout = Build(projectPath);

        assemblyPath = ExtractAssemblyPath(stdout);
        var context = System.Runtime.Loader.AssemblyLoadContext.Default;
        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        return assembly;
    }

    private static string Build(string projectPath)
    {
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
        return stdout;
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