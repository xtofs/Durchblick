
namespace Durchblick.CSharp.Semantics;

using System.Collections.Immutable;
using Durchblick.Collections;
using Durchblick.CSharp.Syntax;

/// <summary>
/// Performs phase 2 of the binder pipeline by building semantic information on top of the symbols.
/// </summary>
/// <remarks>
/// This phase assumes phase 1 has already populated the symbol table with declared namespaces and types.
/// It then walks the syntax tree again and attaches expression, statement, and pattern semantics.
/// </remarks>
internal sealed class SemanticModelBuilder
{
    private readonly SymbolTable _symbols;
    private readonly TypeResolver _types;

    /// <summary>
    /// Creates a semantic-model builder over the symbol table produced by phase 1.
    /// </summary>
    public SemanticModelBuilder(SymbolTable symbols)
    {
        _symbols = symbols;
        _types = new TypeResolver(symbols);
    }

    /// <summary>
    /// Builds the semantic model for the supplied bound compilation.
    /// </summary>
    public SemanticModel Build(BoundCompilation boundCompilation)
    {
        var exprInfos = ImmutableDictionary.CreateBuilder<Expression, ExpressionInfo>();
        var stmtInfos = ImmutableDictionary.CreateBuilder<Statement, StatementInfo>();
        var patternInfos = ImmutableDictionary.CreateBuilder<Pattern, PatternInfo>();
        var diagnostics = ImmutableArray.CreateBuilder<string>();
        diagnostics.AddRange(boundCompilation.Diagnostics);

        var globalScope = new Scope(null, _symbols.Types.Cast<Symbol>().ToImmutableArray());

        BindNode(boundCompilation.Compilation, globalScope, exprInfos, stmtInfos, patternInfos, diagnostics);

        return new SemanticModel(
            Symbols: _symbols,
            ExpressionInfos: exprInfos.ToImmutable(),
            StatementInfos: stmtInfos.ToImmutable(),
            PatternInfos: patternInfos.ToImmutable(),
            Diagnostics: diagnostics.ToImmutable()
        );
    }

