namespace Durchblick.ControlFlow;

/// <summary>Successor/predecessor adjacency over a <see cref="ControlFlowGraph"/>, in block-index space.</summary>
internal static class CfgAdjacency
{
    public static IReadOnlyList<int>[] Successors(ControlFlowGraph graph)
    {
        var successors = new IReadOnlyList<int>[graph.Blocks.Count];
        for (var i = 0; i < successors.Length; i++)
        {
            successors[i] = graph.Blocks[i].Successors;
        }

        return successors;
    }

    public static IReadOnlyList<int>[] Predecessors(ControlFlowGraph graph) =>
        Invert(Successors(graph), graph.Blocks.Count);

    /// <summary>Inverts an adjacency: edge <c>u→v</c> becomes <c>v→u</c>.</summary>
    public static IReadOnlyList<int>[] Invert(IReadOnlyList<int>[] successors, int nodeCount)
    {
        var predecessors = new List<int>[nodeCount];
        for (var i = 0; i < nodeCount; i++)
        {
            predecessors[i] = [];
        }

        for (var u = 0; u < successors.Length; u++)
        {
            foreach (var v in successors[u])
            {
                predecessors[v].Add(u);
            }
        }

        return predecessors;
    }
}

/// <summary>
/// Iterative dominator computation (Cooper, Harvey &amp; Kennedy, "A Simple, Fast Dominance
/// Algorithm"). Generic over any directed graph given as node count, entry, and adjacency.
/// </summary>
internal static class Dominance
{
    /// <summary>
    /// Returns the immediate-dominator array. <c>idom[entry] == entry</c>; nodes unreachable from
    /// <paramref name="entry"/> get <c>-1</c>.
    /// </summary>
    public static int[] Compute(int nodeCount, int entry, IReadOnlyList<int>[] successors, IReadOnlyList<int>[] predecessors)
    {
        var reversePostOrder = ReversePostOrder(nodeCount, entry, successors);

        // Position of each node within the reverse-postorder walk; entry is smallest (0).
        var order = new int[nodeCount];
        Array.Fill(order, -1);
        for (var i = 0; i < reversePostOrder.Count; i++)
        {
            order[reversePostOrder[i]] = i;
        }

        var idom = new int[nodeCount];
        Array.Fill(idom, -1);
        idom[entry] = entry;

        bool changed;
        do
        {
            changed = false;
            foreach (var b in reversePostOrder)
            {
                if (b == entry)
                {
                    continue;
                }

                var newIdom = -1;
                foreach (var p in predecessors[b])
                {
                    if (idom[p] == -1)
                    {
                        continue; // predecessor not processed yet
                    }

                    newIdom = newIdom == -1 ? p : Intersect(p, newIdom, idom, order);
                }

                if (newIdom != -1 && idom[b] != newIdom)
                {
                    idom[b] = newIdom;
                    changed = true;
                }
            }
        }
        while (changed);

        return idom;

        static int Intersect(int a, int b, int[] idom, int[] order)
        {
            while (a != b)
            {
                while (order[a] > order[b])
                {
                    a = idom[a];
                }

                while (order[b] > order[a])
                {
                    b = idom[b];
                }
            }

            return a;
        }
    }

    private static List<int> ReversePostOrder(int nodeCount, int entry, IReadOnlyList<int>[] successors)
    {
        var visited = new bool[nodeCount];
        var postOrder = new List<int>(nodeCount);
        var stack = new Stack<(int Node, int NextChild)>();

        visited[entry] = true;
        stack.Push((entry, 0));
        while (stack.Count > 0)
        {
            var (node, nextChild) = stack.Pop();
            if (nextChild < successors[node].Count)
            {
                stack.Push((node, nextChild + 1));
                var child = successors[node][nextChild];
                if (!visited[child])
                {
                    visited[child] = true;
                    stack.Push((child, 0));
                }
            }
            else
            {
                postOrder.Add(node);
            }
        }

        postOrder.Reverse();
        return postOrder;
    }
}

