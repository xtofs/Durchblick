namespace Durchblick.CSharp.Formatting;

static class EnumerableExtensions
{
    public static IEnumerable<(bool IsInitial, T Item, bool IsFinal)> Enumerate<T>(this IEnumerable<T> source)
    {
        var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            yield break;
        }

        var isInitial = true;
        var current = enumerator.Current;
        while (true)
        {
            var hasNext = enumerator.MoveNext();
            yield return (isInitial, current, !hasNext);
            if (!hasNext) break;
            isInitial = false;
            current = enumerator.Current;
        }
    }
}