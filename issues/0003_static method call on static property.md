---
id: 0003
type: bug
severity: low
status: open
area: code formatter
---

# Issue 0003 — static method call on static property

The formatter currently emits an invalid static method call on a static property.

## Actual

```csharp
public int GetHashCode()
{
  return Default.GetHashCode(this.Value);
}
```

## Expected

```csharp
return EqualityComparer<int>.Default.GetHashCode(<Value>k__BackingField);
```

## Notes

- `Default` is a static property on `EqualityComparer<int>` and should remain qualified.
- The generated call should target `EqualityComparer<int>.Default`, not `Default` by itself.
- The formatted output should preserve valid C# member access for the static property call.
