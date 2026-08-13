// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.WindowsAppSDK.Analyzers.Rules;

/// <summary>
/// Detects custom range-virtualized collections that replace the cache used by
/// <c>RangesChanged</c> before raising a reset without repopulating that cache.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VirtualizedCollectionResetAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.VirtualizedResetDropsCache,
        "Virtualized reset does not repopulate the rebuilt cache",
        "'{0}' replaces range cache '{1}' before Reset, but WinUI 3 may not call RangesChanged again when Count is unchanged; retain the tracked ranges and call {1}.UpdateRanges(...) after raising Reset",
        DiagnosticCategories.Runtime,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A custom IItemsRangeInfo source must repopulate a replacement range cache after a collection reset. " +
                     "WinUI 3 can retain the same visible range without invoking RangesChanged again.",
        helpLinkUri: HelpLinks.For(DiagnosticIds.VirtualizedResetDropsCache));

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
        var resetAccess = method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(node => IsCollectionReset(context, node));
        if (resetAccess is null)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
        var containingType = methodSymbol?.ContainingType;
        if (containingType is null || !ImplementsItemsRangeInfo(containingType))
        {
            return;
        }

        var rangeFields = GetRangesChangedFields(context, containingType);
        if (rangeFields.Count == 0)
        {
            return;
        }

        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.SpanStart >= resetAccess.SpanStart ||
                !CreatesNewInstance(assignment.Right) ||
                context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IFieldSymbol field ||
                !rangeFields.Contains(field, SymbolEqualityComparer.Default) ||
                ReplaysRangesAfterReset(context, method, resetAccess, field))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                resetAccess.GetLocation(),
                methodSymbol!.Name,
                field.Name));
        }
    }

    private static bool IsCollectionReset(
        SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Name.Identifier.ValueText != "Reset")
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        return symbol?.ContainingType?.ToDisplayString() == "System.Collections.Specialized.NotifyCollectionChangedAction";
    }

    private static bool ImplementsItemsRangeInfo(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(item => item.ToDisplayString() is
            "Microsoft.UI.Xaml.Data.IItemsRangeInfo" or
            "Windows.UI.Xaml.Data.IItemsRangeInfo");

    private static HashSet<IFieldSymbol> GetRangesChangedFields(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType)
    {
        var fields = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
        foreach (var method in containingType.GetMembers("RangesChanged").OfType<IMethodSymbol>())
        {
            foreach (var syntaxReference in method.DeclaringSyntaxReferences)
            {
                var syntax = syntaxReference.GetSyntax(context.CancellationToken);
                var semanticModel = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                foreach (var identifier in syntax.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (semanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is IFieldSymbol field)
                    {
                        fields.Add(field);
                    }
                }
            }
        }

        return fields;
    }

    private static bool CreatesNewInstance(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().Any(node =>
            node is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax);

    private static bool ReplaysRangesAfterReset(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method,
        MemberAccessExpressionSyntax resetAccess,
        IFieldSymbol field)
    {
        foreach (var invocation in method.DescendantNodes()
                     .OfType<InvocationExpressionSyntax>()
                     .Where(node => node.SpanStart > resetAccess.SpanStart))
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Name.Identifier.ValueText != "UpdateRanges")
            {
                continue;
            }

            var receiver = context.SemanticModel
                .GetSymbolInfo(memberAccess.Expression, context.CancellationToken)
                .Symbol;
            if (SymbolEqualityComparer.Default.Equals(receiver, field))
            {
                return true;
            }
        }

        return false;
    }
}
