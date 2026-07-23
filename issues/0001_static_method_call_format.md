---
id: 0001
type: bug
severity: medium
status: closed
area: code formatter
---

# Issue 0001 — Double Exit Assignment

## Summary

Static methods calls get formatted without the class name

## Category

bug

## Severity

medium

## Affected Files

src/Durchblick/CSharp/Formatting/CodeFormattingInterpolatedStringHandler.cs

## Reproduction

the current output of the disassemble demo shows the call to the static Method `Enumerable.Range` as

```
IEnumerator<int> local1 = Range(0, a).GetEnumerator();
```

this is not correct C# since it forgets the declaring type name of the static method.
There is of course a way using `static using` to make this work but the general case is to spell the type name out.

## Expected

output should be

```csharp
IEnumerator<int> local1 = Enumerator.Range(0, a).GetEnumerator();
```

## Actual

output is

```csharp
IEnumerator<int> local1 = Range(0, a).GetEnumerator();
```
