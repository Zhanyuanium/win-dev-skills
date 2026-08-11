// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Microsoft.WindowsAppSDK.Analyzers.Tests;

public sealed class AnalyzerCatalogTests
{
    [Fact]
    public void CatalogIncludesEveryAnalyzerType()
    {
        var expected = typeof(AnalyzerCatalog).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var actual = AnalyzerCatalog.CreateAll()
            .Select(analyzer => analyzer.GetType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
