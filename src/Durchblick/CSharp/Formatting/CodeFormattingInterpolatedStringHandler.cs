namespace Durchblick.CSharp.Formatting;

using System.Runtime.CompilerServices;

using Durchblick.Collections;
using Durchblick.CSharp.Syntax;

using SyntaxAttribute = Durchblick.CSharp.Syntax.Attribute;

[InterpolatedStringHandler]
public readonly struct CodeFormattingInterpolatedStringHandler
{

    private readonly CodeFormatter _formatter;

    public CodeFormattingInterpolatedStringHandler(int literalLength, int formattedCount, CodeFormatter formatter)
    {
        _formatter = formatter;
    }

    private readonly record struct SeparatedList<T>(ImmutableCollection<T> Items, string Separator);

    private readonly record struct DelimitedList<T>(ImmutableCollection<T> Items, string Open, string Separator, string Close, bool OmitWhenEmpty);

    /// <summary>
    /// Creates a new separated list with the specified items and separator.
    /// </summary>
    private static SeparatedList<T> Separated<T>(ImmutableCollection<T> items, string separator)
        => new(items, separator);

    /// <summary>
    /// Creates a new delimited list with the specified items, open and close delimiters, separator, and an option to omit when empty.
    /// </summary>
    private static DelimitedList<T> Delimited<T>(ImmutableCollection<T> items, string open, string separator, string close, bool omitWhenEmpty)
        => new(items, open, separator, close, omitWhenEmpty);

    private static DelimitedList<T> CurlyBraces<T>(ImmutableCollection<T> items)
    {
        // Use curly braces with indentation control characters.
        // open = newline + brace + ident + newline
        // close = newline + dedent + brace + newline
        // separator = newline
        return Delimited(items, "\n{\x0E\n", "\n", "\x0F\n}", omitWhenEmpty: false);
    }

    public readonly void AppendLiteral(string s)
    {
        _formatter.writer.Write(s);
    }

    public readonly void AppendFormatted(string? s)
    {
        _formatter.writer.Write(s);
    }

    public readonly void AppendFormatted(object? obj)
    {
        switch (obj)
        {
            case null:
                break;
            case AstNode node:
                AppendFormatted(node);
                break;
            case ExpressionOrBlock body:
                AppendFormatted(body);
                break;
            case PatternProperty property:
                AppendFormatted(property);
                break;
            case SyntaxAttribute attribute:
                AppendFormatted(attribute);
                break;
            case SwitchCase switchCase:
                AppendFormatted(switchCase);
                break;
            case CatchClause catchClause:
                AppendFormatted(catchClause);
                break;
            case AssignmentExpression assignment:
                AppendFormatted(assignment);
                break;
            case Modifier modifier:
                AppendFormatted(modifier);
                break;
            case UnaryOperator op:
                AppendFormatted(op);
                break;
            case LogicalOperator op:
                AppendFormatted(op);
                break;
            case BinaryOperator op:
                AppendFormatted(op);
                break;
            case RelationalOperator op:
                AppendFormatted(op);
                break;
            case TypeKind kind:
                AppendFormatted(kind);
                break;
            case MemberKind kind:
                AppendFormatted(kind);
                break;
            case ModifierKind kind:
                AppendFormatted(kind);
                break;
            case SymbolKind kind:
                AppendFormatted(kind);
                break;
            case PatternKind kind:
                AppendFormatted(kind);
                break;
            default:
                _formatter.writer.Write(obj.ToString());
                break;
        }
    }

    private readonly void AppendFormatted<T>(SeparatedList<T> list)
    {
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (i > 0)
            {
                _formatter.writer.Write(list.Separator);
            }

            _formatter.Format($"{list.Items[i]}");
        }
    }

    private readonly void AppendFormatted<T>(DelimitedList<T> list)
    {
        if (list.OmitWhenEmpty && list.Items.Count == 0)
        {
            return;
        }

        _formatter.writer.Write(list.Open);
        _formatter.Format($"{Separated(list.Items, list.Separator)}");
        _formatter.writer.Write(list.Close);
    }

    public readonly void AppendFormatted(DiscardPattern pattern)
    {
        _formatter.Format($"_");
    }

    public readonly void AppendFormatted(ConstantPattern pattern)
    {
        _formatter.Format($"{pattern.Value}");
    }

    public readonly void AppendFormatted(LogicalPattern pattern)
    {
        _formatter.Format($"{pattern.Left} {pattern.Operator} {pattern.Right}");
    }

    public readonly void AppendFormatted(TypePattern pattern)
    {
        _formatter.Format($"{pattern.TypeReference}");
    }

    public readonly void AppendFormatted(RelationalPattern pattern)
    {
        _formatter.Format($"{pattern.Operator} {pattern.Value}");
    }

    public readonly void AppendFormatted(RecursivePattern pattern)
    {
        _formatter.Format($"{pattern.TypeReference}");
        _formatter.Format($"{Delimited(pattern.Properties, " { ", ", ", " }", true)}");
    }

    public readonly void AppendFormatted(Pattern pattern)
    {
        switch (pattern)
        {
            case DiscardPattern dp:
                AppendFormatted(dp);
                break;
            case ConstantPattern cp:
                AppendFormatted(cp);
                break;
            case LogicalPattern lp:
                AppendFormatted(lp);
                break;
            case TypePattern tp:
                AppendFormatted(tp);
                break;
            case RelationalPattern rp:
                AppendFormatted(rp);
                break;
            case RecursivePattern rp:
                AppendFormatted(rp);
                break;
            default:
                _formatter.Format($"/* unknown pattern {pattern.GetType().Name} */");
                break;
        }
    }

    public readonly void AppendFormatted(PatternProperty property)
    {
        _formatter.Format($"{property.Name}: {property.Pattern}");
    }

    public readonly void AppendFormatted(SyntaxAttribute attribute)
    {
        _formatter.Format($"[{attribute.Type}");
        _formatter.Format($"{Delimited(attribute.Arguments, "(", ", ", ")", false)}");
        _formatter.Format($"]");
    }

    public readonly void AppendFormatted(TypeReference tref)
    {
        switch ((tref.Namespace, tref.Name))
        {
            case ("System", "Object"): _formatter.Format($"object"); break;
            case ("System", "String"): _formatter.Format($"string"); break;
            case ("System", "Int32"): _formatter.Format($"int"); break;
            case ("System", "Boolean"): _formatter.Format($"bool"); break;
            default:

                if (tref.Namespace is not null) { _formatter.Format($"{tref.Namespace}."); }
                _formatter.Format($"{tref.Name}");
                _formatter.Format($"{Delimited(tref.GenericArguments, "<", ", ", ">", true)}");
                break;
        }
    }

    public readonly void AppendFormatted(SymbolReference symbolReference)
    {
        _formatter.Format($"{symbolReference.Id}");
    }

    public readonly void AppendFormatted(Modifier modifier)
    {
        _formatter.Format($"{modifier.Keyword}");
    }

    public readonly void AppendFormatted(SwitchCase switchCase)
    {
        if (switchCase.Pattern is DiscardPattern)
        {
            _formatter.Format($"default:");
        }
        else
        {
            _formatter.Format($"case {switchCase.Pattern}:");
        }
        _formatter.Format($"{Delimited(switchCase.Body, "\n\x0E", "\n", "\x0F", true)}");
    }

    public readonly void AppendFormatted(CatchClause catchClause)
    {
        _formatter.Format($"catch ({catchClause.Type} {catchClause.Variable}) {catchClause.Body}");
    }

    public readonly void AppendFormatted(AssignmentExpression assignment)
    {
        _formatter.Format($"{assignment.Member} = {assignment.Value}");
    }

    public readonly void AppendFormatted(ExprBody body)
    {
        _formatter.Format($"{body.Value}");
    }

    public readonly void AppendFormatted(BlockBody body)
    {
        _formatter.Format($"{body.Block}");
    }

    public readonly void AppendFormatted(ExpressionOrBlock body)
    {
        switch (body)
        {
            case ExprBody eb:
                AppendFormatted(eb);
                break;
            case BlockBody bb:
                AppendFormatted(bb);
                break;
            default:
                _formatter.Format($"/* unknown expression body {body.GetType().Name} */");
                break;
        }
    }

    public readonly void AppendFormatted(UnaryOperator unaryOperator)
    {
        var op = unaryOperator switch { UnaryOperator.Negate => "-", UnaryOperator.Not => "!", UnaryOperator.Increment => "++", UnaryOperator.Decrement => "--", _ => "??" };
        _formatter.Format($"{op}");
    }

    public readonly void AppendFormatted(LogicalOperator logicalOperator)
    {
        var op = logicalOperator switch { LogicalOperator.And => "and", LogicalOperator.Or => "or", _ => "??" };
        _formatter.Format($"{op}");
    }

    public readonly void AppendFormatted(BinaryOperator binaryOperator)
    {
        var op = binaryOperator switch { BinaryOperator.Add => "+", BinaryOperator.Subtract => "-", BinaryOperator.Multiply => "*", BinaryOperator.Divide => "/", BinaryOperator.And => "&", BinaryOperator.Or => "|", BinaryOperator.Equals => "==", BinaryOperator.NotEquals => "!=", BinaryOperator.Less => "<", BinaryOperator.Greater => ">", _ => "??" };
        _formatter.Format($"{op}");
    }

    public readonly void AppendFormatted(RelationalOperator relationalOperator)
    {
        var op = relationalOperator switch { RelationalOperator.Less => "<", RelationalOperator.LessOrEqual => "<=", RelationalOperator.Greater => ">", RelationalOperator.GreaterOrEqual => ">=", _ => "??" };
        _formatter.Format($"{op}");
    }

    public readonly void AppendFormatted(TypeKind typeKind)
    {
        _formatter.Format($"{typeKind.ToString().ToLowerInvariant()}");
    }

    public readonly void AppendFormatted(MemberKind memberKind)
    {
        _formatter.Format($"{memberKind.ToString().ToLowerInvariant()}");
    }

    public readonly void AppendFormatted(ModifierKind modifierKind)
    {
        _formatter.Format($"{modifierKind.ToString().ToLowerInvariant()}");
    }

    public readonly void AppendFormatted(SymbolKind symbolKind)
    {
        _formatter.Format($"{symbolKind.ToString().ToLowerInvariant()}");
    }

    public readonly void AppendFormatted(PatternKind patternKind)
    {
        _formatter.Format($"{patternKind.ToString().ToLowerInvariant()}");
    }

    public readonly void AppendFormatted(LiteralExpression expression)
    {
        switch (expression.Value)
        {
            case string s:
                _formatter.Format($"\"{s}\"");
                break;
            case bool b:
                _formatter.Format($"{(b ? "true" : "false")}");
                break;
            default:
                _formatter.Format($"{expression.Value.ToString()}");
                break;
        }
    }

    public readonly void AppendFormatted(IdentifierExpression expression)
    {
        _formatter.Format($"{expression.Name}");
    }

    public readonly void AppendFormatted(UnaryExpression expression)
    {
        _formatter.Format($"{expression.Operator}{expression.Operand}");
    }

    public readonly void AppendFormatted(BinaryExpression expression)
    {
        _formatter.Format($"{expression.Left} {expression.Operator} {expression.Right}");
    }

    public readonly void AppendFormatted(ConditionalExpression expression)
    {
        _formatter.Format($"{expression.Condition} ? {expression.Then} : {expression.Else}");
    }

    public readonly void AppendFormatted(CallExpression expression)
    {
        _formatter.Format($"{expression.Target}");
        _formatter.Format($"{Delimited(expression.Arguments, "(", ", ", ")", omitWhenEmpty: false)}");
    }

    public readonly void AppendFormatted(MemberAccessExpression expression)
    {
        _formatter.Format($"{expression.Target}.{expression.MemberName}");
    }

    public readonly void AppendFormatted(IndexAccessExpression expression)
    {
        _formatter.Format($"{expression.Target}");
        _formatter.Format($"{Delimited(expression.Indices, "[", ", ", "]", omitWhenEmpty: false)}");
    }

    public readonly void AppendFormatted(ObjectCreationExpression expression)
    {
        _formatter.Format($"new {expression.Type}");
        _formatter.Format($"{Delimited(expression.Arguments, "(", ", ", ")", omitWhenEmpty: false)}");
        if (expression.Initializer.Count > 0)
        {
            _formatter.Format($"{Delimited(expression.Initializer, " { ", ", ", " }", false)}");
        }
    }

    public readonly void AppendFormatted(LambdaExpression expression)
    {
        _formatter.Format($"{Delimited(expression.Parameters, "(", ", ", ")", omitWhenEmpty: false)}");
        _formatter.Format($" => {expression.Body}");
    }

    public readonly void AppendFormatted(TupleExpression expression)
    {
        _formatter.Format($"{Delimited(expression.Elements, "(", ",", ")", omitWhenEmpty: false)}");
    }

    public readonly void AppendFormatted(CastExpression expression)
    {
        _formatter.Format($"({expression.Type}){expression.Expression}");
    }

    public readonly void AppendFormatted(AwaitExpression expression)
    {
        _formatter.Format($"await {expression.Expression}");
    }

    public readonly void AppendFormatted(Parameter expression)
    {
        _formatter.Format($"{expression.Type} {expression.Name}");
    }

    public readonly void AppendFormatted(AssignExpression expression)
    {
        _formatter.Format($"{expression.Target} = {expression.Value}");
    }

    public readonly void AppendFormatted(Expression expr)
    {
        switch (expr)
        {
            case LiteralExpression le:
                AppendFormatted(le);
                break;
            case IdentifierExpression ie:
                AppendFormatted(ie);
                break;
            case UnaryExpression ue:
                AppendFormatted(ue);
                break;
            case BinaryExpression be:
                AppendFormatted(be);
                break;
            case ConditionalExpression ce:
                AppendFormatted(ce);
                break;
            case CallExpression ce:
                AppendFormatted(ce);
                break;
            case MemberAccessExpression mae:
                AppendFormatted(mae);
                break;
            case IndexAccessExpression iae:
                AppendFormatted(iae);
                break;
            case ObjectCreationExpression oce:
                AppendFormatted(oce);
                break;
            case LambdaExpression le:
                AppendFormatted(le);
                break;
            case TupleExpression te:
                AppendFormatted(te);
                break;
            case CastExpression ce:
                AppendFormatted(ce);
                break;
            case AwaitExpression ae:
                AppendFormatted(ae);
                break;
            case Parameter p:
                AppendFormatted(p);
                break;
            case AssignExpression ae:
                AppendFormatted(ae);
                break;
            default:
                _formatter.Format($"/* unknown expression {expr.GetType().Name} */");
                break;
        }
    }

    public readonly void AppendFormatted(BlockStatement statement)
    {
        _formatter.Format($"{CurlyBraces(statement.Statements)}");
    }

    public readonly void AppendFormatted(ExpressionStatement statement)
    {
        _formatter.Format($"{statement.Expression};");
    }

    public readonly void AppendFormatted(ReturnStatement statement)
    {
        _formatter.Format($"return {statement.Expression};");
    }

    public readonly void AppendFormatted(IfStatement statement)
    {
        _formatter.Format($"if ({statement.Condition}) {statement.Then}");
        if (statement.Else is not null)
        {
            _formatter.Format($" else {statement.Else}");
        }
    }

    public readonly void AppendFormatted(WhileStatement statement)
    {
        _formatter.Format($"while ({statement.Condition}) {statement.Body}");
    }

    public readonly void AppendFormatted(ForStatement statement)
    {
        _formatter.writer.Write("for (");
        _formatter.Format($"{Separated(statement.Initializer, " ")}");
        _formatter.writer.Write(" ");
        if (statement.Condition is not null)
        {
            _formatter.Format($"{statement.Condition}");
        }

        _formatter.writer.Write("; ");
        _formatter.Format($"{Separated(statement.Iterator, " ")}");
        _formatter.Format($") {statement.Body}");
    }

    public readonly void AppendFormatted(ForEachStatement statement)
    {
        _formatter.Format($"foreach ({statement.Variable} in {statement.Collection}) {statement.Body}");
    }

    public readonly void AppendFormatted(SwitchStatement statement)
    {
        _formatter.Format($"switch ({statement.Expression}) ");
        _formatter.Format($"{CurlyBraces(statement.Cases)}");
    }

    public readonly void AppendFormatted(TryStatement statement)
    {
        _formatter.Format($"try {statement.Body}");
        _formatter.Format($"{Delimited(statement.Catches, " ", " ", "", false)}");
        if (statement.Finally is not null)
        {
            _formatter.Format($" finally {statement.Finally}");
        }
    }

    public readonly void AppendFormatted(ThrowStatement statement)
    {
        _formatter.Format($"throw {statement.Expression};");
    }

    public readonly void AppendFormatted(BreakStatement statement)
    {
        _formatter.Format($"break;");
    }

    public readonly void AppendFormatted(ContinueStatement statement)
    {
        _formatter.Format($"continue;");
    }

    public readonly void AppendFormatted(VariableDeclarationStatement statement)
    {
        _formatter.Format($"{statement.Declaration};");
    }

    public readonly void AppendFormatted(Statement statement)
    {
        switch (statement)
        {
            case BlockStatement bs:
                AppendFormatted(bs);
                break;
            case ExpressionStatement es:
                AppendFormatted(es);
                break;
            case ReturnStatement rs:
                AppendFormatted(rs);
                break;
            case IfStatement @is:
                AppendFormatted(@is);
                break;
            case WhileStatement ws:
                AppendFormatted(ws);
                break;
            case ForStatement fs:
                AppendFormatted(fs);
                break;
            case ForEachStatement fes:
                AppendFormatted(fes);
                break;
            case SwitchStatement ss:
                AppendFormatted(ss);
                break;
            case TryStatement ts:
                AppendFormatted(ts);
                break;
            case ThrowStatement ts:
                AppendFormatted(ts);
                break;
            case BreakStatement bs:
                AppendFormatted(bs);
                break;
            case ContinueStatement cs:
                AppendFormatted(cs);
                break;
            case VariableDeclarationStatement vds:
                AppendFormatted(vds);
                break;
            default:
                _formatter.Format($"/* unknown statement {statement.GetType().Name} */");
                break;
        }
    }

    public readonly void AppendFormatted(CompilationUnitDecl declaration)
    {
        _formatter.Format($"{Separated(declaration.Namespaces, " ")}");
    }

    public readonly void AppendFormatted(NamespaceDecl declaration)
    {
        _formatter.Format($"namespace {declaration.Name} ");
        _formatter.Format($"{CurlyBraces(declaration.Members)}");
    }

    public readonly void AppendFormatted(TypeDecl declaration)
    {
        _formatter.Format($"{Separated(declaration.Attributes, " ")}");
        if (declaration.Attributes.Count > 0)
        {
            _formatter.writer.Write(" ");
        }

        _formatter.Format($"{Separated(declaration.Modifiers, " ")}");
        if (declaration.Modifiers.Count > 0)
        {
            _formatter.writer.Write(" ");
        }

        _formatter.Format($"{declaration.Kind} {declaration.Name}");
        _formatter.Format($"{Delimited(declaration.TypeParameters, "<", ", ", ">", true)}");
        if (declaration.BaseTypes.Count > 0)
        {
            _formatter.Format($"{Delimited(declaration.BaseTypes, " : ", ", ", "", true)}");
        }

        _formatter.Format($"{CurlyBraces(declaration.Members)}");
    }
    //"\n{", " ", "}\n",
    public readonly void AppendFormatted(MemberDecl declaration)
    {
        _formatter.Format($"{Separated(declaration.Attributes, " ")}");
        if (declaration.Attributes.Count > 0)
        {
            _formatter.writer.Write(" ");
        }

        _formatter.Format($"{Separated(declaration.Modifiers, " ")}");
        if (declaration.Modifiers.Count > 0)
        {
            _formatter.writer.Write(" ");
        }

        switch (declaration.Kind)
        {
            case MemberKind.Field:
                _formatter.Format($"{declaration.TypeReference} {declaration.Name};");
                break;
            case MemberKind.Property:
                _formatter.Format($"{declaration.TypeReference} {declaration.Name} {{ get; set; }}");
                break;
            case MemberKind.Method:
                _formatter.Format($"{declaration.TypeReference} {declaration.Name}");
                _formatter.Format($"{Delimited(declaration.Parameters, "(", ", ", ")", omitWhenEmpty: false)}");
                if (declaration.Body is not null)
                {
                    _formatter.Format($" {declaration.Body}");
                }
                else
                {
                    _formatter.writer.Write(";");
                }
                break;
            case MemberKind.Constructor:
                _formatter.Format($"{declaration.Name}");
                _formatter.Format($"{Delimited(declaration.Parameters, "(", ", ", ")", omitWhenEmpty: false)}");
                if (declaration.Body is not null)
                {
                    _formatter.Format($" {declaration.Body}");
                }
                else
                {
                    _formatter.writer.Write(";");
                }
                break;
            case MemberKind.Event:
                _formatter.Format($"event {declaration.TypeReference} {declaration.Name};");
                break;
            default:
                _formatter.Format($"{declaration.TypeReference} {declaration.Name};");
                break;
        }
    }

    public readonly void AppendFormatted(VariableDecl declaration)
    {
        _formatter.Format($"{declaration.TypeReference} {declaration.Name}");
        if (declaration.Initializer is not null)
        {
            _formatter.Format($" = {declaration.Initializer}");
        }
    }

    public readonly void AppendFormatted(ParameterDecl declaration)
    {
        _formatter.Format($"{declaration.TypeReference} {declaration.Name}");
    }

    public readonly void AppendFormatted(TypeParameterDecl declaration)
    {
        _formatter.Format($"{declaration.Name}");
    }

    public readonly void AppendFormatted(AttributeDecl declaration)
    {
        _formatter.Format($"[{declaration.TypeReference}");
        _formatter.Format($"{Delimited(declaration.Arguments, "(", ", ", ")", false)}");
        _formatter.Format($"]");
    }

    public readonly void AppendFormatted(Declaration declaration)
    {
        switch (declaration)
        {
            case CompilationUnitDecl cud:
                AppendFormatted(cud);
                break;
            case NamespaceDecl nd:
                AppendFormatted(nd);
                break;
            case TypeDecl td:
                AppendFormatted(td);
                break;
            case MemberDecl md:
                AppendFormatted(md);
                break;
            case VariableDecl vd:
                AppendFormatted(vd);
                break;
            case ParameterDecl pd:
                AppendFormatted(pd);
                break;
            case TypeParameterDecl tpd:
                AppendFormatted(tpd);
                break;
            case AttributeDecl ad:
                AppendFormatted(ad);
                break;
            default:
                _formatter.Format($"/* unknown declaration {declaration.GetType().Name} */");
                break;
        }
    }

    public readonly void AppendFormatted(AstNode node)
    {
        switch (node)
        {
            case Pattern p:
                AppendFormatted(p);
                break;
            case Expression e:
                AppendFormatted(e);
                break;
            case Statement s:
                AppendFormatted(s);
                break;
            case Declaration d:
                AppendFormatted(d);
                break;
            case TypeReference tr:
                AppendFormatted(tr);
                break;
            default:
                _formatter.Format($"/* unknown syntax node {node.GetType().Name} */");
                break;
        }
    }

    internal readonly void Dispose()
    {
        _formatter.Dispose();
    }
}
