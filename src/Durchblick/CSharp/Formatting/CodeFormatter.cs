namespace Durchblick.CSharp.Formatting;

using System.Runtime.CompilerServices;

public class CodeFormatter(TextWriter writer)
{
    internal TextWriter writer = writer is IndentedTextWriter ? writer : new IndentedTextWriter(writer);

    // the attribute specifies that the formatter itself is passed to the handler constructor
    public void Format([InterpolatedStringHandlerArgument("")] CodeFormattingInterpolatedStringHandler handler)
    {
        writer.Flush(); // Ensure all buffered content is written before disposing the handler
        handler.Dispose();
    }

    internal void Dispose()
    {
        writer.Dispose();
    }
}
