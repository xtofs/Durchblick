# Durchblick

_Durchblick_ (German for "clear view" — seeing through to what something really is) is a
decompiler for .NET IL. It reads the CIL byte stream of a compiled method and reconstructs
higher-level, C#-like structure from it.

The project is an early work in progress. The IL reader is solid; control-flow graph
construction covers basic block splitting, successor edges, dominators, post-dominators, and
natural-loop detection; and the decompiler can rebuild simple expressions plus a small set of
structured method bodies into the target C# code model. Reconstruction is still intentionally
limited to a small opcode and control-flow subset. See [DESIGN.md](DESIGN.md) for the
architecture, background, and the full gap analysis and phased plan.

## Status

| Stage                                 | State                                                                   |
| ------------------------------------- | ----------------------------------------------------------------------- |
| IL parsing + reification              | ✅ Solid, near-complete                                                 |
| Control-flow graph construction       | ⚠️ Prototype; leaders, successor edges, switch edges, and exits work    |
| CFG analyses                          | ⚠️ Dominators, post-dominators, and natural loops exist                 |
| Stack simulation → syntax expression  | ⚠️ Straight-line prototype for arguments, locals, constants, binaries   |
| Structured method-body reconstruction | ⚠️ Limited `if`/`else`, `while`, `switch`, local declarations, returns  |
| C# syntax model + formatter           | ⚠️ Model exists; formatter prints ASTs with expression precedence tests |
| Semantic binding                      | ⚠️ Exists; role in decompilation is not defined yet                     |
| Metadata / PDB symbols                | ⚠️ Spike exists; not integrated into the main pipeline                  |
| Tests                                 | ⚠️ Specimen harness covers CFG, analyses, expressions, bodies, output   |

## Repository layout

Everything lives in a single class library, `src/Durchblick`, whose folders mirror the
decompiler pipeline:

| Path                           | Namespace                  | Contents                                                   |
| ------------------------------ | -------------------------- | ---------------------------------------------------------- |
| `src/Durchblick/IL`            | `Durchblick.IL`            | IL reader, reified instructions and operands               |
| `src/Durchblick/ControlFlow`   | `Durchblick.ControlFlow`   | Basic blocks, CFG construction, dominance, loop analysis   |
| `src/Durchblick/Decompilation` | `Durchblick.Decompilation` | Stack simulation and limited structured body recovery      |
| `src/Durchblick/Metadata`      | `Durchblick.Metadata`      | PE/PDB reading spike (unintegrated)                        |
| `src/Durchblick/CSharp`        | `Durchblick.CSharp.*`      | The output model: C# AST, semantic binding, formatting     |
| `src/Durchblick/Collections`   | `Durchblick.Collections`   | Immutable collection used by the AST                       |
| `samples/disassemble`          | —                          | Demo: dumps IL, basic blocks, CFG edges, and block results |
| `samples/decompile`            | —                          | Demo: decompiles specimen methods into C#-like output      |
| `samples/CodeModel`            | —                          | Demo: builds, binds, and formats a C# AST by hand          |
| `samples/OpCodeTable`          | —                          | Utility: emits an opcode reference table                   |
| `tests/Durchblick.Tests`       | —                          | Specimen-driven xUnit tests (specimens compiled in)        |

Specimen inputs are compiled directly into the test, disassemble, and decompile projects (see their
`Specimens.cs`), so each reflects over its own assembly with no separate specimen project.

## Building and running

Requires the .NET 10 SDK.

```sh
dotnet build Durchblick.slnx
dotnet test
dotnet run --project samples/disassemble
dotnet run --project samples/decompile
dotnet run --project samples/CodeModel
dotnet run --project samples/OpCodeTable
```

The disassemble demo reflects over the specimen methods in its own `specimen` namespace, dumps
each method's basic blocks and CFG edges, and prints the per-block stack simulation when supported.
The decompile demo reflects over its own specimen methods and prints reconstructed C#-like type and
method declarations through the syntax model and formatter.

## Documentation

- [DESIGN.md](DESIGN.md) — architecture and invariants, background on how decompilers work,
  the C# code model, and the roadmap of remaining work.
