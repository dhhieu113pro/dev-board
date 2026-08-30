# Roslyn DevSpace Initialization Design

## Goal

Make the Roslyn entry in DevSpace Workspace Health actionable. A user can explicitly initialize Roslyn for the current DevSpace, see progress and failures, retry when needed, and only see Roslyn-backed features as available after a real Roslyn workspace has loaded successfully.

## Current State

`DevSpaceDashboard.RoslynCapability` is hard-coded to `Unavailable`. The DevBoard application does not currently reference Roslyn Workspaces/MSBuild packages and there is no Roslyn workspace lifecycle service.

## User Experience

The Workspace Health card keeps the existing Copilot, Codex, Antigravity, and Roslyn rows.

Roslyn states:

- `Unavailable` — show an `Initialize` button.
- `Initializing` — disable the action and show initialization in progress.
- `Available` — hide the action; Roslyn-backed diagnostics/search may use the loaded workspace.
- `Failed` — show a `Retry` button and expose the failure reason in the row/tooltip.

Initialization is manual/on-demand. Opening a DevSpace must not automatically load Roslyn in this phase.

## Workspace Discovery

When initialization starts, discover a .NET workspace under the DevSpace root using this priority:

1. `.slnx`
2. `.sln`
3. `.csproj`

Prefer a workspace file in the DevSpace root. If no root-level candidate exists, search descendants while excluding `.git`, `bin`, `obj`, `node_modules`, `.vs`, `.idea`, and `.vscode` directories.

Within the same priority level, choose the candidate with the shortest relative path; break ties using ordinal path ordering so discovery is deterministic.

If no supported .NET workspace exists, initialization transitions to `Failed` with a user-readable reason.

## Architecture

Add a DevSpace-scoped Roslyn integration service with a narrow public contract, for example `IRoslynDevSpaceService` / `RoslynDevSpaceService`.

Responsibilities:

- own the Roslyn/MSBuild workspace lifecycle for one DevSpace;
- discover the workspace file;
- register MSBuild once per process using `Microsoft.Build.Locator`;
- create and load `MSBuildWorkspace` asynchronously;
- expose current state, failure text, and the loaded Roslyn `Solution`/`Workspace` to future consumers;
- dispose the Roslyn workspace when the DevSpace/dashboard owner is disposed;
- serialize initialization so repeated clicks cannot create concurrent workspaces.

Do not put Roslyn loading logic directly in the Avalonia view or dashboard view model.

## Dependencies

Add the minimum packages required for real Roslyn workspace loading:

- `Microsoft.CodeAnalysis.Workspaces.MSBuild`
- `Microsoft.Build.Locator`

Version selection must be compatible with the repository's .NET 10 target and existing build/publish configuration.

Because DevBoard uses trimmed/AOT release builds, implementation must verify that the chosen Roslyn/MSBuild integration is compatible with release packaging. If Roslyn requires dynamic loading/reflection that conflicts with NativeAOT, scope the first implementation to supported runtime configurations and surface that explicitly rather than faking availability.

## Dashboard Integration

Replace the hard-coded `RoslynCapability` value with observable state sourced from the Roslyn service.

The view model exposes:

- Roslyn state/status text;
- whether Initialize/Retry is available;
- failure detail;
- an async initialization command/method.

The Avalonia view adds the action button to the existing Roslyn Workspace Health row and binds visibility/enabled state to the view model. The code-behind may forward the click, but all state changes live in the view model/service.

The existing Quick Start warning should reflect the actual state rather than permanently saying Roslyn integration is absent.

## State Machine

Valid transitions:

- `Unavailable -> Initializing`
- `Initializing -> Available`
- `Initializing -> Failed`
- `Failed -> Initializing` on Retry

`Available` is only reached after Roslyn successfully loads at least one project from the selected solution/project.

A second initialization request while `Initializing` is in progress must return/await the same in-flight task. It must not create a second `MSBuildWorkspace`.

## Error Handling

Failures must not crash the DevSpace UI. Capture expected initialization failures and expose concise messages, including:

- no `.slnx`, `.sln`, or `.csproj` found;
- .NET SDK/MSBuild not discoverable;
- MSBuild registration failure;
- workspace load failure;
- solution/project loads zero projects.

Diagnostic details may be logged, while the UI shows a concise reason.

## Testing

Follow TDD for behavior changes.

Unit tests should cover:

- discovery priority: `.slnx` before `.sln` before `.csproj`;
- root-level candidates preferred over nested candidates;
- deterministic tie-breaking for multiple candidates;
- ignored/generated directories are skipped;
- state starts as `Unavailable`;
- initialization transitions through `Initializing` to `Available` on success;
- failures transition to `Failed` with a reason;
- Retry can transition from `Failed` back to `Initializing` and then `Available`;
- duplicate initialization returns the same in-flight task and does not create concurrent workspaces;
- dashboard bindings/action availability reflect Roslyn service state.

Add an integration test that loads a tiny temporary .NET project/solution with the real Roslyn/MSBuild path where the CI/runtime environment supports it. If that cannot run in every CI job, isolate it with a clearly documented condition rather than weakening unit coverage.

## Non-Goals

This change does not add or remap keyboard shortcuts. `Ctrl+T` remains New Tab.

This change does not yet implement full Roslyn symbol search, refactoring, code completion, or automatic initialization. It only establishes the real workspace lifecycle needed by diagnostics and future Roslyn-backed features.

## Acceptance Criteria

- Roslyn is no longer permanently hard-coded as unavailable.
- Workspace Health shows `Initialize` when Roslyn has not been initialized.
- Clicking `Initialize` performs real workspace discovery and Roslyn/MSBuild loading asynchronously.
- UI shows `Initializing`, `Available`, or `Failed` based on the real result.
- Failed initialization exposes a useful reason and a `Retry` action.
- Roslyn reaches `Available` only when a real project is loaded.
- Initialization does not block the UI thread.
- Existing Ctrl+T New Tab behavior is unchanged.
- Relevant unit tests pass, and release/AOT compatibility is explicitly verified before claiming the feature complete.
