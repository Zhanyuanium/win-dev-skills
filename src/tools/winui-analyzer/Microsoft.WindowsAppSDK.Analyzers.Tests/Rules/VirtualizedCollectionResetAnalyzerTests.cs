// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.WindowsAppSDK.Analyzers.Rules;
using Xunit;

namespace Microsoft.WindowsAppSDK.Analyzers.Tests.Rules;

public sealed class VirtualizedCollectionResetAnalyzerTests
{
    private const string Types = @"
using System;
using System.Collections.Specialized;

namespace Microsoft.UI.Xaml.Data
{
    public interface IItemsRangeInfo { }
}

public sealed class RangeCache
{
    public void UpdateRanges(object ranges) { }
}
";

    [Fact]
    public async Task Wui2005FlagsRebuiltRangeCacheWithoutReplay()
    {
        await new AnalyzerTest<VirtualizedCollectionResetAnalyzer>()
            .WithSource(Types + @"
public sealed class Source : Microsoft.UI.Xaml.Data.IItemsRangeInfo
{
    private RangeCache cache = new();
    private object trackedRanges = new();
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void RangesChanged(object ranges)
    {
        cache.UpdateRanges(ranges);
    }

    public void ResetCollection()
    {
        cache = new RangeCache();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}")
            .ExpectDiagnostic(DiagnosticIds.VirtualizedResetDropsCache)
            .ExpectMessageContains(DiagnosticIds.VirtualizedResetDropsCache, "retain the tracked ranges")
            .RunAsync();
    }

    [Fact]
    public async Task Wui2005AllowsTrackedRangeReplayAfterReset()
    {
        await new AnalyzerTest<VirtualizedCollectionResetAnalyzer>()
            .WithSource(Types + @"
public sealed class Source : Microsoft.UI.Xaml.Data.IItemsRangeInfo
{
    private RangeCache cache = new();
    private object trackedRanges = new();
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void RangesChanged(object ranges)
    {
        trackedRanges = ranges;
        cache.UpdateRanges(ranges);
    }

    public void ResetCollection()
    {
        cache = new RangeCache();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        cache.UpdateRanges(trackedRanges);
    }
}")
            .RunAsync();
    }

    [Fact]
    public async Task Wui2005IgnoresOrdinaryObservableCollection()
    {
        await new AnalyzerTest<VirtualizedCollectionResetAnalyzer>()
            .WithSource(Types + @"
public sealed class Source
{
    private RangeCache cache = new();
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void RangesChanged(object ranges)
    {
        cache.UpdateRanges(ranges);
    }

    public void ResetCollection()
    {
        cache = new RangeCache();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}")
            .RunAsync();
    }

    [Fact]
    public async Task Wui2005IgnoresResetThatKeepsRangeCache()
    {
        await new AnalyzerTest<VirtualizedCollectionResetAnalyzer>()
            .WithSource(Types + @"
public sealed class Source : Microsoft.UI.Xaml.Data.IItemsRangeInfo
{
    private RangeCache cache = new();
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void RangesChanged(object ranges)
    {
        cache.UpdateRanges(ranges);
    }

    public void ResetCollection()
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}")
            .RunAsync();
    }
}
