# How a decompiler works

Background material: the conceptual pipeline that tools like ILSpy, dnSpy, dotPeek, and
Rider's decompiler use to turn IL back into C#. This document is about decompilation *in
general* — it is not specific to this codebase. For how Durchblick is actually structured,
see [ARCHITECTURE.md](ARCHITECTURE.md); for the plan to implement these stages, see
[ROADMAP.md](ROADMAP.md).

The pipeline runs in roughly these stages:

## 1. IL parsing — read raw IL and metadata

- Parse IL opcodes (variable-length, stack-based).
- Load method bodies, exception regions, locals, and metadata tokens.
- Build a linear instruction list.

This is trivial compared to the later phases.

## 2. Control-flow graph construction

- Identify basic blocks.
- Resolve branch targets.
- Build a CFG with edges for conditional and unconditional jumps.
- Detect exception regions and protected blocks.

This is where the decompiler starts to "see" structure.

## 3. Stack simulation

IL is stack-based; C# is expression-based. The decompiler simulates the evaluation stack to
reconstruct expressions:

- Push operands.
- Pop operands for opcodes.
- Build expression trees.
- Track temporary locals introduced by the compiler.

This produces a low-level expression graph.

## 4. High-level control-flow reconstruction

This is the hardest part. The decompiler must detect:

- `if` / `else` from branch patterns
- `while` loops from backward edges
- `for` loops from induction variables
- `switch` from jump tables
- `try` / `catch` / `finally` from exception regions
- `using` from try/finally with `Dispose`
- `foreach` from enumerator patterns
- `async` / `await` state machines
- iterator state machines

This is where pattern recognition becomes essential.

## 5. Pattern recognition: the heart of decompilation

The C# compiler lowers many constructs into verbose IL. Decompilers must detect these
patterns and "lift" them back to source-level constructs. The major patterns:

### foreach

Compiler lowering:

```csharp
foreach (var x in xs) { /* ... */ }
```

Becomes IL equivalent to:

```csharp
var enumerator = xs.GetEnumerator();
try
{
    while (enumerator.MoveNext())
    {
        var x = enumerator.Current;
        // ...
    }
}
finally
{
    enumerator.Dispose();
}
```

The decompiler recognizes a `GetEnumerator` call, a `MoveNext` loop, a `Current` load, and a
try/finally that disposes — then rewrites it as `foreach`.

### using

Lowered to:

```csharp
var tmp = expr;
try
{
    // ...
}
finally
{
    if (tmp != null) tmp.Dispose();
}
```

Recognized from the local temp + try/finally + `Dispose` call, and rewritten as `using`.

### async/await state machine

Lowered into a state-machine struct/class with:

- a `MoveNext()` method
- `builder.AwaitUnsafeOnCompleted` calls
- a state field
- a task-builder field
- multiple `switch (state)` blocks

Decompilers detect this pattern and reconstruct:

```csharp
async Task Foo()
{
    await Bar();
}
```

### iterator blocks

Lowered into a state machine implementing `IEnumerable<T>` and `IEnumerator<T>`.
Recognized from:

- `yield return` → state transitions
- `yield break` → final state
- a `MoveNext()` with a switch table

### switch expressions

Lowered into nested branches or jump tables. Decompilers detect the shape and rewrite back
into:

```csharp
var x = y switch
{
    1 => "one",
    _ => "other"
};
```

### pattern matching

Lowered into type tests (`isinst`), null checks, branches, and local assignments. Decompilers
detect the combination and reconstruct:

```csharp
if (obj is Foo f) { /* ... */ }
```

## 6. AST reconstruction

Once patterns are recognized, the decompiler builds a high-level AST of expressions,
statements, blocks, types, and members. This AST is now "C#-like".

## 7. High-level simplification passes

- Remove redundant temporaries.
- Inline simple locals.
- Simplify boolean expressions.
- Remove dead code.
- Normalize operator precedence.
- Reconstruct lambdas and closures.
- Reconstruct anonymous types.
- Reconstruct LINQ query syntax (optional).

## 8. C# pretty-printing

Finally, the AST is printed as C#. This is not trivial — the printer must:

- Respect operator precedence.
- Insert parentheses only when needed.
- Format generics, attributes, and modifiers.
- Emit idiomatic C#.
- Avoid ambiguous constructs.

## Why this works

Because the C# compiler is **deterministic** and **pattern-based**, its lowering is
predictable. Decompilers exploit this determinism to run the lowering in reverse.
