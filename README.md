
## The actual algorithmic pipeline
Below is the real pipeline used by tools like ILSpy, dnSpy, JetBrains dotPeek, and Rider’s decompiler.
Each bullet begins with a Guided Link so you can dive deeper into any stage.

### 1. IL parsing — read raw IL and metadata
- Parse IL opcodes (variable‑length, stack‑based).

- Load method bodies, exception blocks, locals, metadata tokens.

- Build a linear instruction list.

This is trivial compared to the later phases.

### 2. Control‑flow graph construction

- Identify basic blocks.
- Resolve branch targets.

Build a CFG with edges for conditional/unconditional jumps.

Detect exception regions and protected blocks.

This is where the decompiler starts to “see” structure.

3. Stack simulation
IL is stack‑based; C# is expression‑based.
The decompiler simulates the evaluation stack to reconstruct expressions:

Push operands

Pop operands for opcodes

Build expression trees

Track temporary locals introduced by the compiler

This produces a low‑level expression graph.

4. High‑level control‑flow reconstruction
This is the hardest part.

The decompiler must detect:

if / else from branch patterns

while loops from backward edges

for loops from induction variables

switch from jump tables

try/catch/finally from exception blocks

using from try/finally with Dispose

foreach from enumerator patterns

async/await state machines

iterator state machines

This is where pattern recognition becomes essential.

pattern recognition of compiler‑lowered constructs

async/await, foreach, using, lambdas, switch expressions, pattern matching, iterator blocks, etc.
