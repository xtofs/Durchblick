# Durchblick — Design

How this codebase is structured, the invariants it preserves, background on how decompilers
work, and the roadmap of remaining work. This is the single design document; the
[README](README.md) covers orientation and build instructions.

- [Pipeline overview](#pipeline-overview)
- [Background: how a decompiler works](#background-how-a-decompiler-works)
- [Architecture](#architecture)
- [The C# code model](#the-c-code-model)
- [Status & roadmap](#status--roadmap)

---

## Pipeline overview

Durchblick is a single class library (`src/Durchblick`) evolving into a .NET IL decompiler.
The conceptual pipeline (IL → CFG → expressions → structured control flow → AST → C#) maps
onto namespaces and folders as follows:

| Stage                               | Namespace / folder             | Type(s)                               | Status                                              |
| ----------------------------------- | ------------------------------ | ------------------------------------- | --------------------------------------------------- |
| Read the IL byte stream             | `Durchblick.IL`                | `ILReader`                            | ✅ Implemented                                      |
| Reify instructions/operands         | `Durchblick.IL`                | `Instruction`, `Operand`              | ✅ Implemented                                      |
| Build basic blocks / CFG            | `Durchblick.ControlFlow`       | `BasicBlock`, `BasicBlockBuilder`     | ⚠️ Prototype                                        |
| Simulate the stack into expressions | `Durchblick.Decompilation`     | `Decompiler`                          | ⚠️ Prototype                                        |
| Reconstruct control flow            | —                              | —                                     | ❌ Not started                                      |
| Target AST                          | `Durchblick.CSharp.Syntax`     | `AstNode` hierarchy                   | ✅ Model exists, not yet produced by the decompiler |
| Semantic model over the AST         | `Durchblick.CSharp.Semantics`  | `Binder`, `SemanticModel`, symbols    | ⚠️ Exists; role in the decompiler not yet defined   |
| Print C#                            | `Durchblick.CSharp.Formatting` | `CodeFormatter`, `IndentedTextWriter` | ⚠️ Prints the AST; not wired to decompilation       |
| PE/PDB metadata                     | `Durchblick.Metadata`          | `Metadata`                            | ⚠️ Unintegrated spike                               |
| Shared collections                  | `Durchblick.Collections`       | `ImmutableCollection<T>`              | ✅ Used by the C# model                             |

The IL-facing namespaces (`IL`, `ControlFlow`, `Decompilation`) are the _input_ side; the
`CSharp.*` namespaces are the _output_ side. The unimplemented middle — control-flow
structuring that turns CFG + expressions into `Durchblick.CSharp.Syntax` nodes — is the heart
of the remaining work.

---

## Background: how a decompiler works

The conceptual pipeline that tools like ILSpy, dnSpy, and dotPeek use to turn IL back into
C#. This section is about decompilation _in general_, not this codebase.

1. **IL parsing** — decode the variable-length, stack-based opcode stream; load method
   bodies, exception regions, locals, metadata tokens. Trivial compared to what follows.
2. **Control-flow graph construction** — identify basic blocks, resolve branch targets,
   build edges for conditional/unconditional jumps, detect exception regions.
3. **Stack simulation** — IL is stack-based, C# is expression-based. Simulate the evaluation
   stack to rebuild expression trees and track compiler-introduced temporaries.
4. **High-level control-flow reconstruction** — the hardest part: recover `if`/`else` from
   branch patterns, `while` from back edges, `for` from induction variables, `switch` from
   jump tables, `try`/`catch`/`finally` from exception regions.
5. **Pattern recognition** — the C# compiler lowers many constructs into verbose IL
   deterministically; decompilers run that lowering in reverse:
   - `foreach` → `GetEnumerator` call + `MoveNext` loop + `Current` load + disposing try/finally
   - `using` → local temp + try/finally + null-checked `Dispose`
   - `async`/`await` → state-machine type with `MoveNext()`, builder calls, and a state field
   - iterator blocks → state machine implementing `IEnumerable<T>`/`IEnumerator<T>`
   - `switch` expressions → nested branches or jump tables
   - pattern matching → `isinst` type tests + null checks + branches
6. **AST reconstruction** — build a C#-like tree of expressions, statements, and members.
7. **Simplification passes** — remove redundant temporaries, inline simple locals, simplify
   booleans, remove dead code, reconstruct lambdas/closures/anonymous types.
8. **Pretty-printing** — emit idiomatic C#: correct operator precedence, minimal
   parentheses, proper formatting of generics, attributes, and modifiers.

This works because the C# compiler's lowering is **deterministic and pattern-based**;
decompilers exploit that determinism.

---

## Architecture

### IL reading: two layers, strictly separated

#### Layer 1 — `ILReader`: non-reified pull cursor

`ILReader` ([src/Durchblick/IL/ILReader.cs](src/Durchblick/IL/ILReader.cs)) is a pull-based
cursor over the raw IL byte stream of one method body, in the style of
`XmlReader`/`Utf8JsonReader`:

- `Read()` advances to the next instruction; `Seek(ilOffset)` repositions the cursor
  (IL branch targets are always valid instruction starts, so random access is cheap).
- The current instruction is exposed as `Offset`, `OpCode`, and typed operand properties
  (`Int32Operand`, `BranchTarget`, `MethodOperand`, ...). Which property is valid is
  discriminated by `OperandType`; a mismatched access throws `InvalidOperationException`.
- Operand decoding is **lazy**: nothing beyond the opcode is decoded until an operand
  property is read. In particular, metadata token resolution
  (`Module.ResolveMethod/Field/Type/Member/String/Signature`, with generic context) only
  happens on access.
- Reading allocates nothing per instruction.

**Invariant: `ILReader` is the single source of decoding truth.** No other type decodes IL
bytes. `ILReader` must never _require_ reification to function.

#### Layer 2 — optional reification: `Operand` and `Instruction`

`Operand` (tagged union struct: one object reference + 64 bits of value data, discriminated
by `OperandType`, guarded typed getters) and `Instruction(Offset, OpCode, Operand)` are
**materialized snapshots** of the reader's current state, produced only via
`ILReader.Operand`, `ILReader.Current`, or `ILReader.ToInstructions()`.

**Invariant: reification is layered on top of the reader, never the other way around.**
`Instruction`/`Operand` hold decoded values; they never touch IL bytes.

#### Consumption rule

- A **single forward pass** (dumping, linear stack simulation) consumes `ILReader` directly —
  no allocation, lazy operands.
- **Multi-pass or random-access analyses** (basic blocks, CFG, dominators) materialize the
  instruction list **once** via `ToInstructions()` and share it.

### Data structures are separate from the algorithms that build them

Analysis results are plain immutable records; construction logic lives in dedicated builders:

| Data (pure)  | Built by            |
| ------------ | ------------------- |
| `BasicBlock` | `BasicBlockBuilder` |

Future work (CFG edges, dominator trees, loop detection) follows the same split.

### Conventions

- Branch and switch targets are normalized to **absolute IL offsets** at decode time; raw
  relative deltas never leave `ILReader`.
- "IL" is capitalized in type names: `ILReader`, not `IlReader`.
- Values with a closed set of kinds carry their kind explicitly (`OperandType` tag) rather
  than having it inferred by consumers.
- File-scoped namespaces throughout; folder structure mirrors namespaces.

---

## The C# code model

`Durchblick.CSharp.*` is the _output side_ of the decompiler: an in-memory model of C#
source, independent of Roslyn.

### Syntax (`Durchblick.CSharp.Syntax`)

An immutable AST built from records deriving from `AstNode`:

- **Expressions** — `LiteralExpression`, `IdentifierExpression`, `BinaryExpression`,
  `CallExpression`, `MemberAccessExpression`, `ConditionalExpression`, `LambdaExpression`,
  `CastExpression`, `AwaitExpression`, ...
- **Statements** — `BlockStatement`, `IfStatement`, `WhileStatement`, `ForStatement`,
  `ForEachStatement`, `SwitchStatement`, `TryStatement`, `ThrowStatement`, ...
- **Declarations** — compilation unit, namespaces, types, members, variables.
- **Patterns** — type, constant, relational, logical, recursive.
- Auxiliary nodes: `TypeReference`, modifiers, attributes, switch cases, catch clauses.

Nodes are pure data; they carry no binding or type information.

### Semantics (`Durchblick.CSharp.Semantics`)

A second phase that binds an AST: `Binder`/`SemanticBinder` walk the tree, build a symbol
table (`NamespaceSymbol`, `TypeSymbol` and subtypes, `MethodSymbol`, `LocalSymbol`, ...),
resolve types (`TypeResolver`), and produce a `SemanticModel` with per-node info records
(`ExpressionInfo`, `StatementInfo`, `PatternInfo`) and diagnostics.

Its role in the decompiler is **not yet defined** — decompilation may only need the syntax
model plus the formatter. It is kept because binding will become relevant when reconstructed
code must be type-checked or names must be resolved/disambiguated. See roadmap.

### Formatting (`Durchblick.CSharp.Formatting`)

`CodeFormatter` renders a syntax tree to C# text through `IndentedTextWriter`. This is the
seed of the pretty-printer stage; precedence-aware minimal parenthesization is still open.

### Collections (`Durchblick.Collections`)

`ImmutableCollection<T>` (with `CollectionBuilder` support) is the sequence type used by the
AST nodes.

---

## Status & roadmap

The remaining work separates into three kinds of problem, triaged independently:

- **Mistakes** — existing code that is wrong.
- **Implementation gaps** — right shape, incomplete coverage.
- **Architecture / flow gaps** — capabilities not yet designed.

### Current maturity

| Pipeline stage                           | Location                              | Status                                                      |
| ---------------------------------------- | ------------------------------------- | ----------------------------------------------------------- |
| 1. IL parsing + reification              | `Durchblick.IL`                       | ✅ Solid, near-complete                                     |
| 2. CFG construction                      | `Durchblick.ControlFlow`              | ⚠️ Prototype, covers leaders/successor edges                |
| 3. Stack simulation                      | `Durchblick.Decompilation.Decompiler` | ⚠️ Straight-line expression prototype                       |
| 4. Control-flow reconstruction           | —                                     | ❌ Not started                                              |
| 5–8. Patterns / AST production / printer | —                                     | ❌ Not started (AST _model_ exists)                         |
| Output AST + formatter                   | `Durchblick.CSharp.*`                 | ✅ Model + basic formatter                                  |
| Semantic binding                         | `Durchblick.CSharp.Semantics`         | ⚠️ Exists, role undefined                                   |
| Metadata / PDB symbols                   | `Durchblick.Metadata`                 | ⚠️ Unintegrated spike                                       |
| Tests                                    | `tests/Durchblick.Tests`              | ⚠️ Specimen harness + CFG and expression-simulator coverage |

`ILReader` is the strongest asset and needs no rework: zero-alloc pull cursor, lazy operand
decoding, correct 1-/2-byte opcode tables, correct `InlineSwitch` sizing, absolute-offset
branch normalization, clean reification layer. **Build on it; don't touch it.**

### A. Mistakes (existing code is wrong)

| #         | Location                                     | Defect                                                                               | Consequence                                                                                  |
| --------- | -------------------------------------------- | ------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| ~~A1~~ ✅ | `BasicBlockBuilder.Build`                    | Only split blocks _after_ a branch/return/throw; never _before_ a branch target.     | **Fixed.** Leaders are computed in instruction-index space, including branch/switch targets. |
| ~~A2~~ ✅ | Successor computation, `Cond_Branch` case    | Recorded only the taken target; **dropped the fall-through edge**.                   | **Fixed.** Conditional branches preserve target edge(s) and fall-through edge.               |
| ~~A3~~ ✅ | Successor computation                        | No `InlineSwitch` case; called `GetBranchTarget()` which throws on a switch operand. | **Fixed.** Switch targets are handled in case order.                                         |
| ~~A4~~ ✅ | Successor computation, fall-through case     | Returned `[]` instead of the fall-through block.                                     | **Fixed.** Non-terminating blocks point to the next block.                                   |
| A5        | Control-flow expression reconstruction       | Branching control flow is intentionally rejected by expression decompilation.        | `if`/ternary/loop reconstruction needs a separate structuring pass.                          |
| ~~A6~~ ✅ | Decompiler block addressing                  | Old prototype used offset-keyed block maps and assumed entry offset `0`.             | **Fixed.** Current expression prototype consumes the index-based CFG.                        |
| ~~A7~~ ✅ | Decompiler opcode switch vs binary operators | `Sub`, `Ceq`, `Clt` were in the operator dictionary but not in the simulator switch. | **Fixed for the straight-line prototype.**                                                   |
| ~~A8~~ ✅ | Decompiler parameter handling                | Old prototype always prepended a `this` parameter, even for static methods.          | **Fixed** (guarded by `!methodInfo.IsStatic`).                                               |
| ~~A9~~ ✅ | Cosmetic                                     | Dead code, unused usings, stale doc paths.                                           | **Fixed.**                                                                                   |

### B. Implementation gaps (right shape, incomplete coverage)

- **B1 — `Decompiler.DecompileExpression` covers a small straight-line opcode subset.** Missing, at minimum:
  - Loads: `ldarg.3`, `ldarg.s`, `ldarg`, `ldloc.2/3`, `ldloc.s`, `ldloc`, `ldc.i4.4..8`,
    `ldc.i4.m1`, `ldc.i4.s`, `ldc.i4`, `ldc.i8`, `ldc.r4/r8`, `ldstr`, `ldnull`, `ldarga`/`ldloca`.
  - Stores: `starg`, `stloc.2/3`, `stloc.s`, `stloc`.
  - Arithmetic/logic: `sub` (see A7), `rem`, `neg`, `and`, `or`, `xor`, `shl`, `shr`, `not`, `.ovf`.
  - Comparisons: `ceq`, `clt` (see A7), `cgt.un`, `clt.un`.
  - Calls/objects: `call`, `callvirt`, `newobj`, `ret`-with-value.
  - Fields/arrays/convert: `ldfld`/`stfld`/`ldsfld`, `dup`, `pop`, `ldlen`, `ldelem*`/`stelem*`,
    `conv.*`, `box`/`unbox`, `isinst`, `castclass`.
- **B2 — operand-carrying forms unused.** The reader exposes `VariableIndex`/`Int32Operand`;
  the simulator never consumes them, so only the compact `.0/.1/.2` forms work. Drive
  loads/stores/constants off the operand instead of enumerating fixed indices.
- **B3 — statement/control-flow tests are still missing.** `Calculate`/`Calculate2` cover
  straight-line expressions, and `Calculate3` covers CFG shape plus the current unsupported
  branching boundary. Loop/switch statement reconstruction is not covered because it does not
  exist yet.
- **B4 — the C# code model has no tests.** Syntax construction, binding, and formatting are
  exercised only by `samples/CodeModelDemo`.

### C. Architecture / flow gaps (not yet designed — decisions required)

- **C1 — Output IR.** ~~Decision: dedicated C# AST vs. LINQ Expressions.~~ **Decided:** the
  dedicated AST is `Durchblick.CSharp.Syntax`, now part of this library. The straight-line
  expression prototype emits `Durchblick.CSharp.Syntax.Expression` nodes. Remaining work:
  method-body and control-flow reconstruction must emit `Durchblick.CSharp.Syntax.Statement`
  nodes.
- **C2 — Control-flow structuring does not exist.** No `if/else`, loops, or `switch`
  recovery; expression decompilation deliberately rejects branching control flow. Needs a
  structuring pass over the CFG (dominator tree + natural-loop / interval analysis,
  Cifuentes-style) emitting structured statements. This is the heart of the project.
- **C3 — Metadata source fork.** Two unreconciled ways to read an assembly coexist: the
  reflection path (`MethodInfo` → `ILReader`/`BasicBlockBuilder`/`Decompiler`, plus `Dotnet.CompileAndLoad`) and
  the `System.Reflection.Metadata` PE/PDB path (`Durchblick.Metadata`, unintegrated).
  **Decision:** converge the real pipeline on `MetadataReader` (no assembly load/execution,
  full metadata, portable PDB), keeping reflection only in the specimen test harness.
  `ILReader` currently takes `MethodBase`; switching means re-plumbing token resolution
  (`Module.Resolve*` → `MetadataReader`).
- **C4 — No exception-region model.** Handler regions are never read; handler entries aren't
  leaders. Blocks try/catch/finally, `using`, `foreach`, async, and makes CFG construction
  technically incomplete.
- **C5 — No symbol/name recovery integration.** `Durchblick.Metadata` reads PDB local names
  but nothing consumes them; locals are named via `LocalVariableInfo.ToString()`. Design how
  PDB names flow into the AST.
- **C6 — No real pretty-printer.** `CodeFormatter` renders the AST but precedence-aware
  minimal parenthesization and idiomatic output are unstarted. The disassemble demo has a
  small local expression renderer for the current syntax-expression subset.
- **C7 — Semantic model's role.** `Durchblick.CSharp.Semantics` binds hand-built ASTs, but
  the decompiler has no defined use for it yet. Decide whether reconstructed code gets bound
  (e.g., to validate output or resolve names) or whether the semantic layer serves a
  different consumer.

### Phased roadmap

Ordered by dependency. Each phase is independently verifiable.

**Phase 0 — Foundations & decisions** _(unblocks everything)_

- [x] Test project with specimen harness (`tests/Durchblick.Tests`, `[Specimen]` attribute).
- [x] C1 decision: dedicated C# AST (`Durchblick.CSharp.Syntax`), merged into this library.
- [ ] C3 decision recorded and acted on: `MetadataReader` for the pipeline, reflection for
      the harness.

**Phase 1 — Fix the CFG** _(A1–A4, A6)_

- [x] Wire up leader detection; build blocks by cutting at leader boundaries.
- [x] Successors: `Branch`→[target]; `Cond_Branch`→[target, fall-through];
      `Switch`→[targets…, fall-through]; `Return`/`Throw`→[]; fall-through block→[next].
- [ ] Golden CFG tests for a loop specimen and a compiled switch specimen.

**Phase 2 — Complete the linear simulator** _(B1–B3, A7)_

- [ ] Broaden `Decompiler.DecompileExpression` to full straight-line opcode coverage, operand-driven (B2).
- [x] Wire `sub`/`ceq`/`clt` into the straight-line simulator (A7).
- [ ] Add `call`/`newobj`/field/`dup`/`pop`/`conv` handling.
- [ ] Per-opcode simulator tests.

**Phase 3 — Control-flow reconstruction** _(C2 — the core)_

- [ ] Dominator tree + natural-loop detection over the CFG.
- [ ] Structurer: `if/else` from conditional diamonds, `while`/`do` from back edges,
      sequences, multiple returns — emitting `Durchblick.CSharp.Syntax` statements (C1).
- [ ] Add `ToBody`/`ToStatements` for structured method-body reconstruction.
- [ ] Golden tests: `Calculate3` → real `if`; a loop specimen → `while`.

**Phase 4 — Metadata & symbols** _(C3, C5, C4-read)_

- [ ] Integrate `Durchblick.Metadata`: PDB local + parameter names into the AST; PDB optional.
- [ ] Read exception regions (data model only here; structuring in Phase 5).

**Phase 5 — Exception handling & compiler patterns** _(C4, background §4–5)_

- [ ] try/catch/finally structuring; then `using`, `foreach`, `switch` tables, pattern
      matching; async/iterator state machines last (long tail).

**Phase 6 — Pretty-printer** _(C6)_

- [ ] `CodeFormatter` → idiomatic C# with correct operator precedence and minimal
      parentheses.

### Verification

- Build: `dotnet build Durchblick.slnx`
- Tests: `dotnet test`
- Demo: `dotnet run --project samples/disassemble` (compiles `specimens/add`, dumps blocks
  and supported straight-line expressions);
  `dotnet run --project samples/CodeModelDemo`
  (builds and formats an AST, binds it, reports semantic info).
- End-to-end target per phase: compile a specimen with the C# compiler, decompile it, and
  assert the reconstructed structure (Phase 1: block/edge shape; Phase 3: `if`/loop
  presence; Phase 6: emitted C# source) against a golden expectation.
