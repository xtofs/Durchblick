namespace CSharpCodeModel.Collections;

public static class ImmutableCollectionBuilder
{
    public static ImmutableCollection<T> Create<T>(ReadOnlySpan<T> items)
        => new(items.ToArray());
}

