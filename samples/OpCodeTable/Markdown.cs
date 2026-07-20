static class Markdown
{
    public static void FormatAsTable(string[] header, IEnumerable<object[]> objs)
    {
        var rows = objs.Select(row => row.Select(cell => cell.ToString()!).ToArray()).ToList();
        var cols = rows.Take(20).Max(row => row.Length);
        var widths = rows.Aggregate(new int[cols], (acc, row) =>
        {
            for (int i = 0; i < cols; i++)
            {
                acc[i] = Math.Max(acc[i], row[i].Length);
            }
            return acc;
        });

        // header
        for (int i = 0; i < cols; i++)
        {
            if (i > 0) Console.Write(" | ");
            Console.Write(header[i].PadRight(widths[i] + 1));
        }
        Console.WriteLine();

        // separator
        for (int i = 0; i < cols; i++)
        {
            if (i > 0) Console.Write(" | ");
            Console.Write(new string('-', widths[i] + 1));
        }
        Console.WriteLine();

        // rows
        foreach (var row in rows)
        {
            for (int i = 0; i < cols; i++)
            {
                if (i > 0) Console.Write(" | ");
                Console.Write(row[i].PadRight(widths[i] + 1));
            }
            Console.WriteLine();
        }
    }
}
