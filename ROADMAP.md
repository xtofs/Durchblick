# Durchblick — Roadmap & Gap Analysis

## Context

Durchblick is a .NET IL decompiler. The intended pipeline (per `ARCH.md`/`ARCHITECTURE.md`)
is the standard one: **IL parsing → CFG → stack simulation → control-flow reconstruction →
AST → C# pretty-printing**. Today the repo implements the first third cleanly plus a
proof-of-concept stack simulator; the rest is unstarted or stubbed.

This document separates the remaining work into three kinds of problem so they can be triaged
independently:

- **Mistakes** — existing code that is wrong (drops CFG edges, dead leader algorithm, etc.).
- **Implementation gaps** — code whose shape is right but coverage is incomplete
  (`RunBlockLeaders` bails on most opcodes).
- **Architecture / flow gaps** — capabilities not yet designed (control-flow structuring,
  the output AST, metadata source, exception handling).

---

## Current maturity

| Pipeline stage (ARCH.md) | File | Status |
| --- | --- | --- |
| 1. IL parsing + reification | `ILReader.cs`, `Instruction.cs` | ✅ Solid, near-complete |
| 2. CFG construction | `BasicBlockBuilder.cs`, `BasicBlock.cs` | ⚠️ Prototype, buggy |
| 3. Stack simulation | `Decompiler.cs` | ⚠️ Hardcoded ~15 opcodes |
| 4. Control-flow reconstruction | — | ❌ Not started |
| 5–13. Patterns / AST / printer | — | ❌ Not started |
| Metadata / PDB symbols | `Metadata.cs` | ⚠️ Unintegrated spike |
| Tests | — | ❌ None |

`ILReader` is the strongest asset and needs no rework: zero-alloc pull cursor, lazy operand
decoding, correct 1-/2-byte opcode tables, correct `InlineSwitch` sizing, absolute-offset
branch normalization, clean reification layer. **Build on it; don't touch it.**

---

## A. Mistakes (existing code is wrong)

| # | Location | Defect | Consequence |
| --- | --- | --- | --- |
| A1 | `BasicBlockBuilder.Build` (`:22`) | Only splits blocks *after* a branch/return/throw; never *before* a branch target. The correct `FindLeaders` is written but commented out (`:24`) and never called. | Any target landing mid-run (loops, backward edges) is not a block boundary → wrong blocks. |
| A2 | `FindSuccessorBlocks` `Cond_Branch` (`:61`) | Records only the taken target; **drops the fall-through edge**. `Branch` and `Cond_Branch` cases are byte-identical. | Conditional blocks have 1 successor instead of 2 → CFG is not a valid flow graph. |
| A3 | `FindSuccessorBlocks` (`:56`) | No `InlineSwitch` case; calls `GetBranchTarget()` which throws on a switch operand. | Any `switch` crashes CFG construction. |
| A4 | `FindSuccessorBlocks` `FlowControl.Next` (`:67`) | Returns `[]` instead of the fall-through block. (Latent today because A1 means blocks only ever end on branches; becomes live once A1 is fixed.) | Fall-through blocks lose their successor. |
| A5 | `Decompiler.ToExpression` `Brfalse_s` (`:128`) | Treats `Brfalse_s` as an unconditional jump to its target — ignores fall-through, never emits an `if`. | `Calculate3`'s `if (a > 3)` is silently mis-reconstructed. |
| A6 | `Decompiler.ToExpression` (`:112`,`:125`) | `blocks[target]` / `blocks[0]` throw `KeyNotFoundException` when a target isn't a block leader (a direct result of A1); `blocks[0]` assumes entry == offset 0. | Crashes on non-trivial input; fragile entry assumption. |
| A7 | `Decompiler` switch (`:64`) vs `BinaryOperators` (`:87`) | `Sub`, `Ceq`, `Clt` are in the operator dictionary but **not** in the `case` labels → they fall to `default` and throw `NotSupportedException`. | Half-wired: subtraction and equality/less-than comparisons don't work despite looking supported. |
| A8 | `Decompiler.GetParametersAndLocals` (`:101`) | Always prepends a `this` parameter, even for static methods. | For static methods every `ldarg.N` index is off by one. |
| A9 | Cosmetic | `var exit = instruction; ;` (double `;`, unused) `BasicBlockBuilder.cs:34`; unused `Trace`/`Level` in `Decompiler`; unused usings. (~~stale `ARCHITECTURE.md` path~~ — fixed during docs reconciliation.) | Noise. |

