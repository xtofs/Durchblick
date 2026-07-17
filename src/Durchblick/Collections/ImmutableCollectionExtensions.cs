namespace Durchblick.Collections;

using System.Collections.Generic;

public static class ImmutableCollectionExtensions
{
    public static ImmutableCollection<T> ToImmutableCollection<T>(this IEnumerable<T> source)
    {
        if (source is ImmutableCollection<T> immutable)
            return immutable;

        return new ImmutableCollection<T>(source.ToArray());
    }
}

