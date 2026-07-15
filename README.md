# Durchblick

*Durchblick* (German for "clear view" — seeing through to what something really is) is a
decompiler for .NET IL. It reads the CIL byte stream of a compiled method and reconstructs
higher-level, C#-like structure from it.

The project is an early work in progress. The IL reader is solid; control-flow graph
construction and stack-based expression reconstruction exist in prototype form. The later
stages of a real decompiler — control-flow reconstruction, an AST, and a C# printer — are
not built yet. See [ROADMAP.md](ROADMAP.md) for a full gap analysis and the phased plan.

## Status

| Stage | State |
| --- | --- |
| IL parsing + reification | ✅ Working |
| Control-flow graph construction | ⚠️ Prototype |
| Stack simulation → expressions | ⚠️ Prototype (small opcode subset) |
| Control-flow reconstruction (`if` / loops) | ❌ Not started |
| AST + C# pretty-printing | ❌ Not started |

## Repository layout

| Path | Contents |
| --- | --- |
| `src/durchblick` | The decompiler library |
| `samples/demo` | Runnable demo that decompiles a specimen method |
| `specimens/add` | Small C# inputs compiled and fed to the decompiler |

## Building and running

Requires the .NET 10 SDK.

```sh
dotnet build Decompiler.slnx
dotnet run --project samples/demo
```

The demo compiles `specimens/add`, selects a method, dumps its basic blocks, and prints the
reconstructed expression.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — how this codebase is structured and the invariants it
  preserves.
- [FLOW.md](FLOW.md) — background: how a decompiler works in general, the full conceptual
  pipeline from IL to C#.
- [ROADMAP.md](ROADMAP.md) — gap analysis and the phased plan for the remaining work.
