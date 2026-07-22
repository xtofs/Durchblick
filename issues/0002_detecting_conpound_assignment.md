---
id: 0001
type: bug
severity: low
status: open
area: structurer
---

# Issue 0002 — detecting compound assignment

## Summary

the source code
```csharp
            sum += x;
```

in the Calculate6 example the code gets decompiled to

```csharp
    local0 = local0 + local2;
``` 

## Category

bug

## Severity

low

