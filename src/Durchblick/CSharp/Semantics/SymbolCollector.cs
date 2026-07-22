namespace Durchblick.CSharp.Semantics;

using System.Collections.Immutable;
using Durchblick.Collections;
using Durchblick.CSharp.Syntax;

/// <summary>
/// Performs phase 1 of the binder pipeline by collecting declared symbols.
/// </summary>
/// <remarks>
/// The collector walks declaration nodes only. It assumes the syntax tree is well-formed enough
/// to enumerate namespaces, types, and members, and it produces the symbol table needed by phase 2.
/// </remarks>
internal sealed class SymbolCollector
{
    /// <summary>
    /// Builds the symbol table, root scope, and member symbol list for a compilation unit.
    /// </summary>
    public BoundCompilation Collect(CompilationUnitDecl compilation)
    {
        var diagnostics = ImmutableArray.CreateBuilder<string>();

        var namespaceSymbols = new List<NamespaceSymbol>();
        var typeSymbols = new List<TypeSymbol>();

        foreach (var namespaceDecl in compilation.Namespaces)
        {
            var namespaceTypes = namespaceDecl.Members
                .Select<TypeDecl, TypeSymbol>(typeDecl => new DeclaredTypeSymbol(typeDecl, []))
                .ToImmutableCollection();

            var namespaceSymbol = new NamespaceSymbol(namespaceDecl.Name, [], namespaceTypes);
            namespaceSymbols.Add(namespaceSymbol);

            foreach (var typeSymbol in namespaceTypes)
            {
                typeSymbols.Add(typeSymbol);
            }
        }

        var symbolTable = new SymbolTable(
            namespaceSymbols.ToImmutableCollection(),
            typeSymbols.ToImmutableCollection());

        var globalScope = new Scope();
        foreach (var namespaceSymbol in namespaceSymbols)
        {
            globalScope.Add(namespaceSymbol);
        }

        foreach (var typeSymbol in typeSymbols)
        {
            globalScope.Add(typeSymbol);
        }

        var typeResolver = new TypeResolver(symbolTable);
        var memberSymbols = new List<MemberSymbol>();

        foreach (var namespaceDecl in compilation.Namespaces)
        {
            var namespaceScope = new Scope(globalScope);
            var namespaceSymbol = namespaceSymbols.First(ns => ns.Name == namespaceDecl.Name);
            namespaceScope.Add(namespaceSymbol);

            foreach (var typeDecl in namespaceDecl.Members)
            {
                BindTypeDecl(typeDecl, namespaceScope, typeResolver, memberSymbols, diagnostics);
            }
        }

        return new BoundCompilation(
            Compilation: compilation,
            GlobalScope: globalScope,
            SymbolTable: symbolTable,
            Diagnostics: diagnostics.ToImmutable(),
            MemberSymbols: memberSymbols.ToImmutableArray()
        );
    }

    private void BindTypeDecl(
        TypeDecl typeDecl,
        Scope parentScope,
        TypeResolver typeResolver,
        List<MemberSymbol> memberSymbols,
        ImmutableArray<string>.Builder diagnostics)
    {
        var declaredType = ResolveType(new TypeReference(typeDecl.Name, null, []), typeResolver, diagnostics);
        var typeScope = new Scope(parentScope);
        typeScope.Add(declaredType);

        foreach (var typeParameterDecl in typeDecl.TypeParameters)
        {
            typeScope.Add(new TypeParameterSymbol(typeParameterDecl));
        }

        foreach (var memberDecl in typeDecl.Members)
        {
            var memberSymbol = CreateMemberSymbol(memberDecl, declaredType, typeResolver, diagnostics);
            if (memberSymbol is null)
            {
                diagnostics.Add($"Unsupported member kind '{memberDecl.Kind}' on type '{typeDecl.Name}'.");
                continue;
            }

            memberSymbols.Add(memberSymbol);
            typeScope.Add(memberSymbol);
        }

        foreach (var memberDecl in typeDecl.Members)
        {
            BindMemberDecl(memberDecl, typeScope, typeResolver, diagnostics, declaredType);
        }
    }

    private MemberSymbol? CreateMemberSymbol(
        MemberDecl memberDecl,
        TypeSymbol declaredType,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics)
    {
        var parameterSymbols = memberDecl.Parameters
            .Select(parameter => new ParameterSymbol(parameter, ResolveType(parameter.TypeReference, typeResolver, diagnostics)))
            .ToImmutableCollection();

        return memberDecl.Kind switch
        {
            MemberKind.Method => new MethodSymbol(
                memberDecl,
                ResolveType(memberDecl.TypeReference, typeResolver, diagnostics),
                parameterSymbols),

            MemberKind.Property => new PropertySymbol(
                memberDecl,
                ResolveType(memberDecl.TypeReference, typeResolver, diagnostics)),

            MemberKind.Field => new FieldSymbol(
                memberDecl,
                ResolveType(memberDecl.TypeReference, typeResolver, diagnostics)),

            MemberKind.Event => new EventSymbol(
                memberDecl,
                ResolveType(memberDecl.TypeReference, typeResolver, diagnostics)),

            MemberKind.Constructor => new MethodSymbol(
                memberDecl,
                declaredType,
                parameterSymbols),

            _ => null
        };
    }

