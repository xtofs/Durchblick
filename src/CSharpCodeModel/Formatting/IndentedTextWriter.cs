namespace CSharpCodeModel;

using System.Text;



public partial class CodeFormatter
{
    // public void Format(Declaration node, Declaration? parent = null)
    // {
    //     switch (node)
    //     {
    //         case CompilationUnitDecl decl: this.Format(decl); break;
    //         case NamespaceDecl decl: this.Format(decl); break;
    //         case TypeDecl decl: this.Format(decl); break;
    //         case MemberDecl decl: this.Format(decl); break;
    //         case TypeParameterDecl decl: this.Format(decl); break;
    //         case ParameterDecl decl: this.Format(decl); break;
    //         case VariableDecl decl: this.Format(decl); break;
    //         default: throw new NotImplementedException($"Formatter not implemented for {node?.GetType().Name ?? "null"}");
    //     }
    // }

    //     private void Format(VariableDecl decl)
    //     {
    //         throw new NotImplementedException();
    //     }


    //     private void Format(ParameterDecl decl)
    //     {
    //         throw new NotImplementedException();
    //     }

    //     private void Format(TypeParameterDecl decl)
    //     {
    //         throw new NotImplementedException();
    //     }

    //     private void Format(MemberDecl decl, Declaration? parent)
    //     {
    //         switch (decl.Kind)
    //         {
    //             case MemberKind.Field:
    //                 writer.WriteLine($"field {decl.Name} : {decl.TypeReference.Name}");
    //                 break;
    //             case MemberKind.Property:
    //                 writer.WriteLine($"public {decl.TypeReference.Name} {decl.Name} {{ get; set; }}");
    //                 break;
    //             case MemberKind.Method:
    //                 var parameterList = string.Join(", ", decl.Parameters.Select(p => $"{p.TypeReference.Name} {p.Name}"));
    //                 writer.WriteLine($"public {decl.TypeReference.Name} {decl.Name} ({parameterList}) {{ /* ... */ }}");
    //                 break;
    //             default:
    //                 throw new NotImplementedException($"Formatter not implemented for member kind {decl.Kind}");
    //         }
    //     }

    //     private void Format(TypeDecl decl)
    //     {
    //         var keyword = decl.Kind switch
    //         {
    //             TypeKind.Class => "class",
    //             TypeKind.Struct => "struct",
    //             TypeKind.Interface => "interface",
    //             TypeKind.Enum => "enum",
    //             _ => throw new NotImplementedException($"Unknown type kind: {decl.Kind}")
    //         };
    //         {
    //             var parameterList = decl.TypeParameters.Count > 0
    //                 ? $"<{string.Join(", ", decl.TypeParameters.Select(tp => tp.Name))}>"
    //                 : string.Empty;
    //             writer.WriteLine($"{keyword} {decl.Name}{parameterList}");

    //             if (decl.BaseTypes.Count > 0)
    //             {
    //                 writer.Indent("BaseTypes:");
    //                 foreach (var baseType in decl.BaseTypes)
    //                 {
    //                     this.Format(baseType);
    //                 }
    //                 writer.Dedent("EndBaseTypes");
    //             }
    //             if (decl.Members.Count == 0)
    //             {
    //                 writer.WriteLine("{ }");
    //             }
    //             else
    //             {
    //                 writer.Indent("{");
    //                 foreach (var member in decl.Members)
    //                 {
    //                     this.Format((Declaration)member, decl);
    //                 }
    //                 writer.Dedent("}");
    //             }
    //         }
    //     }

    //     private void Format(NamespaceDecl decl)
    //     {
    //         writer.WriteLine($"namespace {decl.Name}");
    //         using (writer.WithIndentation("{", "}"))
    //         {
    //             foreach (var member in decl.Members)
    //             {
    //                 Format((Declaration)member);
    //             }
    //         }
    //     }

    //     private void Format(CompilationUnitDecl decl)
    //     {
    //         foreach (var ns in decl.Namespaces)
    //         {
    //             Format((Declaration)ns);
    //         }
    //     }
    // }

    public class IndentedTextWriter(TextWriter innerWriter) : TextWriter
    {
        private readonly TextWriter _innerWriter = innerWriter;
        private int _indentLevel;
        private bool _atStartOfLine = true;

        public override Encoding Encoding => _innerWriter.Encoding;

        public string IndentString { get; private set; } = "    ";

        public void Indent() => _indentLevel++;

        public void Dedent() => _indentLevel--;

        public void Indent(string open)
        {
            Write(open); Write('\n'); _indentLevel++;
        }

        public void Dedent(string close)
        {
            _indentLevel--; Write(close); Write('\n');
        }


        public override void Write(char value)
        {
            WriteIndentationIfNeeded();

            _innerWriter.Write(value);

            if (value == '\n')
            {
                _atStartOfLine = true;
            }
        }

        void WriteIndentationIfNeeded()
        {
            if (_atStartOfLine)
            {
                for (var i = 0; i < _indentLevel; i++) _innerWriter.Write(IndentString);
                _atStartOfLine = false;
            }
        }

        public override void Write(string? value)
        {
            if (value == null)
            {
                return;
            }
            var first = true;
            foreach (var line in value.EnumerateLines())
            {
                if (!first) { Write('\n'); } else { first = false; }
                WriteIndentationIfNeeded();
                _innerWriter.Write(line);
            }
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        internal IDisposable WithIndentation(string v1, string v2)
        {
            return new IndentationScope(this, v1, v2);
        }

        private class IndentationScope : IDisposable
        {
            private readonly IndentedTextWriter writer;
            private readonly string close;

            public IndentationScope(IndentedTextWriter indentedTextWriter, string open, string close)
            {
                this.writer = indentedTextWriter;
                this.close = close;
                writer.Indent(open);
            }

            public void Dispose()
            {
                writer.Dedent(close);
            }
        }
    }
}