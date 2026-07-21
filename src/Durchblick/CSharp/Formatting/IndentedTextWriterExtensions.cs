namespace Durchblick.CSharp.Formatting;

public static class IndentedTextWriterExtensions
{
    public static IDisposable WithIndentation(this IndentedTextWriter writer, string open, string close)
    {
        return new IndentationScope(writer, open, close);
    }

    private class IndentationScope : IDisposable
    {
        private readonly IndentedTextWriter _writer;
        private readonly string _close;

        public IndentationScope(IndentedTextWriter indentedTextWriter, string open, string close)
        {
            this._writer = indentedTextWriter;
            this._close = close;
            _writer.Write(open);
        }

        public void Dispose()
        {
            _writer.Level -= 1;
            _writer.Write(_close);
        }
    }
}
