namespace Durchblick.CSharp.Formatting;

using System.Text;

public class IndentedTextWriter(TextWriter innerWriter) : TextWriter
{
    private readonly TextWriter _innerWriter = innerWriter;

    public int Level { get; set; } = 0;

    public string IndentString { get; set; } = "    ";

    private bool _atStartOfLine = true;

    public override Encoding Encoding => _innerWriter.Encoding;

    /// <summary>Control character (ASCII SO, Shift Out) embedded in the stream to increase indentation.</summary>
    public const char Indent = '\x0E';

    /// <summary>Control character (ASCII SI, Shift In) embedded in the stream to decrease indentation.</summary>
    public const char Dedent = '\x0F'; // aka Outdent

    public override void Write(char value)
    {
        switch (value)
        {
            case Indent:
                Level += 1;
                return;
            case Dedent:
                Level -= 1;
                return;
        }

        WriteIndentationIfNeeded();

        if (value == '\n')
        {
            _atStartOfLine = true;
        }

        _innerWriter.Write(value);
    }

    private void WriteIndentationIfNeeded()
    {
        if (_atStartOfLine)
        {
            for (var i = 0; i < Level; i++) _innerWriter.Write(IndentString);
            _atStartOfLine = false;
        }
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        var remaining = value.AsSpan();

        while (!remaining.IsEmpty)
        {
            var index = remaining.IndexOfAny('\n', Indent, Dedent);
            if (index < 0)
            {
                WriteText(remaining);
                return;
            }

            WriteText(remaining[..index]);
            Write(remaining[index]);
            remaining = remaining[(index + 1)..];
        }
    }

    private void WriteText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        WriteIndentationIfNeeded();
        _innerWriter.Write(text);
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        Write('\n');
    }
}
