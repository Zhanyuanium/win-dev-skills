// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.WindowsAppSDK.Analyzers;

internal static class MigrationTiers
{
    public const string PropertyKey = "MigrationTier";
    public const string StartupCrash = "startup-crash";

    public static readonly ImmutableDictionary<string, string?> StartupCrashProperties =
        ImmutableDictionary<string, string?>.Empty.Add(PropertyKey, StartupCrash);
}
