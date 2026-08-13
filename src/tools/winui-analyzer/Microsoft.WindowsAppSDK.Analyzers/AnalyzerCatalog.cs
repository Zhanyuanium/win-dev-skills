// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.WindowsAppSDK.Analyzers.Rules;

namespace Microsoft.WindowsAppSDK.Analyzers;

/// <summary>
/// Creates the complete analyzer set for out-of-build hosts.
/// </summary>
public static class AnalyzerCatalog
{
    public static ImmutableArray<DiagnosticAnalyzer> CreateAll() =>
        ImmutableArray.Create<DiagnosticAnalyzer>(
            new UwpApiAnalyzer(),
            new ApiMappingAnalyzer(),
            new XamlAnalyzer(),
            new XamlCodeBehindAnalyzer(),
            new TabViewContentAnalyzer(),
            new AsyncVoidAnalyzer(),
            new VirtualizedCollectionResetAnalyzer(),
            new AttachedPropertyAnalyzer(),
            new MvvmPatternAnalyzer(),
            new WebView2InitAnalyzer(),
            new GenAiApiAnalyzer());
}