    private void BindMemberDecl(
        MemberDecl memberDecl,
        Scope parentScope,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics,
        TypeSymbol declaredType)
    {
        if (memberDecl.Body is null)
        {
            foreach (var accessor in memberDecl.Accessors)
            {
                BindStatement(accessor.Body, parentScope, typeResolver, diagnostics, declaredType);
            }

            return;
        }

        var memberScope = new Scope(parentScope);

        foreach (var parameterDecl in memberDecl.Parameters)
        {
            var parameterSymbol = new ParameterSymbol(
                parameterDecl,
                ResolveType(parameterDecl.TypeReference, typeResolver, diagnostics));
            memberScope.Add(parameterSymbol);
        }

        BindStatement(memberDecl.Body, memberScope, typeResolver, diagnostics, declaredType);
    }

    private void BindStatement(
        Statement statement,
        Scope scope,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics,
        TypeSymbol declaredType)
    {
        switch (statement)
        {
            case BlockStatement blockStatement:
                var blockScope = new Scope(scope);
                foreach (var nestedStatement in blockStatement.Statements)
                {
                    BindStatement(nestedStatement, blockScope, typeResolver, diagnostics, declaredType);
                }

                break;

            case ExpressionStatement expressionStatement:
                BindExpression(expressionStatement.Expression, scope, typeResolver, diagnostics);
                break;

            case VariableDeclarationStatement variableDeclarationStatement:
                BindVariableDecl(variableDeclarationStatement.Declaration, scope, typeResolver, diagnostics);
                break;

            case IfStatement ifStatement:
                BindExpression(ifStatement.Condition, scope, typeResolver, diagnostics);
                BindStatement(ifStatement.Then, new Scope(scope), typeResolver, diagnostics, declaredType);

                if (ifStatement.Else is not null)
                {
                    BindStatement(ifStatement.Else, new Scope(scope), typeResolver, diagnostics, declaredType);
                }

                break;

            case ForEachStatement forEachStatement:
                BindExpression(forEachStatement.Collection, scope, typeResolver, diagnostics);
                var iterationSymbol = new LocalSymbol(
                    forEachStatement.Variable,
                    ResolveType(forEachStatement.Variable.TypeReference, typeResolver, diagnostics));
                var forEachScope = new Scope(scope);
                forEachScope.Add(iterationSymbol);
                BindStatement(forEachStatement.Body, forEachScope, typeResolver, diagnostics, declaredType);
                break;

            case ReturnStatement returnStatement:
                BindExpression(returnStatement.Expression, scope, typeResolver, diagnostics);
                break;

            case WhileStatement whileStatement:
                BindExpression(whileStatement.Condition, scope, typeResolver, diagnostics);
                BindStatement(whileStatement.Body, new Scope(scope), typeResolver, diagnostics, declaredType);
                break;

            case ForStatement forStatement:
                var forScope = new Scope(scope);
                foreach (var initializer in forStatement.Initializer)
                {
                    BindStatement(initializer, forScope, typeResolver, diagnostics, declaredType);
                }

                if (forStatement.Condition is not null)
                {
                    BindExpression(forStatement.Condition, forScope, typeResolver, diagnostics);
                }

                foreach (var iterator in forStatement.Iterator)
                {
                    BindStatement(iterator, forScope, typeResolver, diagnostics, declaredType);
                }

                BindStatement(forStatement.Body, new Scope(forScope), typeResolver, diagnostics, declaredType);
                break;

            case SwitchStatement switchStatement:
                BindExpression(switchStatement.Expression, scope, typeResolver, diagnostics);

                foreach (var switchCase in switchStatement.Cases)
                {
                    BindPattern(switchCase.Pattern, scope, typeResolver, diagnostics);

                    foreach (var nestedStatement in switchCase.Body)
                    {
                        BindStatement(nestedStatement, new Scope(scope), typeResolver, diagnostics, declaredType);
                    }
                }

                break;

            case TryStatement tryStatement:
                BindStatement(tryStatement.Body, new Scope(scope), typeResolver, diagnostics, declaredType);

                foreach (var catchClause in tryStatement.Catches)
                {
                    var catchScope = new Scope(scope);
                    catchScope.Add(new LocalSymbol(new VariableDecl(catchClause.Type, catchClause.Variable, null), ResolveType(catchClause.Type, typeResolver, diagnostics)));
                    BindStatement(catchClause.Body, catchScope, typeResolver, diagnostics, declaredType);
                }

                if (tryStatement.Finally is not null)
                {
                    BindStatement(tryStatement.Finally, new Scope(scope), typeResolver, diagnostics, declaredType);
                }

                break;

            case ThrowStatement throwStatement:
                BindExpression(throwStatement.Expression, scope, typeResolver, diagnostics);
                break;

            case BreakStatement:
            case ContinueStatement:
                break;
        }
    }

