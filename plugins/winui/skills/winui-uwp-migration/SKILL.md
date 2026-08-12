---
name: winui-uwp-migration
description: "Use immediately when migrating, porting, or converting a C# UWP app to WinUI 3 / Windows App SDK, including projects using Windows.UI.Xaml or UWP Package.appxmanifest files. C++/WinRT and Visual Basic UWP migrations are out of scope."
---

# UWP to WinUI 3 migration

Preserve the app; do not redesign it. Keep every page, control, resource, helper, navigation path, and user-visible behavior unless the API has no WinUI 3 desktop equivalent. Unsupported behavior must be reported explicitly, never omitted silently.

## Ownership

- `winapp migrate` creates the WinUI project, copies the UWP source, performs safe mechanical transforms, and writes `migration-report.json`.
- This skill builds one semantic migration plan, uses the report as evidence within that plan, then uses build-time diagnostics and source-to-target state replay to finish the migration.
- `migration-report.json` is a mechanical snapshot and evidence index. It is neither a semantic work schedule nor a complete inventory; an empty TODO list does not guarantee a buildable or runnable app.
- Do not call `winapp migrate analyze`, `winapp migrate scaffold`, `winapp migrate validate`, or `winui-analyze`.

Load the `winui-dev-workflow` skill before building or running. Its `BuildAndRun.ps1` injects the WinUI analyzer into the build and launches through `winapp run`.

## 1. Run the mechanical migration

The first substantive action after loading this skill is to run this command. Before it runs, inspect only enough workspace metadata to locate the UWP project and target directory. Do not inventory or read the source files first: the generated report and merged target are the starting point for semantic analysis.

```powershell
winapp migrate "<absolute-uwp-project-directory>" `
    --output "<absolute-new-winui-project-directory>"
