namespace Durchblick.Tests;

using Durchblick.ControlFlow;
using Durchblick.IL;

/// <summary>
/// Unit tests for the CFG analyses (dominators, post-dominators, natural loops) over hand-built
/// control-flow graphs. Only successor edges matter here, so instruction indices are dummies.
/// </summary>
public class DominanceTests
{
    // 0 → {1, 2}, 1 → {3}, 2 → {3}, 3 → {}   (an if/else diamond)
    private static readonly ControlFlowGraph Diamond = Graph([1, 2], [3], [3], []);

    // 0 → {1}, 1 → {2, 3}, 2 → {1}, 3 → {}   (a while loop: header 1, body block 2, exit 3)
    private static readonly ControlFlowGraph Loop = Graph([1], [2, 3], [1], []);

    // 0 → {1}, 1 → {2}, 2 → {}               (a straight sequence)
    private static readonly ControlFlowGraph Sequence = Graph([1], [2], []);

    [Fact]
    public void Diamond_merge_block_is_dominated_by_entry_only()
    {
        var dominators = DominatorTree.Build(Diamond);

        Assert.Equal(0, dominators.ImmediateDominator(3));
        Assert.True(dominators.Dominates(0, 3));
        Assert.False(dominators.Dominates(1, 3));
        Assert.True(dominators.Dominates(3, 3)); // reflexive
    }

    [Fact]
    public void Diamond_branch_join_is_the_merge_block()
    {
        var postDominators = PostDominatorTree.Build(Diamond);

        // The join where both arms of the branch at block 0 reconverge.
        Assert.Equal(3, postDominators.ImmediatePostDominator(0));
        Assert.Equal(3, postDominators.ImmediatePostDominator(1));
        Assert.True(postDominators.PostDominates(3, 0));
    }

    [Fact]
    public void Diamond_has_no_loops()
    {
        var dominators = DominatorTree.Build(Diamond);

        Assert.Empty(LoopAnalysis.Find(Diamond, dominators));
    }

    [Fact]
    public void Loop_is_detected_from_the_back_edge()
    {
        var dominators = DominatorTree.Build(Loop);

        var loop = Assert.Single(LoopAnalysis.Find(Loop, dominators));

        Assert.Equal(1, loop.Header);
        Assert.Equal([1, 2], loop.Body.OrderBy(block => block));
    }

    [Fact]
    public void Sequence_dominators_and_post_dominators_are_the_neighbours()
    {
        var dominators = DominatorTree.Build(Sequence);
        var postDominators = PostDominatorTree.Build(Sequence);

        Assert.Equal(0, dominators.ImmediateDominator(1));
        Assert.Equal(1, dominators.ImmediateDominator(2));

        Assert.Equal(1, postDominators.ImmediatePostDominator(0));
        Assert.Equal(2, postDominators.ImmediatePostDominator(1));
    }

    /// <summary>Builds a CFG from successor lists; block <c>i</c> has successors <c>successors[i]</c>.</summary>
    private static ControlFlowGraph Graph(params int[][] successors)
    {
        var blocks = successors
            .Select((edges, index) => new BasicBlock(index, index, edges))
            .ToArray();

        return new ControlFlowGraph([], blocks);
    }
}
