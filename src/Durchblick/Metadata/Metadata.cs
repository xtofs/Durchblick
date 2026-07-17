namespace Durchblick.Metadata;

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal class Metadata
{
    internal static void Load(string assemblyPath)
    {

        using var peStream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(peStream);

        var mdReader = peReader.GetMetadataReader();

        // Load PDB
        using var pdbStream = File.OpenRead(Path.ChangeExtension(assemblyPath, ".pdb"));
        var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var pdbReader = pdbProvider.GetMetadataReader();

        foreach (var methodHandle in mdReader.MethodDefinitions)
        {
            var methodDef = mdReader.GetMethodDefinition(methodHandle);
            var methodName = mdReader.GetString(methodDef.Name);

            // Map method → debug info
            var debugHandle = methodHandle.ToDebugInformationHandle();

            Console.WriteLine($"Method: {methodName}");

            // Enumerate all scopes and filter by method
            foreach (var scopeHandle in pdbReader.LocalScopes)
            {
                var scope = pdbReader.GetLocalScope(scopeHandle);

                if (scope.Method == debugHandle.ToDefinitionHandle())
                {
                    foreach (var localHandle in scope.GetLocalVariables())
                    {
                        var local = pdbReader.GetLocalVariable(localHandle);
                        var name = pdbReader.GetString(local.Name);

                        Console.WriteLine($"  Local: {name}");
                    }
                }
            }
        }
    }
}