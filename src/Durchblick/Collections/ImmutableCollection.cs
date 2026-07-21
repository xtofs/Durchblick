namespace Durchblick.Collections;

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Durchblick.CSharp.Syntax;

[CollectionBuilder(typeof(ImmutableCollectionBuilder), "Create")]
public sealed class ImmutableCollection<T>(T[] items) : IEnumerable<T>
{
    private readonly T[] _items = items;

    public static ImmutableCollection<TypeReference> Empty { get; } = new ImmutableCollection<TypeReference>(Array.Empty<TypeReference>());

    public int Count => _items.Length;
    

    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return $"ImmutableCollection<{typeof(T).Name}>{{{string.Join(", ", _items)}}}";
    }
}