/// <summary>
/// Immediate-dominator tree over a <see cref="ControlFlowGraph"/> (entry is block 0). Indices are
/// block indices into <see cref="ControlFlowGraph.Blocks"/>. Built by <see cref="Build"/>.
/// </summary>
public sealed class DominatorTree
{
    private readonly int[] _idom;

    private DominatorTree(int[] idom, int root)
    {
        _idom = idom;
        Root = root;
    }

    /// <summary>The entry block; it is its own immediate dominator.</summary>
    public int Root { get; }

    /// <summary>Immediate dominator of <paramref name="block"/>; the root's is itself.</summary>
    public int ImmediateDominator(int block) => _idom[block];

    /// <summary>Does <paramref name="dominator"/> dominate <paramref name="block"/>? (Every block dominates itself.)</summary>
    public bool Dominates(int dominator, int block)
    {
        for (var b = block; b >= 0; b = _idom[b])
        {
            if (b == dominator)
            {
                return true;
            }

            if (b == Root)
            {
                return false;
            }
        }

        return false;
    }

    public static DominatorTree Build(ControlFlowGraph graph)
    {
        var successors = CfgAdjacency.Successors(graph);
        var predecessors = CfgAdjacency.Invert(successors, graph.Blocks.Count);
        var idom = Dominance.Compute(graph.Blocks.Count, 0, successors, predecessors);
        return new DominatorTree(idom, 0);
    }
}

/// <summary>
/// Immediate-post-dominator tree: dominators of the <em>reversed</em> CFG rooted at a virtual exit
/// node (index <see cref="VirtualExit"/> == <c>Blocks.Count</c>) linked from every block with no
/// successors. <see cref="ImmediatePostDominator"/> of a two-way branch block is the join node
/// where its arms reconverge (or <see cref="VirtualExit"/> if they never do). Built by <see cref="Build"/>.
/// </summary>
public sealed class PostDominatorTree
{
    private readonly int[] _ipdom;

    private PostDominatorTree(int[] ipdom, int virtualExit)
    {
        _ipdom = ipdom;
        VirtualExit = virtualExit;
    }

    /// <summary>The synthetic sink that unifies all method exits; it is its own post-dominator.</summary>
    public int VirtualExit { get; }

    /// <summary>Immediate post-dominator of <paramref name="block"/>; <see cref="VirtualExit"/> means "no join within the method".</summary>
    public int ImmediatePostDominator(int block) => _ipdom[block];

    /// <summary>Does <paramref name="postDominator"/> post-dominate <paramref name="block"/>?</summary>
    public bool PostDominates(int postDominator, int block)
    {
        for (var b = block; b >= 0; b = _ipdom[b])
        {
            if (b == postDominator)
            {
                return true;
            }

            if (b == VirtualExit)
            {
                return false;
            }
        }

        return false;
    }

    public static PostDominatorTree Build(ControlFlowGraph graph)
    {
        var blockCount = graph.Blocks.Count;
        var virtualExit = blockCount;
        var nodeCount = blockCount + 1;

        // Reversed graph: for each edge u→v add v→u; the virtual exit points at every real exit.
        var reversed = new List<int>[nodeCount];
        for (var i = 0; i < nodeCount; i++)
        {
            reversed[i] = [];
        }

        for (var u = 0; u < blockCount; u++)
        {
            var successors = graph.Blocks[u].Successors;
            foreach (var v in successors)
            {
                reversed[v].Add(u);
            }

            if (successors.Count == 0)
            {
                reversed[virtualExit].Add(u);
            }
        }

        var predecessors = CfgAdjacency.Invert(reversed, nodeCount);
        var ipdom = Dominance.Compute(nodeCount, virtualExit, reversed, predecessors);
        return new PostDominatorTree(ipdom, virtualExit);
    }
}
