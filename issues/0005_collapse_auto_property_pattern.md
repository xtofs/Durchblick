---
id: 0005
type: enhancement
severity: low
status: open
area: post-processing
---

# Issue 0005 — collapse explicit auto-property pattern

## Summary

After auto-property metadata is decompiled into an explicit backing field plus explicit accessor bodies, a later post-processing pass should collapse that pattern into C# auto-property syntax.

## Category

enhancement

## Severity

low

## Current Output

The auto-property feature intentionally emits the lowered shape first:

```csharp
private readonly int Value__BackingField;

public int Value
{
    get
    {
        return Value__BackingField;
    }

    init
    {
        Value__BackingField = value;
    }
}
```

For mutable properties, the setter accessor uses `set` instead of `init`.

## Expected Future Output

A post-processing pass should recognize the matching backing field and accessor bodies and render the simpler source-level form:

```csharp
public int Value { get; init; }
```

or:

```csharp
public int Value { get; set; }
```

## Notes

- The current explicit field/accessor output is useful as an intermediate representation and should remain the lower-level decompilation result.
- The simplification should only happen when the backing field is used exclusively by the matching property accessors and the accessor bodies exactly match the auto-property pattern.
- The pass should preserve `init` versus `set` based on the setter metadata.
