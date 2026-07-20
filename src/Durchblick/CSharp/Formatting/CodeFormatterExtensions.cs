using Durchblick.Collections;

namespace Durchblick.CSharp.Formatting;

static class CodeFormatterExtensions
{
    public static string FormatAsList<T>(this ImmutableCollection<T> rows, string open, string separator, string close, bool parenthesisIfEmpty = false)
    {
        if (rows.Count == 0 && !parenthesisIfEmpty)
        {
            return string.Empty;
        }

        return $"{open}{string.Join(separator, rows)}{close}";
    }
}