## B. Implementation gaps (right shape, incomplete coverage)

- **B1 — `RunBlockLeaders` covers ~15 hardcoded opcodes.** Missing, at minimum:
  - Loads: `ldarg.3`, `ldarg.s`, `ldarg`, `ldloc.2/3`, `ldloc.s`, `ldloc`, `ldc.i4.4..8`,
    `ldc.i4.m1`, `ldc.i4.s`, `ldc.i4`, `ldc.i8`, `ldc.r4/r8`, `ldstr`, `ldnull`, `ldarga`/`ldloca`.
  - Stores: `starg`, `stloc.2/3`, `stloc.s`, `stloc`.
  - Arithmetic/logic: `sub` (see A7), `rem`, `neg`, `and`, `or`, `xor`, `shl`, `shr`, `not`, `.ovf`.
  - Comparisons: `ceq`, `clt` (see A7), `cgt.un`, `clt.un`.
  - Calls/objects: `call`, `callvirt`, `newobj`, `ret`-with-value.
  - Fields/arrays/convert: `ldfld`/`stfld`/`ldsfld`, `dup`, `pop`, `ldlen`, `ldelem*`/`stelem*`,
    `conv.*`, `box`/`unbox`, `isinst`, `castclass`.
- **B2 — operand-carrying forms unused.** The reader exposes `VariableIndex`/`Int32Operand`; the
  simulator never consumes them, so only the compact `.0/.1/.2` forms work. Drive loads/stores/
  constants off the operand instead of enumerating fixed indices.
- **B3 — only `Calculate3` is exercised.** `Calculate`/`Calculate2` and any loop/switch case are
  never run; no coverage of the paths that expose A1–A5.

## C. Architecture / flow gaps (not yet designed — decisions required)

- **C1 — Output IR (the gating decision).** Currently `System.Linq.Expressions` is reused as the
  IR. Neat for pure expressions (`return a+b`) but cannot represent statements, side-effecting
  assignments, labels/gotos, or multiple returns. **Decision:** dedicated minimal C# AST
  (Statement/Expression hierarchy) vs. staying on LINQ Expressions. This gates stages 4 and 6–13.
  *Recommendation:* build a small dedicated AST.
- **C2 — Control-flow structuring does not exist.** No `if/else`, loops, or `switch` recovery;
  `ToExpression` returns a single expression, not a body. Needs a structuring pass over the CFG
  (dominator tree + natural-loop / interval analysis, Cifuentes-style) emitting structured
  statements. This is ARCH.md §4 — the heart of the project.
- **C3 — Metadata source fork.** Two unreconciled ways to read an assembly coexist: the
  reflection path (`MethodInfo` → `ILReader`/`Decompiler`, plus `Dotnet.CompileAndLoad`) and the
  `System.Reflection.Metadata` PE/PDB path (`Metadata.cs`, unintegrated). **Decision:** converge
  the real pipeline on `MetadataReader` (no assembly load/execution, full metadata, portable PDB),
  keeping reflection only in the specimen test harness. `ILReader` currently takes `MethodBase`;
  switching means re-plumbing token resolution (`Module.Resolve*` → `MetadataReader`).
- **C4 — No exception-region model.** Handler regions (`ExceptionHandlingClauses` / metadata) are
  never read; handler entries aren't leaders (the builder even notes this). Blocks try/catch/
  finally, using, foreach, async (ARCH.md §5–8) and makes CFG construction technically incomplete.
