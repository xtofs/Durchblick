using Durchblick.Collections;

namespace Durchblick.CSharp.Formatting;

static class CodeFormatterExtensions
{
    public static string FormatAsList<T>(this ImmutableCollection<T> items, string open, string separator, string close, bool parenthesisIfEmpty = false)
    {
        if (items.Count == 0 && !parenthesisIfEmpty)
        {
            return string.Empty;
        }

        return $"{open}{string.Join(separator, items)}{close}";
    }

    public static string FormatAsList<T>(this ImmutableCollection<T> items, Func<T, string> formatter, string open, string separator, string close, bool parenthesisIfEmpty = false)
    {
        if (items.Count == 0 && !parenthesisIfEmpty)
        {
            return string.Empty;
        }

        return $"{open}{string.Join(separator, items.Select(formatter))}{close}";
    }
}