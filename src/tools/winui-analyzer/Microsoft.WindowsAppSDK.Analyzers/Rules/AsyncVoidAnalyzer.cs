// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.WindowsAppSDK.Analyzers.Rules;

/// <summary>
/// Detects private, parameterless <c>async void</c> methods that are not used
/// as delegates. These are not event handlers, and an exception after an
/// <c>await</c> terminates a WinUI application.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.ParameterlessAsyncVoid,
        "Non-event async method returns void",
        "'{0}' is not an event handler but returns void; return Task and await it (or explicitly discard the Task) so exceptions do not terminate the app",
        DiagnosticCategories.Runtime,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Private parameterless non-event methods should return Task. " +
                     "Exceptions from async void methods are posted to the WinUI synchronization context and can terminate the process.",
        helpLinkUri: HelpLinks.For(DiagnosticIds.ParameterlessAsyncVoid));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword) ||
            method.ParameterList.Parameters.Count != 0 ||
            method.ReturnType is not PredefinedTypeSyntax returnType ||
            !returnType.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
        if (methodSymbol?.DeclaredAccessibility != Accessibility.Private ||
            methodSymbol.ExplicitInterfaceImplementations.Length != 0 ||
            IsUsedAsDelegate(context, methodSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            method.ReturnType.GetLocation(),
            MigrationTiers.StartupCrashProperties,
            method.Identifier.ValueText));
    }

    private static bool IsUsedAsDelegate(
        SyntaxNodeAnalysisContext context,
        IMethodSymbol methodSymbol)
    {
        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot(context.CancellationToken);
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            foreach (var simpleName in root.DescendantNodes()
                         .OfType<SimpleNameSyntax>()
                         .Where(node => node.Identifier.ValueText == methodSymbol.Name))
            {
                var symbolInfo = semanticModel.GetSymbolInfo(simpleName, context.CancellationToken);
                if (!IsSameMethod(symbolInfo.Symbol, methodSymbol) &&
                    !symbolInfo.CandidateSymbols.Any(candidate => IsSameMethod(candidate, methodSymbol)))
                {
                    continue;
                }

                ExpressionSyntax expression = simpleName;
                if (simpleName.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == simpleName)
                {
                    expression = memberAccess;
                }

                if (semanticModel.GetTypeInfo(expression, context.CancellationToken)
                        .ConvertedType?.TypeKind == TypeKind.Delegate)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSameMethod(ISymbol? candidate, IMethodSymbol methodSymbol) =>
        candidate is IMethodSymbol candidateMethod &&
        SymbolEqualityComparer.Default.Equals(
            candidateMethod.OriginalDefinition,
            methodSymbol.OriginalDefinition);
}
