namespace Durchblick.ControlFlow;

/// <summary>A natural loop: its header block and the set of blocks in its body (header included).</summary>
public sealed record NaturalLoop(int Header, IReadOnlySet<int> Body);

/// <summary>
/// Finds natural loops in a <see cref="ControlFlowGraph"/> from its back edges. A back edge is an
/// edge <c>u→v</c> whose target <c>v</c> dominates its source <c>u</c>; <c>v</c> is the loop header.
/// </summary>
public static class LoopAnalysis
{
    public static IReadOnlyList<NaturalLoop> Find(ControlFlowGraph graph, DominatorTree dominators)
    {
        var predecessors = CfgAdjacency.Predecessors(graph);
        var bodyByHeader = new Dictionary<int, HashSet<int>>();

        for (var u = 0; u < graph.Blocks.Count; u++)
        {
            foreach (var v in graph.Blocks[u].Successors)
            {
                if (!dominators.Dominates(v, u))
                {
                    continue; // not a back edge
                }

                if (!bodyByHeader.TryGetValue(v, out var body))
                {
                    body = new HashSet<int> { v };
                    bodyByHeader[v] = body;
                }

                CollectBody(header: v, tail: u, predecessors, body);
            }
        }

        return bodyByHeader
            .OrderBy(entry => entry.Key)
            .Select(entry => new NaturalLoop(entry.Key, entry.Value))
            .ToArray();
    }

    /// <summary>Adds every block that reaches <paramref name="tail"/> without passing through the header.</summary>
    private static void CollectBody(int header, int tail, IReadOnlyList<int>[] predecessors, HashSet<int> body)
    {
        var worklist = new Stack<int>();
        if (body.Add(tail))
        {
            worklist.Push(tail);
        }

        while (worklist.Count > 0)
        {
            var block = worklist.Pop();
            foreach (var predecessor in predecessors[block])
            {
                if (predecessor != header && body.Add(predecessor))
                {
                    worklist.Push(predecessor);
                }
            }
        }
    }
}
