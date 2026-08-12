# Visual validation

Use this protocol to preserve observable behavior across the UWP-to-WinUI migration. It orchestrates existing tools; do not create helper scripts, temporary test projects, or app-specific CLI extensions.

## 1. Plan evidence by behavior

After reading `migration-report.json` and the source, create a compact state plan:

- startup and initial navigation;
- each distinct top-level feature path;
- states that exercise migration-sensitive TODOs, bindings, data loading, selection, dialogs, or window-dependent behavior;
- protocol, file, toast, or command-line activation; suspend/resume and lifecycle transitions; background tasks; and secondary windows when the source uses them;
- user-provided critical flows.

Capture one state per distinct outcome or migration risk. Do not capture every data item, repeated control, or equivalent permutation. Store evidence under `<target>/.migration-evidence/source` and `<target>/.migration-evidence/target`; do not treat these files as application source or commit them unless the user requests it.

For each visual runtime state captured by the agent, retain:

1. ordered semantic actions;
2. a screenshot;
3. a JSON UI tree;
4. the related migration TODOs or feature paths.

For a non-visual state, retain its trigger, expected outcome, observable runtime evidence, and related TODOs. User-provided screenshots or recordings can replace agent-captured source evidence only when the actions and expected outcome are known; otherwise classify the state as `unverified`.

## 2. Capture the original UWP app

Prefer user-provided baseline evidence when it already covers the state plan. Otherwise:

1. Build the source with its existing solution/project tooling and the current-machine architecture. Do not edit the source to make capture easier.
2. Deploy and launch the built layout with `winapp run "<layout>" --manifest "<manifest>" --detach --json`.
3. If a successful Debug build crashes during framework startup, make at most one Release build and launch attempt. After that, record the affected states as `unverified` rather than entering a launch-repair loop.
4. Compare `winapp ui list-windows --json` before and after launch. A UWP top-level window may belong to `ApplicationFrameHost` instead of the PID returned by `winapp run`; identify the newly visible window by title and timing, then use its HWND for all capture and interaction commands.

Capture a state with the HWND:

```powershell
winapp ui inspect -w <hwnd> -d 8 --json |
    Set-Content -Encoding utf8 "<state>-ui.json"
winapp ui screenshot -w <hwnd> --focus --json -o "<state>.png"
winapp ui invoke "<semantic-name>" -w <hwnd> --json
```

After every action, inspect again and confirm that the expected state was reached before taking its screenshot. Prefer visible semantic names for action selectors because they can survive framework-specific AutomationId changes.

If permissions, external data, hardware, or credentials prevent a state from running, record only that state as blocked or unverified. Do not silently replace it with a different behavior.

After the last source state, inspect the source HWND for its title-bar Close element, invoke that element, and confirm the HWND is no longer listed:

```powershell
winapp ui invoke "Close" -w <source-hwnd> --json
winapp ui list-windows --json
```

If that window does not expose a UIA Close element, stop only the exact source PID returned by `winapp run`, then confirm the HWND disappeared. Never stop `ApplicationFrameHost` or clean up by a broad process name: it can own unrelated UWP windows. Perform this cleanup immediately after source capture, including when a planned source state fails, so a later migration timeout cannot leave the source app open.

## 3. Replay against WinUI 3

After the final build, launch the target through `BuildAndRun.ps1`. Use its PID with `winapp ui`; if more than one window is returned, select the intended HWND for each state rather than assuming one window covers the whole plan.

Replay the same ordered semantic actions and capture the same states under the target evidence directory. If a source semantic name is ambiguous or changed intentionally, inspect the target tree and use its AutomationId for target-local precision. Record the mapping instead of changing the source baseline.

Reuse the running app while replaying states. Restart only when a state explicitly depends on clean startup or prior actions cannot be reversed.

Replay nonstandard activation, lifecycle, background, and multi-window states with the same existing OS or deployment mechanism used for the source. If the environment cannot trigger or observe one of these states, mark its associated behavior `blocked` or `unverified`; a normal launch does not verify it.

After the last target state, close the exact target HWND the same way and confirm it disappeared. This also lets the asynchronous `BuildAndRun.ps1` / `winapp run --debug-output` invocation finish instead of leaving a live diagnostic session.

## 4. Compare semantically

For every source/target state pair, verify:

- the action succeeded and reached the intended state;
- expected navigation, text, controls, data, selection, status, and user-visible outcomes are present;
- content order and relative layout remain usable;
- no content is missing, blank, unintentionally hidden, clipped, or replaced by template UI;
- any intentional difference is supported by the migration design or platform behavior.

Use screenshots as semantic visual evidence, not as a raw pixel threshold. UWP and WinUI 3 can differ in theme, window size, default styles, spacing, rasterization, and item density while preserving behavior. Normalize window size and theme when practical, but do not hide legitimate platform differences or fail parity solely because pixels differ.

## 5. Apply the completion gate

Classify each planned state:

- `verified`: the required source evidence for that visual or non-visual state exists, target replay succeeded, and semantic comparison passed;
- `blocked`: an identified external prerequisite prevented capture or replay;
- `unverified`: no usable source evidence exists or the comparison could not be completed;
- `failed`: replay or comparison exposed a regression.

A TODO may be resolved only when all states that evidence its behavior are `verified`. Keep blocked, unverified, and failed behavior pending in `migration-report.json`, with the evidence path and reason. Build success, process launch, or a target-only screenshot is not parity evidence.