- **C5 — No symbol/name recovery integration.** `Metadata.cs` reads PDB local names but nothing
  consumes them; locals are named via `LocalVariableInfo.ToString()`. Design how PDB names flow
  into the AST.
- **C6 — No pretty-printer.** Output is `Expression.ToString()` / block dumps. ARCH.md §13
  (precedence, minimal parens, idiomatic C#) is unstarted and depends on C1.
- **C7 — No test architecture.** One method hardcoded in `Program.cs`. A decompiler needs
  specimen-driven golden tests (compile specimen → decompile → compare) so every phase is
  verifiable.

---

## Phased roadmap

Ordered by dependency. Each phase is independently verifiable.

### Phase 0 — Foundations & decisions *(unblocks everything)*
- [ ] **C7:** Add an xUnit test project `tests/durchblick.tests`; reuse `Dotnet.CompileAndLoad`
      as the specimen harness. Establish golden tests.
- [ ] **C1 decision:** dedicated C# AST vs. LINQ Expressions. Record in `ARCHITECTURE.md`.
- [ ] **C3 decision:** `MetadataReader` for the pipeline, reflection for the harness. Record it.
- [ ] **A9:** dead-code / docs cleanup (cheap, do alongside).

### Phase 1 — Fix the CFG *(mistakes A1–A4, A6, A9)*
- [ ] Wire up `FindLeaders`; build blocks by cutting at leader boundaries (two-pass).
- [ ] Successors: `Branch`→[target]; `Cond_Branch`→[target, fall-through];
      `Switch`→[targets…, fall-through]; `Return`/`Throw`→[]; fall-through block→[next].
- [ ] Golden `CfgTests` for `Calculate1/2/3` + a loop specimen + a switch specimen.

### Phase 2 — Complete the linear simulator *(gaps B1–B3, mistakes A5, A7, A8)*
- [ ] Broaden `RunBlockLeaders` to full straight-line opcode coverage, operand-driven (B2).
- [ ] Wire `sub`/`ceq`/`clt` into the switch (A7); fix static-method `this` (A8).
- [ ] Add `call`/`newobj`/field/`dup`/`pop`/`conv` handling.
- [ ] Per-opcode simulator tests.

### Phase 3 — Control-flow reconstruction *(C2 — the core)*
- [ ] Dominator tree + natural-loop detection over the CFG.
- [ ] Structurer: `if/else` from conditional diamonds, `while`/`do` from back edges, sequences,
      multiple returns → emit the Phase-0 AST.
- [ ] Replace single-expression `ToExpression` with `ToBody`/`ToStatements`.
- [ ] Golden tests: `Calculate3` → real `if`; a loop specimen → `while`.

### Phase 4 — Metadata & symbols *(C3, C5, C4-read)*
- [ ] Integrate `Metadata.cs`: PDB local + parameter names into the AST; make PDB optional.
- [ ] Read exception regions (data model only here; structuring in Phase 5).

### Phase 5 — Exception handling & compiler patterns *(C4, ARCH.md §5–10)*
- [ ] try/catch/finally structuring; then `using`, `foreach`, `switch` tables, pattern matching;
      async/iterator state machines last (long tail).

### Phase 6 — Pretty-printer *(C6, ARCH.md §13)*
- [ ] AST → idiomatic C# with correct operator precedence and minimal parentheses.

---

## Verification

- Build/run demo: `dotnet run --project samples/demo` (compiles `specimens/add`, dumps blocks,
  prints reconstructed output).
- Tests (from Phase 0): `dotnet test`.
- End-to-end target per phase: compile a specimen with the C# compiler, decompile it, and assert
  the reconstructed structure (Phase 1: block/edge shape; Phase 3: `if`/loop presence; Phase 6:
  emitted C# source) against a golden expectation.