```

Use `--name <ProjectName>` only when the user requires a specific target name. The output directory may be new, empty, or contain only supported control-plane metadata such as `.git` and `.github`; those entries are preserved. The command creates the official WinUI scaffold itself; never run `dotnet new winui` separately and never copy the project by hand.

If the command fails, fix the reported prerequisite or input problem and retry. Do not work around it with a second scaffold or a nested project copy.

Before editing, establish one whole-app semantic model:

1. Confirm `<target>/migration-report.json` exists.
2. Read it once.
3. Confirm `schemaVersion` is supported and `status` is `mechanical-migration-complete`.
4. Read the complete project structure and the source, XAML, manifest, and project files needed to understand startup, navigation, shared state, resources, and every feature path.
5. Combine report residuals, source behavior, and analyzer-relevant UWP APIs into one migration inventory grouped by shared root cause.
6. Resolve every uncertain API mapping before editing. Prefer Microsoft Learn and repository-local guidance; do not launch a research subagent for routine API lookups.

Do not turn report categories, files, or locations into separate turns. The report points to evidence; the semantic inventory determines the edit plan.

## 2. Capture the source behavior baseline

Before editing the target, read [Visual validation](references/visual-validation.md). Define a compact state plan that covers startup, each top-level feature path, and every migration-sensitive behavior in the semantic inventory, including nonstandard activation, lifecycle, background, and multi-window behavior when present. Capture the original UWP app at those states, including screenshots and UI trees for visual states, when it can run.

Use only existing build, deployment, OS activation, `winapp run`, and `winapp ui` commands. Do not create UI automation scripts or add test code to either app. If the source cannot run, accept user-provided screenshots or recordings only when their action context and expected outcome are known. If neither runtime nor sufficient user evidence is available, record the affected behavior as `unverified`; do not infer parity from source code alone.

After the last source state is captured, close that exact source window by HWND and confirm it disappeared before editing the target. Do not leave the source app running during migration. Never terminate `ApplicationFrameHost` or use a broad process-name cleanup because it can host unrelated UWP windows.

## 3. Apply one coherent migration

Fix shared causes through shared abstractions before patching call sites. For example, establish an app-owned window reference or one HWND/orientation helper, then migrate every dependent page consistently. Preserve startup order and cross-page behavior.

Apply the planned changes as one coherent patch when practical. If the app is too large, split only at an architecture boundary that can build independently. Never use report order, one category per turn, or one file per turn as the partition.

Use report categories as checks within the patch:

- merge app resources without replacing the WinUI startup bootstrap;
- restore only compatible dependencies and manifest declarations required by preserved features;
- replace dispatcher and windowing APIs through shared WinUI 3 abstractions;
- reconcile shared-file conflicts without losing either source's required behavior;
- wire the initial page without replacing generated bootstrap or title-bar behavior.

For an unknown report category, use its `summary`, `reason`, and `locations` as evidence; do not guess from the ID. Preserve source XAML bindings, event handlers, default selection, initialization order, navigation reachability, AutomationIds, and observable feature outcomes. Do not rewrite working pages merely to make them look more idiomatic.

When an API mapping is uncertain, consult the official [UWP to Windows App SDK mapping table](https://learn.microsoft.com/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/api-mapping-table). Never fabricate an equivalent or remove behavior merely because the first interop attempt fails. Use a visible fallback only when authoritative documentation confirms that the original behavior has no desktop equivalent. A fallback is a documented limitation, not evidence that the original feature was resolved.

## 4. Build and fix in batches

Run the `BuildAndRun.ps1` supplied by `winui-dev-workflow` in build-only mode:

```powershell
.\BuildAndRun.ps1 -SkipRun
```

On failure, read the complete error set, group it by root cause, and fix each group in one pass. Do not build after every file or diagnostic. Target no more than three grouped builds: initial convergence, root-cause correction, and final confirmation.

Fix:

- compiler and XAML errors;
- migration-blocking compatibility diagnostics (`WUI0001`–`WUI0005` and `WUI2003`);
- missing content, resources, packages, and manifest declarations required by preserved features.

WinUI XAML compilation can take several minutes. A shell status saying the command is still running is not a build failure: continue reading that same shell. Do not terminate it or start a duplicate build unless the workflow reports an error or remains inactive beyond the benchmark or user-provided timeout.

Do not spend turns clearing advisory diagnostics unrelated to migration success. If repeated builds expose the same error, stop making speculative edits and inspect the full type, project, and call-site context.

## 5. Replay and compare the migrated app

Use `BuildAndRun.ps1` without `-SkipRun`; never launch the packaged executable directly.

Replay the source state plan against the migrated app with the generic commands in [Visual validation](references/visual-validation.md). Prefer stable semantic names for cross-framework UI actions; use target-specific AutomationIds only after inspecting the target UI. At each visual state, capture both the UI tree and screenshot; retain the relevant observable evidence for non-visual states.

Compare observable outcomes, content, control presence, navigation, and relative layout. Theme, window dimensions, default styling, and rendering density may legitimately change between UWP and WinUI 3; do not use raw pixel similarity as the parity gate. Missing content, blank regions, clipping, failed actions, or an unreachable state are failures.

If the app exits or turns blank, read the `winapp run --debug-output` diagnostics from the workflow and fix the runtime cause before declaring completion.

Do not create temporary UI automation scripts or exhaustively probe equivalent permutations. Reuse one source instance within source capture and one target instance within target replay, closing each immediately after its phase. Capture one state per distinct behavior or migration risk. A launch-only smoke check is insufficient when source baseline evidence exists.

## 6. Finalize the report

Only after the app builds and planned target states have been replayed, update `migration-report.json` once:

- set a TODO from `pending` to `resolved` only when the implemented code and available evidence establish that its required behavior is preserved;
- leave fallback behavior, blocked hardware-dependent behavior, failed replay states, and behavior without source evidence as `pending`, and report why it is blocked or unverified;
- do not delete TODOs, rewrite their original descriptions, or invent completion evidence.

Report unresolved behavior and the visual-validation status to the user. Do not claim behavioral or visual parity from build success or a process launch, and do not claim the migration complete while required work remains pending.