    private void BindNode(
        AstNode node,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics
    )
    {
        switch (node)
        {
            case Expression expr:
                var info = BindExpression(expr, scope, exprInfos, diagnostics);
                exprInfos[expr] = info;
                break;

            case Statement stmt:
                var sInfo = BindStatement(stmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                stmtInfos[stmt] = sInfo;
                break;

            case Pattern pat:
                var pInfo = BindPattern(pat, scope, exprInfos, patternInfos, diagnostics);
                patternInfos[pat] = pInfo;
                break;

            case Declaration decl:
                BindDeclaration(decl, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                break;

            default:
                break;
        }
    }

    private ExpressionInfo BindExpression(
        Expression expr,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        return expr switch
        {
            IdentifierExpression id =>
                BindIdentifier(id, scope),

            LiteralExpression lit =>
                BindLiteral(lit),

            BinaryExpression bin =>
                BindBinary(bin, scope, exprInfos, diagnostics),

            UnaryExpression un =>
                BindUnary(un, scope, exprInfos, diagnostics),

            CastExpression cast =>
                BindCast(cast, scope, exprInfos, diagnostics),

            IsInstanceExpression isInstance =>
                BindIsInstance(isInstance, scope, exprInfos, diagnostics),

            CallExpression call =>
                BindCall(call, scope, exprInfos, diagnostics),

            MemberAccessExpression ma =>
                BindMemberAccess(ma, scope, exprInfos, diagnostics),

            ObjectCreationExpression oc =>
                BindObjectCreation(oc, scope, exprInfos, diagnostics),

            ConditionalExpression cond =>
                BindConditional(cond, scope, exprInfos, diagnostics),

            LambdaExpression lambda =>
                BindLambda(lambda, scope, exprInfos, diagnostics),

            AwaitExpression await =>
                BindAwait(await, scope, exprInfos, diagnostics),

            IndexAccessExpression indexAccess =>
                BindIndexAccess(indexAccess, scope, exprInfos, diagnostics),

            TupleExpression tuple =>
                BindTuple(tuple, scope, exprInfos, diagnostics),

            _ => new UnknownExpressionInfo(expr)
        };
    }
    private StatementInfo BindStatement(
        Statement stmt,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        switch (stmt)
        {
            case BlockStatement block:
                return BindBlock(block, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case ExpressionStatement es:
                {
                    var expressionInfo = BindExpression(es.Expression, scope, exprInfos, diagnostics);
                    exprInfos[es.Expression] = expressionInfo;
                    return new ExpressionStatementInfo(es, expressionInfo);
                }

            case VariableDeclarationStatement vds:
                return BindVariableDecl(vds, scope, exprInfos, diagnostics);

            case IfStatement ifs:
                return BindIf(ifs, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case ForEachStatement fes:
                return BindForEach(fes, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case ReturnStatement ret:
                return BindReturn(ret, scope, exprInfos, diagnostics);

            case WhileStatement whileStmt:
                return BindWhile(whileStmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case ForStatement forStmt:
                return BindFor(forStmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case SwitchStatement switchStmt:
                return BindSwitch(switchStmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case TryStatement tryStmt:
                return BindTry(tryStmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case ThrowStatement throwStmt:
                return BindThrow(throwStmt, scope, exprInfos, diagnostics);

            case BreakStatement breakStmt:
                return BindUnsupportedStatement(breakStmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            case ContinueStatement continueStmt:
                return BindUnsupportedStatement(continueStmt, scope, exprInfos, stmtInfos, patternInfos, diagnostics);

            default:
                return new UnknownStatementInfo(stmt);
        }
    }

    private PatternInfo BindPattern(
        Pattern pat,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        switch (pat)
        {
            case TypePattern tp:
                return new TypePatternInfo(tp, _types!.Resolve(tp.TypeReference));

            case ConstantPattern cp:
                {
                    var expressionInfo = BindExpression(cp.Value, scope, exprInfos, diagnostics);
                    exprInfos[cp.Value] = expressionInfo;
                    return new ConstantPatternInfo(cp, expressionInfo);
                }

            case RelationalPattern rp:
                {
                    var expressionInfo = BindExpression(rp.Value, scope, exprInfos, diagnostics);
                    exprInfos[rp.Value] = expressionInfo;
                    return new RelationalPatternInfo(rp, expressionInfo);
                }

            case LogicalPattern lp:
                {
                    var leftInfo = BindPattern(lp.Left, scope, exprInfos, patternInfos, diagnostics);
                    var rightInfo = BindPattern(lp.Right, scope, exprInfos, patternInfos, diagnostics);
                    patternInfos[lp.Left] = leftInfo;
                    patternInfos[lp.Right] = rightInfo;
                    return new LogicalPatternInfo(lp, new[] { leftInfo, rightInfo }.ToImmutableCollection());
                }

            case RecursivePattern rec:
                return BindRecursivePattern(rec, scope, exprInfos, patternInfos, diagnostics);

            default:
                return new UnknownPatternInfo(pat);
        }
    }
    private void BindDeclaration(
        Declaration decl,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        switch (decl)
        {
            case CompilationUnitDecl compilationUnit:
                foreach (var namespaceDecl in compilationUnit.Namespaces)
                {
                    BindNode(namespaceDecl, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                }

                break;

            case NamespaceDecl namespaceDecl:
                foreach (var typeDecl in namespaceDecl.Members)
                {
                    BindNode(typeDecl, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                }

                break;

            case TypeDecl typeDecl:
                var typeScope = new Scope(scope);
                var declaredType = scope.Lookup(typeDecl.Name) as TypeSymbol ?? _types!.Resolve(new TypeReference(typeDecl.Name, null, []));

                foreach (var memberDecl in typeDecl.Members)
                {
                    var memberSymbol = CreateMemberSymbol(memberDecl, declaredType);
                    if (memberSymbol is not null)
                    {
                        typeScope.Add(memberSymbol);
                    }
                }

                foreach (var memberDecl in typeDecl.Members)
                {
                    BindNode(memberDecl, typeScope, exprInfos, stmtInfos, patternInfos, diagnostics);
                }

                break;

            case MemberDecl member:
                var memberScope = new Scope(scope);
                foreach (var parameter in member.Parameters)
                {
                    var parameterType = _types!.Resolve(parameter.TypeReference);
                    memberScope.Add(new ParameterSymbol(parameter, parameterType));
                }

                if (member.Body is not null)
                {
                    BindNode(member.Body, memberScope, exprInfos, stmtInfos, patternInfos, diagnostics);
                }

                foreach (var accessor in member.Accessors)
                {
                    BindNode(accessor.Body, memberScope, exprInfos, stmtInfos, patternInfos, diagnostics);
                }
                break;

            default:
                break;
        }
    }
    private IdentifierInfo BindIdentifier(IdentifierExpression id, Scope scope)
    {
        var symbol = scope.Lookup(id.Name);

        TypeSymbol? type = symbol switch
        {
            LocalSymbol l => l.Type,
            ParameterSymbol p => p.Type,
            FieldSymbol f => f.Type,
            PropertySymbol pr => pr.Type,
            MethodSymbol m => m.ReturnType,
            _ => null
        };

        return new IdentifierInfo(id, symbol, type);
    }
    private LiteralInfo BindLiteral(LiteralExpression lit)
    {
        var type = InferLiteralType(lit);
        return new LiteralInfo(lit, type);
    }
    private BinaryInfo BindBinary(
        BinaryExpression bin,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var left = BindExpression(bin.Left, scope, exprInfos, diagnostics);
        var right = BindExpression(bin.Right, scope, exprInfos, diagnostics);
        exprInfos[bin.Left] = left;
        exprInfos[bin.Right] = right;

        var leftType = GetExpressionType(left);
        var rightType = GetExpressionType(right);

        // TODO: operator resolution
        var resultType = leftType ?? rightType ?? new ErrorTypeSymbol("Unknown binary result type");

        return new BinaryInfo(bin, leftType!, rightType!, null, resultType);
    }
    private UnaryInfo BindUnary(
        UnaryExpression un,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var operand = BindExpression(un.Operand, scope, exprInfos, diagnostics);
        exprInfos[un.Operand] = operand;
        var operandType = GetExpressionType(operand);

        // TODO: operator resolution
        return new UnaryInfo(un, operandType!, null, operandType!);
    }
    private CastInfo BindCast(
        CastExpression cast,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var exprInfo = BindExpression(cast.Expression, scope, exprInfos, diagnostics);
        exprInfos[cast.Expression] = exprInfo;
        var sourceType = GetExpressionType(exprInfo);
        var targetType = _types!.Resolve(cast.Type);

        return new CastInfo(cast, targetType, sourceType!);
    }

    private IsInstanceInfo BindIsInstance(
        IsInstanceExpression isInstance,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var exprInfo = BindExpression(isInstance.Expression, scope, exprInfos, diagnostics);
        exprInfos[isInstance.Expression] = exprInfo;
        var sourceType = GetExpressionType(exprInfo);
        var targetType = _types!.Resolve(isInstance.Type);

        return new IsInstanceInfo(isInstance, targetType, sourceType!);
    }
    private CallInfo BindCall(
        CallExpression call,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var targetInfo = BindExpression(call.Target, scope, exprInfos, diagnostics);
        exprInfos[call.Target] = targetInfo;

        var argInfos = call.Arguments
            .Select(a =>
            {
                var argumentInfo = BindExpression(a, scope, exprInfos, diagnostics);
                exprInfos[a] = argumentInfo;
                return argumentInfo;
            })
            .ToImmutableArray();

        ImmutableCollection<TypeSymbol> argTypes = argInfos
            .Select(argumentInfo => GetExpressionType(argumentInfo) ?? new ErrorTypeSymbol("Unknown argument type"))
            .ToImmutableCollection();

        // TODO: overload resolution
        var returnType = targetInfo switch
        {
            IdentifierInfo { Symbol: MethodSymbol methodSymbol } => methodSymbol.ReturnType,
            _ => null
        };

        return new CallInfo(call, null, returnType, argTypes);
    }
    private MemberAccessInfo BindMemberAccess(
        MemberAccessExpression ma,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        _ = BindExpression(ma.Target, scope, exprInfos, diagnostics);
        exprInfos[ma.Target] = BindExpression(ma.Target, scope, exprInfos, diagnostics);

        // TODO: resolve member on target type
        return new MemberAccessInfo(ma, null, null);
    }
    private ObjectCreationInfo BindObjectCreation(
        ObjectCreationExpression oc,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var type = _types!.Resolve(oc.Type);

        var argInfos = oc.Arguments
            .Select(a =>
            {
                var argumentInfo = BindExpression(a, scope, exprInfos, diagnostics);
                exprInfos[a] = argumentInfo;
                return argumentInfo;
            })
            .ToImmutableArray();

        foreach (var initializer in oc.Initializer)
        {
            var initializerInfo = BindExpression(initializer.Value, scope, exprInfos, diagnostics);
            exprInfos[initializer.Value] = initializerInfo;
        }

        var argTypes = argInfos
            .Select(GetExpressionType)
            .Select(type => type ?? new ErrorTypeSymbol("Unknown object creation argument type"))
            .ToImmutableCollection();

        return new ObjectCreationInfo(oc, type, argTypes);
    }
    private ConditionalInfo BindConditional(
        ConditionalExpression cond,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var condition = BindExpression(cond.Condition, scope, exprInfos, diagnostics);
        var thenExpr = BindExpression(cond.Then, scope, exprInfos, diagnostics);
        var elseExpr = BindExpression(cond.Else, scope, exprInfos, diagnostics);
        exprInfos[cond.Condition] = condition;
        exprInfos[cond.Then] = thenExpr;
        exprInfos[cond.Else] = elseExpr;

        var condType = GetExpressionType(condition);
        var thenType = GetExpressionType(thenExpr);
        var elseType = GetExpressionType(elseExpr);

        // TODO: type unification
        var resultType = thenType ?? elseType ?? new ErrorTypeSymbol("Unknown conditional result type");

        return new ConditionalInfo(cond, condType!, thenType!, elseType!, resultType);
    }
    private LambdaInfo BindLambda(
        LambdaExpression lambda,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var lambdaScope = new Scope(scope);

        foreach (var parameter in lambda.Parameters)
        {
            var parameterType = _types!.Resolve(parameter.Type);
            lambdaScope.Add(new ParameterSymbol(new ParameterDecl(parameter.Name, parameter.Type, []), parameterType));
        }

        TypeSymbol delegateType;
        switch (lambda.Body)
        {
            case ExprBody exprBody:
                {
                    var bodyInfo = BindExpression(exprBody.Value, lambdaScope, exprInfos, diagnostics);
                    exprInfos[exprBody.Value] = bodyInfo;
                    delegateType = GetExpressionType(bodyInfo) ?? new ErrorTypeSymbol("Lambda body type unknown");
                    break;
                }

            case BlockBody blockBody:
                BindBlock(blockBody.Block, lambdaScope, exprInfos, ImmutableDictionary.CreateBuilder<Statement, StatementInfo>(), ImmutableDictionary.CreateBuilder<Pattern, PatternInfo>(), diagnostics);
                delegateType = new ErrorTypeSymbol("Lambda type inference not implemented");
                break;

            default:
                delegateType = new ErrorTypeSymbol("Lambda type inference not implemented");
                break;
        }

        return new LambdaInfo(lambda, null, delegateType);
    }
    private AwaitInfo BindAwait(
        AwaitExpression await,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var exprInfo = BindExpression(await.Expression, scope, exprInfos, diagnostics);
        exprInfos[await.Expression] = exprInfo;
        var awaitedType = GetExpressionType(exprInfo) ?? new ErrorTypeSymbol("Unknown awaitable type");

        // TODO: awaitable resolution
        return new AwaitInfo(await, awaitedType, awaitedType);
    }

    private BlockStatementInfo BindBlock(
        BlockStatement block,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var blockScope = new Scope(scope);

        var statements = block.Statements
            .Select(statement =>
            {
                var statementInfo = BindStatement(statement, blockScope, exprInfos, stmtInfos, patternInfos, diagnostics);
                stmtInfos[statement] = statementInfo;
                return statementInfo;
            })
            .ToImmutableCollection();

        return new BlockStatementInfo(block, statements);
    }

    private VariableDeclarationStatementInfo BindVariableDecl(
        VariableDeclarationStatement statement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        ExpressionInfo? initializer = null;
        if (statement.Declaration.Initializer is not null)
        {
            initializer = BindExpression(statement.Declaration.Initializer, scope, exprInfos, diagnostics);
            exprInfos[statement.Declaration.Initializer] = initializer;
        }

        var symbol = new LocalSymbol(statement.Declaration, _types!.Resolve(statement.Declaration.TypeReference));
        scope.Add(symbol);

        return new VariableDeclarationStatementInfo(statement, symbol, initializer);
    }

    private IfStatementInfo BindIf(
        IfStatement ifStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var condition = BindExpression(ifStatement.Condition, scope, exprInfos, diagnostics);
        exprInfos[ifStatement.Condition] = condition;

        var thenInfo = BindStatement(ifStatement.Then, new Scope(scope), exprInfos, stmtInfos, patternInfos, diagnostics);
        stmtInfos[ifStatement.Then] = thenInfo;

        StatementInfo? elseInfo = null;
        if (ifStatement.Else is not null)
        {
            elseInfo = BindStatement(ifStatement.Else, new Scope(scope), exprInfos, stmtInfos, patternInfos, diagnostics);
            stmtInfos[ifStatement.Else] = elseInfo;
        }

        return new IfStatementInfo(ifStatement, condition, thenInfo, elseInfo);
    }

    private ForEachStatementInfo BindForEach(
        ForEachStatement forEachStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var collectionInfo = BindExpression(forEachStatement.Collection, scope, exprInfos, diagnostics);
        exprInfos[forEachStatement.Collection] = collectionInfo;
        var collectionType = GetExpressionType(collectionInfo) ?? new ErrorTypeSymbol("Unknown collection type");
        var elementType = InferElementType(collectionType);

        var iterationType = _types!.Resolve(forEachStatement.Variable.TypeReference);
        if (iterationType is ErrorTypeSymbol && elementType is not ErrorTypeSymbol)
        {
            iterationType = elementType;
        }

        var iterationVariable = new LocalSymbol(forEachStatement.Variable, iterationType);
        var loopScope = new Scope(scope);
        loopScope.Add(iterationVariable);

        var body = BindStatement(forEachStatement.Body, loopScope, exprInfos, stmtInfos, patternInfos, diagnostics);
        stmtInfos[forEachStatement.Body] = body;

        return new ForEachStatementInfo(forEachStatement, iterationVariable, collectionInfo, elementType, body);
    }

    private ReturnStatementInfo BindReturn(
        ReturnStatement returnStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var expression = BindExpression(returnStatement.Expression, scope, exprInfos, diagnostics);
        exprInfos[returnStatement.Expression] = expression;
        return new ReturnStatementInfo(returnStatement, expression);
    }

    private UnknownStatementInfo BindWhile(
        WhileStatement whileStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var condition = BindExpression(whileStatement.Condition, scope, exprInfos, diagnostics);
        exprInfos[whileStatement.Condition] = condition;
        var body = BindStatement(whileStatement.Body, new Scope(scope), exprInfos, stmtInfos, patternInfos, diagnostics);
        stmtInfos[whileStatement.Body] = body;

        return new UnknownStatementInfo(whileStatement);
    }

    private UnknownStatementInfo BindFor(
        ForStatement forStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var forScope = new Scope(scope);

        foreach (var initializer in forStatement.Initializer)
        {
            var initializerInfo = BindStatement(initializer, forScope, exprInfos, stmtInfos, patternInfos, diagnostics);
            stmtInfos[initializer] = initializerInfo;
        }

        if (forStatement.Condition is not null)
        {
            var conditionInfo = BindExpression(forStatement.Condition, forScope, exprInfos, diagnostics);
            exprInfos[forStatement.Condition] = conditionInfo;
        }

        foreach (var iterator in forStatement.Iterator)
        {
            var iteratorInfo = BindStatement(iterator, forScope, exprInfos, stmtInfos, patternInfos, diagnostics);
            stmtInfos[iterator] = iteratorInfo;
        }

        var body = BindStatement(forStatement.Body, new Scope(forScope), exprInfos, stmtInfos, patternInfos, diagnostics);
        stmtInfos[forStatement.Body] = body;

        return new UnknownStatementInfo(forStatement);
    }

    private UnknownStatementInfo BindSwitch(
        SwitchStatement switchStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var expressionInfo = BindExpression(switchStatement.Expression, scope, exprInfos, diagnostics);
        exprInfos[switchStatement.Expression] = expressionInfo;

        foreach (var switchCase in switchStatement.Cases)
        {
            var patternInfo = BindPattern(switchCase.Pattern, scope, exprInfos, patternInfos, diagnostics);
            patternInfos[switchCase.Pattern] = patternInfo;

            foreach (var statement in switchCase.Body)
            {
                var statementInfo = BindStatement(statement, new Scope(scope), exprInfos, stmtInfos, patternInfos, diagnostics);
                stmtInfos[statement] = statementInfo;
            }
        }

        return new UnknownStatementInfo(switchStatement);
    }

    private UnknownStatementInfo BindTry(
        TryStatement tryStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var body = BindStatement(tryStatement.Body, new Scope(scope), exprInfos, stmtInfos, patternInfos, diagnostics);
        stmtInfos[tryStatement.Body] = body;

        foreach (var catchClause in tryStatement.Catches)
        {
            var catchScope = new Scope(scope);
            var catchType = _types!.Resolve(catchClause.Type);
            catchScope.Add(new LocalSymbol(new VariableDecl(catchClause.Type, catchClause.Variable, null), catchType));
            var catchBody = BindStatement(catchClause.Body, catchScope, exprInfos, stmtInfos, patternInfos, diagnostics);
            stmtInfos[catchClause.Body] = catchBody;
        }

        if (tryStatement.Finally is not null)
        {
            var finallyBody = BindStatement(tryStatement.Finally, new Scope(scope), exprInfos, stmtInfos, patternInfos, diagnostics);
            stmtInfos[tryStatement.Finally] = finallyBody;
        }

        return new UnknownStatementInfo(tryStatement);
    }

    private UnknownStatementInfo BindThrow(
        ThrowStatement throwStatement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var expressionInfo = BindExpression(throwStatement.Expression, scope, exprInfos, diagnostics);
        exprInfos[throwStatement.Expression] = expressionInfo;

        return new UnknownStatementInfo(throwStatement);
    }

    private UnknownStatementInfo BindUnsupportedStatement(
        Statement statement,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Statement, StatementInfo>.Builder stmtInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        switch (statement)
        {
            case WhileStatement whileStatement:
                BindWhile(whileStatement, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                break;

            case ForStatement forStatement:
                BindFor(forStatement, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                break;

            case SwitchStatement switchStatement:
                BindSwitch(switchStatement, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                break;

            case TryStatement tryStatement:
                BindTry(tryStatement, scope, exprInfos, stmtInfos, patternInfos, diagnostics);
                break;

            case ThrowStatement throwStatement:
                BindThrow(throwStatement, scope, exprInfos, diagnostics);
                break;
        }

        return new UnknownStatementInfo(statement);
    }

    private UnknownExpressionInfo BindIndexAccess(
        IndexAccessExpression indexAccess,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var targetInfo = BindExpression(indexAccess.Target, scope, exprInfos, diagnostics);
        exprInfos[indexAccess.Target] = targetInfo;

        foreach (var index in indexAccess.Indices)
        {
            var indexInfo = BindExpression(index, scope, exprInfos, diagnostics);
            exprInfos[index] = indexInfo;
        }

        return new UnknownExpressionInfo(indexAccess);
    }

    private UnknownExpressionInfo BindTuple(
        TupleExpression tuple,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        foreach (var element in tuple.Elements)
        {
            var elementInfo = BindExpression(element, scope, exprInfos, diagnostics);
            exprInfos[element] = elementInfo;
        }

        return new UnknownExpressionInfo(tuple);
    }

    private RecursivePatternInfo BindRecursivePattern(
        RecursivePattern recursivePattern,
        Scope scope,
        ImmutableDictionary<Expression, ExpressionInfo>.Builder exprInfos,
        ImmutableDictionary<Pattern, PatternInfo>.Builder patternInfos,
        ImmutableArray<string>.Builder diagnostics)
    {
        var type = _types!.Resolve(recursivePattern.TypeReference);

        var subPatterns = recursivePattern.Properties
            .Select(property =>
            {
                var patternInfo = BindPattern(property.Pattern, scope, exprInfos, patternInfos, diagnostics);
                patternInfos[property.Pattern] = patternInfo;
                return patternInfo;
            })
            .ToImmutableCollection();

        return new RecursivePatternInfo(recursivePattern, type, subPatterns);
    }

    private MemberSymbol? CreateMemberSymbol(MemberDecl memberDecl, TypeSymbol declaredType)
    {
        return memberDecl.Kind switch
        {
            MemberKind.Method => new MethodSymbol(
                memberDecl,
                _types!.Resolve(memberDecl.TypeReference),
                memberDecl.Parameters
                    .Select(parameter => new ParameterSymbol(parameter, _types.Resolve(parameter.TypeReference)))
                    .ToImmutableCollection()),

            MemberKind.Property => new PropertySymbol(memberDecl, _types!.Resolve(memberDecl.TypeReference)),

            MemberKind.Field => new FieldSymbol(memberDecl, _types!.Resolve(memberDecl.TypeReference)),

            MemberKind.Event => new EventSymbol(memberDecl, _types!.Resolve(memberDecl.TypeReference)),

            MemberKind.Constructor => new MethodSymbol(
                memberDecl,
                declaredType,
                memberDecl.Parameters
                    .Select(parameter => new ParameterSymbol(parameter, _types!.Resolve(parameter.TypeReference)))
                    .ToImmutableCollection()),

            _ => null
        };
    }

    private TypeSymbol InferElementType(TypeSymbol collectionType)
    {
        return collectionType switch
        {
            DeclaredTypeSymbol declaredType when declaredType.GenericArguments.Count > 0 => declaredType.GenericArguments.First(),
            ErrorTypeSymbol errorType => errorType,
            _ => new ErrorTypeSymbol($"Unknown element type for collection '{collectionType.Name}'")
        };
    }

    private TypeSymbol? GetExpressionType(ExpressionInfo info)
    {
        return info switch
        {
            IdentifierInfo i => i.Type,
            LiteralInfo lit => lit.Type,
            BinaryInfo bin => bin.ResultType,
            UnaryInfo un => un.ResultType,
            CastInfo cast => cast.TargetType,
            IsInstanceInfo isInstance => isInstance.TargetType,
            CallInfo call => call.ReturnType,
            MemberAccessInfo ma => ma.Type,
            ObjectCreationInfo oc => oc.Type,
            ConditionalInfo cond => cond.ResultType,
            LambdaInfo lambda => lambda.DelegateType,
            AwaitInfo await => await.ResultType,

            UnknownExpressionInfo _ => null,

            _ => null
        };
    }
    private TypeSymbol InferLiteralType(LiteralExpression lit)
    {
        return lit.Value switch
        {
            int => _types!.ResolveBuiltIn("int"),
            long => _types!.ResolveBuiltIn("long"),
            float => _types!.ResolveBuiltIn("float"),
            double => _types!.ResolveBuiltIn("double"),
            bool => _types!.ResolveBuiltIn("bool"),
            string => _types!.ResolveBuiltIn("string"),
            char => _types!.ResolveBuiltIn("char"),
            null => _types!.ResolveBuiltIn("null"),

            _ => new ErrorTypeSymbol($"Unknown literal type: {lit.Value}")
        };
    }
}
