namespace Durchblick.CSharp.Formatting;

using Durchblick.CSharp.Syntax;



public partial class CodeFormatter(TextWriter textWriter)
{
    private readonly IndentedTextWriter writer = new IndentedTextWriter(textWriter);

    public void Format(CompilationUnitDecl decl)
    {
        foreach (var ns in decl.Namespaces)
        {
            FormatNamespace(ns);
        }
    }
    public void FormatNamespace(NamespaceDecl decl)
    {
        writer.WriteLine($"namespace {decl.Name}");
        using (writer.WithIndentation("{", "}"))
        {
            foreach (var (i, member) in decl.Members.Select((m, i) => (i, m)))
            {
                if (i != 0) { writer.WriteLine(); }
                FormatType(member);
            }
        }
    }

    public void FormatType(TypeDecl type)
    {
        var keyword = type.Kind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            _ => throw new NotImplementedException($"Unknown type kind: {type.Kind}")
        };
        var typeParameterList = type.TypeParameters.FormatAsList("<", ", ", ">", false);
        writer.WriteLine($"{keyword} {type.Name}{typeParameterList}");
        using (writer.WithIndentation("{", "}"))
        {
            if (type.Kind == TypeKind.Enum)
            {
                foreach (var (i, member, f) in type.Members.Enumerate())
                {
                    FormatEnumMember(member, i, f);
                }
            }
            else
            {
                foreach (var (i, member, f) in type.Members.Enumerate())
                {
                    FormatMember(member, i, f);
                }
            }
        }
    }

    private void FormatEnumMember(MemberDecl member, bool isInitial, bool isFinal)
    {
        if (!isInitial) { writer.WriteLine(); }
        writer.WriteLine($"{member.Name}{(isFinal ? "" : ",")}");
    }

    private void FormatMember(MemberDecl member, bool isInitial, bool isFinal)
    {
        if (!isInitial) { writer.WriteLine(); }
        var mods = member.Modifiers.FormatAsList(m => m.Keyword, "", " ", " "); // separated by space and additional space if not empty
        writer.Write($"{mods}{member.TypeReference.Name} {member.Name} ");
        switch (member.Kind)
        {
            case MemberKind.Field:
                writer.WriteLine(";");
                break;
            case MemberKind.Property:
                writer.WriteLine("{ get; set; }");
                break;
            case MemberKind.Method:
                var parameters = member.Parameters.FormatAsList("(", ", ", ")", false);
                writer.WriteLine($"{member.Name}{parameters}");
                // var parameters = member.Parameters.Select(p => $"{p.TypeReference.Name}: {p.Name}");
                // writer.WriteLine($"({string.Join(", ", parameters)})");
                using (writer.WithIndentation("{", "}"))
                {
                    FormatStatement(member.Body);
                }
                break;
            default:
                throw new NotImplementedException($"Formatter not implemented for member kind {member.Kind}");
        }
    }

    private void FormatStatement(Statement? body)
    {
        switch (body)
        {
            case null:
                break;
            case BlockStatement bs:
                foreach (var statement in bs.Statements)
                {
                    FormatStatement(statement);
                }
                break;
            case ExpressionStatement es:
                // writer.Write("/* expr stmt */");
                FormatExpression(es.Expression);
                writer.WriteLine(";");
                break;

            case ReturnStatement rs:
                writer.Write($"return ");
                FormatExpression(rs.Expression);
                writer.WriteLine($";");
                break;
            case VariableDeclarationStatement vds:
                var varDecl = vds.Declaration;
                writer.Write($"{varDecl.TypeReference.Name} {varDecl.Name}");
                if (varDecl.Initializer != null) writer.Write($" = {varDecl.Initializer}");
                writer.WriteLine(";");
                break;
            case ContinueStatement cs:
                writer.WriteLine("continue;");
                break;
            case BreakStatement bs:
                writer.WriteLine("break;");
                break;
            case ThrowStatement ts:
                writer.WriteLine($"throw {ts.Expression};");
                break;

            case ForStatement fs:
                writer.Write("for (");
                if (fs.Initializer != null) writer.Write($"{fs.Initializer}; ");
                if (fs.Condition != null) writer.Write($"{fs.Condition}; ");
                if (fs.Iterator != null) writer.Write($"{fs.Iterator}");
                writer.WriteLine(")");
                using (writer.WithIndentation("{", "}"))
                {
                    FormatStatement(fs.Body);
                }
                break;

            case WhileStatement ws:
                writer.Write("while (");
                FormatExpression(ws.Condition);
                writer.WriteLine(")");
                using (writer.WithIndentation("{", "}"))
                {
                    FormatStatement(ws.Body);
                }
                break;

            case ForEachStatement fes:
                varDecl = fes.Variable;
                writer.Write($"foreach ({varDecl.TypeReference.Name} {varDecl.Name} in {fes.Collection})");
                writer.WriteLine();
                using (writer.WithIndentation("{", "}"))
                {
                    FormatStatement(fes.Body);
                }
                break;

            case SwitchStatement sws:
                writer.Write($"switch (");
                FormatExpression(sws.Expression);
                writer.Write($")");
                using (writer.WithIndentation("{", "}"))
                {
                    foreach (var section in sws.Cases)
                    {
                        if (section.Pattern is DiscardPattern)
                        {
                            writer.WriteLine($"default:");
                        }
                        else
                        {
                            writer.Write($"case ");
                            FormatPattern(section.Pattern);
                            writer.Write($":");
                        }
                        using var _ = writer.WithIndentation("", "");
                        foreach (var stmt in section.Body)
                        {
                            FormatStatement(stmt);
                        }
                    }
                }
                break;

            default:
#if DEBUG
                writer.WriteLine($"// Formatter not implemented for statement type {body.GetType().Name}");
#else
                throw new NotImplementedException($"Formatter not implemented for statement type {body.GetType().Name}");
#endif

                break;
        }
    }

    private void FormatPattern(Pattern pattern)
    {
        switch (pattern)
        {
            case DiscardPattern dp:
                writer.Write($"_");
                break;

            case ConstantPattern cp:
                FormatExpression(cp.Value);
                break;

            case TypePattern tp:
                FormatTypeReference(tp.TypeReference);
                break;
            // public static TypePattern Type(TypeReference type) => new(type);
            // public static ConstantPattern Constant(LiteralExpression value) => new(value);
            // public static DiscardPattern Discard() => new();
            // public static RelationalPattern Rel(RelationalOperator op, Expression value) => new(op, value);
            // public static LogicalPattern Logical(LogicalOperator op, Pattern left, Pattern right) => new(op, left, right);
            // public static RecursivePattern Recursive(TypeReference type, IEnumerable<PatternProperty> props)
            default:
#if DEBUG
                writer.Write($"/* Formatter not implemented for pattern type {pattern.GetType().Name} */");
#else
                throw new NotImplementedException($"Formatter not implemented for pattern type {pattern.GetType().Name}");
#endif
                break;
        }
    }

    private void FormatTypeReference(TypeReference typeReference)
    {
        // TODO: Implement full type reference formatting
        writer.Write(typeReference.Namespace);
        writer.Write(".");
        writer.Write(typeReference.Name);
        writer.Write(typeReference.GenericArguments.FormatAsList("<", ", ", ">", false));
    }

    private void FormatExpression(Expression expression)
    {
        switch (expression)
        {
            case LiteralExpression le:
                if (le.Value is string)
                    writer.Write($"\"{le.Value}\"");
                else
                    writer.Write(le.Value);
                break;
            case IdentifierExpression ie:
                writer.Write(ie.Name);
                break;

            case AssignExpression ae:
                // writer.WriteLine($"{ae.Target} = {ae.Value};");
                FormatExpression(ae.Target);
                writer.Write(" = ");
                FormatExpression(ae.Value);
                break;

            case BinaryExpression be:
                FormatExpression(be.Left);
                writer.Write(" ");
                FormatBinaryOperator(be.Operator);
                writer.Write(" ");
                FormatExpression(be.Right);
                break;

            default:
#if DEBUG
                writer.Write($"/* Formatter not implemented for expression type {expression.GetType().Name} */");
#else
                throw new NotImplementedException($"Formatter not implemented for expression type {expression.GetType().Name}");
#endif
                break;
        }
    }

    private void FormatBinaryOperator(BinaryOperator op)
    {
        switch (op)
        {
            case BinaryOperator.Add:
                writer.Write("+");
                break;
            case BinaryOperator.Subtract:
                writer.Write("-");
                break;
            case BinaryOperator.Multiply:
                writer.Write("*");
                break;
            case BinaryOperator.Divide:
                writer.Write("/");
                break;
            // case BinaryOperator.Modulo:
            //     writer.Write("%");
            //     break;
            case BinaryOperator.Equals:
                writer.Write("==");
                break;
            case BinaryOperator.NotEquals:
                writer.Write("!=");
                break;
            case BinaryOperator.And:
                writer.Write("&&");
                break;
            case BinaryOperator.Or:
                writer.Write("||");
                break;
            default:
#if DEBUG
                writer.Write($"/* Formatter not implemented for binary operator {op} */");
#else
                throw new NotImplementedException($"Formatter not implemented for binary operator {op}");
#endif
                break;
        }
    }
}
