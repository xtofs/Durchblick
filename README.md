# Durchblick

_Durchblick_ (German for "clear view" — seeing through to what something really is) is a
decompiler for .NET IL. It reads the CIL byte stream of a compiled method and reconstructs
higher-level, C#-like structure from it.

The project is an early work in progress. The IL reader is solid; control-flow graph
construction exists in prototype form; and a small straight-line stack simulator can rebuild
simple returned expressions into the target C# code model. Full control-flow reconstruction and
method-body generation are not started yet. See [DESIGN.md](DESIGN.md) for the architecture,
background, and the full gap analysis and phased plan.

## Status

| Stage                                 | State                                                      |
| ------------------------------------- | ---------------------------------------------------------- |
| IL parsing + reification              | ✅ Solid, near-complete                                    |
| Control-flow graph construction       | ⚠️ Prototype; leaders and successor edges are covered      |
| Stack simulation → syntax expression  | ⚠️ Straight-line prototype for a small opcode subset       |
| Structured method-body reconstruction | ❌ Not started (`if`/loops/switch/statements)              |
| C# syntax model + formatter           | ✅ Model exists; formatter is basic                        |
| Semantic binding                      | ⚠️ Exists; role in decompilation is not defined yet        |
| Metadata / PDB symbols                | ⚠️ Spike exists; not integrated into the main pipeline     |
| Tests                                 | ⚠️ Specimen harness covers CFG edges and expression slices |

## Repository layout

Everything lives in a single class library, `src/Durchblick`, whose folders mirror the
decompiler pipeline:

| Path                           | Namespace                  | Contents                                               |
| ------------------------------ | -------------------------- | ------------------------------------------------------ |
| `src/Durchblick/IL`            | `Durchblick.IL`            | IL reader, reified instructions and operands           |
| `src/Durchblick/ControlFlow`   | `Durchblick.ControlFlow`   | Basic blocks and CFG construction                      |
| `src/Durchblick/Decompilation` | `Durchblick.Decompilation` | Stack simulation → C# syntax expressions               |
| `src/Durchblick/Metadata`      | `Durchblick.Metadata`      | PE/PDB reading spike (unintegrated)                    |
| `src/Durchblick/CSharp`        | `Durchblick.CSharp.*`      | The output model: C# AST, semantic binding, formatting |
| `src/Durchblick/Collections`   | `Durchblick.Collections`   | Immutable collection used by the AST                   |
| `samples/disassemble`          | —                          | Demo: decompiles a specimen method end to end          |
| `samples/CodeModelDemo`        | —                          | Demo: builds, binds, and formats a C# AST by hand      |
| `specimens/add`                | —                          | Small C# inputs compiled and fed to the decompiler     |
| `tests/Durchblick.Tests`       | —                          | Specimen-driven xUnit tests                            |

## Building and running

Requires the .NET 10 SDK.

```sh
dotnet build Durchblick.slnx
dotnet test
dotnet run --project samples/disassemble
dotnet run --project samples/CodeModelDemo
```

The disassemble demo compiles `specimens/add`, selects each method, dumps its basic blocks,
and prints the expression reconstructed by the straight-line simulator when supported.

## Documentation

- [DESIGN.md](DESIGN.md) — architecture and invariants, background on how decompilers work,
  the C# code model, and the roadmap of remaining work.
