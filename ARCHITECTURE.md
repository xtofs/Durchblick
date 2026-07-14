# Architecture

Design decisions for this repository. [ARCH.md](ARCH.md) is background material on how
decompilers work in general; this document records the decisions made _here_ and the
invariants that must be preserved.

## IL reading: two layers, strictly separated

### Layer 1 — `ILReader`: non-reified pull cursor

`ILReader` (src/stack-simulation/ILReader.cs) is a pull-based cursor over the raw IL
byte stream of one method body, in the style of `XmlReader`/`Utf8JsonReader`:

- `Read()` advances to the next instruction; `Seek(ilOffset)` repositions the cursor
  (IL branch targets are always valid instruction starts, so random access is cheap).
- The current instruction is exposed as `Offset`, `OpCode`, and typed operand
  properties (`Int32Operand`, `BranchTarget`, `MethodOperand`, ...). Which property is
  valid is discriminated by `OperandType`; a mismatched access throws
  `InvalidOperationException`.
- Operand decoding is **lazy**: nothing beyond the opcode is decoded until an operand
  property is read. In particular, metadata token resolution
  (`Module.ResolveMethod/Field/Type/Member/String/Signature`, with generic context) only
  happens on access.
- Reading allocates nothing per instruction.

**Invariant: `ILReader` is the single source of decoding truth.** No other type decodes
IL bytes. `ILReader` must never _require_ reification to function.

### Layer 2 — optional reification: `Operand` and `Instruction`

`Operand` (tagged union struct: one object reference + 64 bits of value data,
discriminated by `OperandType`, guarded typed getters) and
`Instruction(Offset, OpCode, Operand)` are **materialized snapshots** of the reader's
current state, produced only via `ILReader.Operand`, `ILReader.Current`, or
`ILReader.ToInstructions()`.

**Invariant: reification is layered on top of the reader, never the other way around.**
`Instruction`/`Operand` hold decoded values; they never touch IL bytes.

### Consumption rule

- A **single forward pass** (dumping, linear stack simulation) consumes `ILReader`
  directly — no allocation, lazy operands.
- **Multi-pass or random-access analyses** (basic blocks, CFG, dominators) materialize
  the instruction list **once** via `ToInstructions()` and share it.

## Data structures are separate from the algorithms that build them

Analysis results are plain immutable records; construction logic lives in dedicated
builders:

| Data (pure)  | Built by            |
| ------------ | ------------------- |
| `BasicBlock` | `BasicBlockBuilder` |

Future work (CFG edges, dominator trees, loop detection) follows the same split.

## Conventions

- Branch and switch targets are normalized to **absolute IL offsets** at decode time;
  raw relative deltas never leave `ILReader`.
- "IL" is capitalized in type names: `ILReader`, not `IlReader`.
- Values with a closed set of kinds carry their kind explicitly (`OperandType` tag)
  rather than having it inferred by consumers.