    private void BindVariableDecl(
        VariableDecl variableDecl,
        Scope scope,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics)
    {
        var symbol = new LocalSymbol(variableDecl, ResolveType(variableDecl.TypeReference, typeResolver, diagnostics));
        scope.Add(symbol);

        if (variableDecl.Initializer is not null)
        {
            BindExpression(variableDecl.Initializer, scope, typeResolver, diagnostics);
        }
    }

    private void BindExpression(
        Expression expression,
        Scope scope,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics)
    {
        switch (expression)
        {
            case IdentifierExpression:
            case LiteralExpression:
                break;

            case UnaryExpression unaryExpression:
                BindExpression(unaryExpression.Operand, scope, typeResolver, diagnostics);
                break;

            case BinaryExpression binaryExpression:
                BindExpression(binaryExpression.Left, scope, typeResolver, diagnostics);
                BindExpression(binaryExpression.Right, scope, typeResolver, diagnostics);
                break;

            case ConditionalExpression conditionalExpression:
                BindExpression(conditionalExpression.Condition, scope, typeResolver, diagnostics);
                BindExpression(conditionalExpression.Then, scope, typeResolver, diagnostics);
                BindExpression(conditionalExpression.Else, scope, typeResolver, diagnostics);
                break;

            case CallExpression callExpression:
                BindExpression(callExpression.Target, scope, typeResolver, diagnostics);
                foreach (var argument in callExpression.Arguments)
                {
                    BindExpression(argument, scope, typeResolver, diagnostics);
                }

                break;

            case MemberAccessExpression memberAccessExpression:
                BindExpression(memberAccessExpression.Target, scope, typeResolver, diagnostics);
                break;

            case ObjectCreationExpression objectCreationExpression:
                foreach (var argument in objectCreationExpression.Arguments)
                {
                    BindExpression(argument, scope, typeResolver, diagnostics);
                }

                foreach (var initializer in objectCreationExpression.Initializer)
                {
                    BindExpression(initializer.Value, scope, typeResolver, diagnostics);
                }

                break;

            case LambdaExpression lambdaExpression:
                BindLambda(lambdaExpression, scope, typeResolver, diagnostics);
                break;

            case TupleExpression tupleExpression:
                foreach (var element in tupleExpression.Elements)
                {
                    BindExpression(element, scope, typeResolver, diagnostics);
                }

                break;

            case CastExpression castExpression:
                BindExpression(castExpression.Expression, scope, typeResolver, diagnostics);
                break;

            case IsInstanceExpression isInstanceExpression:
                BindExpression(isInstanceExpression.Expression, scope, typeResolver, diagnostics);
                _ = ResolveType(isInstanceExpression.Type, typeResolver, diagnostics);
                break;

            case AwaitExpression awaitExpression:
                BindExpression(awaitExpression.Expression, scope, typeResolver, diagnostics);
                break;

            case IndexAccessExpression indexAccessExpression:
                BindExpression(indexAccessExpression.Target, scope, typeResolver, diagnostics);
                foreach (var index in indexAccessExpression.Indices)
                {
                    BindExpression(index, scope, typeResolver, diagnostics);
                }

                break;
        }
    }

    private void BindLambda(
        LambdaExpression lambdaExpression,
        Scope scope,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics)
    {
        var lambdaScope = new Scope(scope);

        foreach (var parameter in lambdaExpression.Parameters)
        {
            lambdaScope.Add(new ParameterSymbol(new ParameterDecl(parameter.Name, parameter.Type, []), typeResolver.Resolve(parameter.Type)));
        }

        switch (lambdaExpression.Body)
        {
            case ExprBody exprBody:
                BindExpression(exprBody.Value, lambdaScope, typeResolver, diagnostics);
                break;

            case BlockBody blockBody:
                BindStatement(blockBody.Block, lambdaScope, typeResolver, diagnostics, typeResolver.ResolveBuiltIn(BuiltInTypeNames.Void));
                break;
        }
    }

    private void BindPattern(
        Pattern pattern,
        Scope scope,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics)
    {
        switch (pattern)
        {
            case TypePattern typePattern:
                _ = ResolveType(typePattern.TypeReference, typeResolver, diagnostics);
                break;

            case ConstantPattern constantPattern:
                BindExpression(constantPattern.Value, scope, typeResolver, diagnostics);
                break;

            case RelationalPattern relationalPattern:
                BindExpression(relationalPattern.Value, scope, typeResolver, diagnostics);
                break;

            case LogicalPattern logicalPattern:
                BindPattern(logicalPattern.Left, scope, typeResolver, diagnostics);
                BindPattern(logicalPattern.Right, scope, typeResolver, diagnostics);
                break;

            case RecursivePattern recursivePattern:
                _ = ResolveType(recursivePattern.TypeReference, typeResolver, diagnostics);
                foreach (var property in recursivePattern.Properties)
                {
                    BindPattern(property.Pattern, scope, typeResolver, diagnostics);
                }

                break;
        }
    }

    private static TypeSymbol ResolveType(
        TypeReference reference,
        TypeResolver typeResolver,
        ImmutableArray<string>.Builder diagnostics)
    {
        var type = typeResolver.Resolve(reference);

        if (type is ErrorTypeSymbol errorType)
        {
            diagnostics.Add(errorType.Message);
        }

        return type;
    }
}