
**3. Stack simulation**

IL is stack‑based; C# is expression‑based. The decompiler simulates the
evaluation stack to reconstruct expressions:

- Push operands

- Pop operands for opcodes

- Build expression trees

- Track temporary locals introduced by the compiler

This produces a **low‑level expression graph**.

**4. High‑level control‑flow reconstruction**

This is the hardest part.

The decompiler must detect:

- if / else from branch patterns

- while loops from backward edges

- for loops from induction variables

- switch from jump tables

- try/catch/finally from exception blocks

- using from try/finally with Dispose

- foreach from enumerator patterns

- async/await state machines

- iterator state machines

This is where pattern recognition becomes essential.

**🔍 Pattern recognition: the heart of decompilation**

The C# compiler lowers many constructs into verbose IL. Decompilers must
detect these patterns and "lift" them back.

Here are the major patterns:

**5. Foreach pattern**

Compiler lowering:

csharp

foreach (var x in xs) { \... }

Becomes IL equivalent to:

csharp

var enumerator = xs.GetEnumerator();

try {

while (enumerator.MoveNext()) {

var x = enumerator.Current;

\...

}

}

finally {

enumerator.Dispose();

}

The decompiler recognizes:

- A GetEnumerator call

- A MoveNext loop

- A Current load

- A try/finally disposing pattern

Then rewrites it as a foreach.

**6. Using pattern**

Lowered to:

csharp

var tmp = expr;

try {

\...

}

finally {

if (tmp != null) tmp.Dispose();

}

Pattern recognition:

- Local temp

- try/finally

- Dispose call → rewrite as using.

**7. Async/await state machine**

Lowered into a **state machine struct/class** with:

- MoveNext() method

- builder.AwaitUnsafeOnCompleted calls

- state field

- taskBuilder field

- multiple switch(state) blocks

Decompilers detect this pattern and reconstruct:

csharp

async Task Foo() {

await Bar();

}

**8. Iterator blocks**

Lowered into a state machine implementing IEnumerable\<T\> and
IEnumerator\<T\>.

Pattern recognition:

- yield return → state transitions

- yield break → final state

- MoveNext() with switch table

**9. Switch expressions**

Lowered into nested branches or jump tables. Decompilers detect the
shape and rewrite back into:

csharp

var x = y switch {

1 =\> \"one\",

\_ =\> \"other\"

};

**10. Pattern matching**

Lowered into:

- Type tests (isinst)

- Null checks

- Branches

- Local assignments

Decompilers detect the combination and reconstruct:

csharp

if (obj is Foo f) { \... }

**🧱 11. AST reconstruction**

Once patterns are recognized, the decompiler builds a **high‑level
AST**:

- Expressions

- Statements

- Blocks

- Types

- Members

This AST is now "C#‑like".

**🧹 12. High‑level simplification passes**

- Remove redundant temporaries

- Inline simple locals

- Simplify boolean expressions

- Remove dead code

- Normalize operator precedence

- Reconstruct lambdas and closures

- Reconstruct anonymous types

- Reconstruct LINQ query syntax (optional)

**🖨️ 13. C# pretty‑printing**

Finally, the AST is printed as C# code.

This is not trivial --- the printer must:

- Respect operator precedence

- Insert parentheses only when needed

- Format generics, attributes, modifiers

- Emit idiomatic C#

- Avoid ambiguous constructs

**🧠 Why this works**

Because the C# compiler is **deterministic** and **pattern‑based**, its
lowering is predictable. Decompilers exploit this determinism.
