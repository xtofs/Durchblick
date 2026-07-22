---
id: 0004
type: bug
severity: medium
status: open
area: structurer
---

# Issue 0004 — cached delegate null-check pattern

## Summary

The structurer does not yet recognize the compiler-generated cached delegate initialization pattern.

## Category

bug

## Severity

medium

## Reproduction

Decompiling `Ufo.ComponentStore<T>.All` currently skips the method at `dup`:

```il
IL_0000: ldarg.0
IL_0001: ldfld System.Collections.Generic.Dictionary`2[Ufo.EntityId,System.Collections.Generic.List`1[T]] _components
IL_0006: callvirt ValueCollection get_Values()
IL_000B: ldsfld System.Func`2[System.Collections.Generic.List`1[T],System.Collections.Generic.IEnumerable`1[T]] <>9__3_0
IL_0010: dup
IL_0011: brtrue.s IL_002A
```

The same shape also appears in `Ufo.ComponentStore<T>.Add` with `<>9__1_0`.

## Expected

The decompiler should recognize this as a compiler-generated static delegate-cache null check and reconstruct the call using the effective delegate expression, without exposing the cache field or failing on `dup`.

## Actual

The structurer throws:

```text
Unsupported IL opcode for body reconstruction at IL_0010: dup.
```

## Notes

- This is likely the start of the Roslyn cached-lambda/static-delegate pattern: load cached field, duplicate it for a null check, branch if already initialized, otherwise create/store the delegate and continue.
- The pattern should probably be handled as a higher-level stack/control-flow recognition rather than as an isolated `dup` instruction.
