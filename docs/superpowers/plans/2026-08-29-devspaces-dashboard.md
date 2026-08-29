# DevSpaces Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a first-class DevSpaces Dashboard that summarizes the active repository/worktree, existing DevSpace sessions, Git changes, Roslyn state, quick-start actions, and recent DevSpaces activity without duplicating terminal, Files, Working Copy, or Roslyn ownership.

**Architecture:** Extend the existing path-scoped `ViewModels.DevSpaces` owner with an internal page enum and one `DevSpaceDashboard` child view model. The dashboard projects existing session/Git/Roslyn state into lightweight immutable summaries and delegates all launches/navigation back to existing DevSpaces/SourceGit flows; it never owns PTYs, Git polling, Files tree state, or Roslyn sidecars.

**Tech Stack:** .NET 10, C#, Avalonia 11.x, CommunityToolkit.Mvvm, xUnit, existing SourceGit DevSpaces/Git/Roslyn models.

**Spec:** `docs/superpowers/specs/2026-08-29-devspaces-dashboard-design.md`

## Global Constraints

- Dashboard is a DevSpaces control center, not a second IDE.
- Preserve the existing repository/worktree-path ownership in `DevSpaceRegistry`.
- Preserve mounted terminal controls and PTY/TUI lifetime across Dashboard, Files, and Terminals navigation.
- Do not add a second `git status` polling loop, filesystem watcher, Roslyn sidecar, terminal backend, or repository-level workspace owner.
- Dashboard is the default DevSpaces internal page, while the existing first-session behavior still creates the first terminal exactly once when DevSpaces first becomes active.
- Roslyn and AI CLI availability are optional capability states and must never break the rest of Dashboard.
- Recent Activity is in-memory only, per worktree, capped at 20 entries.
- Reuse SourceGit dynamic theme resources and localization; do not introduce a dashboard-specific theme.
- Keep `Ctrl/Cmd+P` Go to File behavior unchanged.
- Tests use the existing `tests/SourceGit.Tests` xUnit project targeting `net10.0`.

---

## File Structure

**Create**
- `src/Models/DevSpacePage.cs` — internal DevSpaces page enum.
- `src/ViewModels/DevSpaceDashboard.cs` — dashboard projection, activity, health, and delegated actions.
- `src/ViewModels/DevSpaceDashboardModels.cs` — immutable dashboard row/summary records and capability state.
- `src/DevSpaces/IDevSpaceRoslynStatusProvider.cs` — optional Roslyn status/action adapter that keeps Dashboard independent of a concrete Roslyn implementation.
- `src/DevSpaces/NullDevSpaceRoslynStatusProvider.cs` — neutral provider used when Roslyn is unavailable.
- `src/Views/DevSpaceDashboard.axaml` — responsive dashboard cards.
- `src/Views/DevSpaceDashboard.axaml.cs` — thin interaction/navigation bridge only where bindings are insufficient.
- `tests/SourceGit.Tests/DevSpacesDashboardTests.cs` — page/session/activity/quick-start/isolation tests.
- `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs` — pure aggregation tests.

**Modify**
- `src/ViewModels/DevSpaces.cs` — own `ActivePage` and `Dashboard`, migrate `IsFilesActive` to page-derived state, expose navigation helpers.
- `src/Views/DevSpaces.axaml` — add Dashboard/Files/Terminals/Roslyn internal navigation and host the dashboard without unloading terminal controls.
- `src/Views/DevSpaces.axaml.cs` — switch page visibility/input state without recreating terminal surfaces.
- `src/DevSpaces/DevSpaceRegistry.cs` — pass the owning repository into the DevSpaces model while preserving path keying.
- `src/Resources/Locales/DevSpaces.axaml` — Dashboard localization keys used by DevSpaces resources.
- Existing locale resources only where SourceGit's localization validation requires matching keys.

---

### Task 1: Introduce the DevSpaces internal page model

**Files:**
- Create: `src/Models/DevSpacePage.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces: `Models.DevSpacePage { Dashboard, Files, Terminals, Roslyn }`.
- Produces: `ViewModels.DevSpaces.ActivePage`, `IsDashboardActive`, `IsFilesActive`, `IsTerminalsActive`, `IsRoslynActive`.
- Produces: `ActivateDashboard()`, `ActivateFiles()`, `ActivateTerminals()`, `ActivateRoslyn()`.

- [ ] **Step 1: Write failing page-state tests**

Create `DevSpacesDashboardTests.cs` with tests that construct `new ViewModels.DevSpaces(tempPath, fakeLauncher)` and assert:

```csharp
Assert.Equal(Models.DevSpacePage.Dashboard, spaces.ActivePage);
Assert.True(spaces.IsDashboardActive);
Assert.False(spaces.IsFilesActive);

spaces.ActivateFiles();
Assert.Equal(Models.DevSpacePage.Files, spaces.ActivePage);

spaces.ActivateTerminals();
Assert.Equal(Models.DevSpacePage.Terminals, spaces.ActivePage);
```

Also assert `OpenFile(relativePath)` changes `ActivePage` to `Files` and does not modify `Sessions`.

- [ ] **Step 2: Run the focused test and verify RED**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpacesDashboardTests
```

Expected: compile/test failure because `DevSpacePage` and `ActivePage` do not exist.

- [ ] **Step 3: Implement the enum and single page source of truth**

Add:

```csharp
namespace SourceGit.Models;

public enum DevSpacePage
{
    Dashboard,
    Files,
    Terminals,
    Roslyn,
}
```

In `ViewModels.DevSpaces`, replace `_isFilesActive` as the authoritative state with:

```csharp
public Models.DevSpacePage ActivePage
{
    get => _activePage;
    private set
    {
        if (!SetProperty(ref _activePage, value))
            return;

        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsFilesActive));
        OnPropertyChanged(nameof(IsTerminalsActive));
        OnPropertyChanged(nameof(IsRoslynActive));
    }
}

public bool IsDashboardActive => ActivePage == Models.DevSpacePage.Dashboard;
public bool IsFilesActive => ActivePage == Models.DevSpacePage.Files;
public bool IsTerminalsActive => ActivePage == Models.DevSpacePage.Terminals;
public bool IsRoslynActive => ActivePage == Models.DevSpacePage.Roslyn;
```

Initialize `_activePage = Models.DevSpacePage.Dashboard` and make `ActivateTerminal(...)` / terminal creation select `Terminals`, while `OpenFile(...)` selects `Files`.

Do **not** move `EnsureFirstSession()` into Dashboard; it remains invoked by the existing DevSpaces outer activation path.

- [ ] **Step 4: Re-run focused tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Models/DevSpacePage.cs src/ViewModels/DevSpaces.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: add DevSpaces internal page state"
```

---

### Task 2: Add lightweight dashboard summary models and bounded activity

**Files:**
- Create: `src/ViewModels/DevSpaceDashboardModels.cs`
- Create: `src/ViewModels/DevSpaceDashboard.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Test: `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs`
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces: `DevSpaceDashboardSessionRow`, `DevSpaceGitSummary`, `DevSpaceActivityEntry`, `DevSpaceActivityKind`.
- Produces: `DevSpaceCapabilityState { Checking, Available, Unavailable, Failed }`.
- Produces: `DevSpaceDashboard.Activity`, capped at 20, newest first.
- Produces: `DevSpaceDashboard.AddActivity(DevSpaceActivityKind kind, string text, DateTimeOffset? at = null)`.

- [ ] **Step 1: Write failing pure summary/activity tests**

Cover status count aggregation for Added/Modified/Deleted/Renamed and staged/unstaged flags, activity insert order, a 20-entry cap, and independent activity lists for two separate `DevSpaces` instances.

- [ ] **Step 2: Run focused tests and verify RED**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "DevSpaceDashboard"
```

Expected: missing types/properties.

- [ ] **Step 3: Implement immutable rows and dashboard child ownership**

Use data-only records/enums:

```csharp
public enum DevSpaceCapabilityState
{
    Checking,
    Available,
    Unavailable,
    Failed,
}

public sealed record DevSpaceGitSummary(
    int Total,
    int Added,
    int Modified,
    int Deleted,
    int Renamed,
    int Staged,
    int Unstaged);

public sealed record DevSpaceActivityEntry(
    DevSpaceActivityKind Kind,
    string Text,
    DateTimeOffset At);
```

`DevSpaceDashboard` receives the owning `DevSpaces` and workspace path, owns an `AvaloniaList<DevSpaceActivityEntry>`, inserts newest entries at index 0, and removes the last item while count exceeds 20.

Instantiate exactly one Dashboard from the `DevSpaces` constructor. Dispose Dashboard subscriptions from `DevSpaces.Dispose()` without giving Dashboard terminal-disposal ownership.

- [ ] **Step 4: Feed session lifecycle into Recent Activity**

Add activity after existing terminal/session mutations succeed. Subscribe only to session metadata required for projection; never create terminal surfaces from Dashboard.

- [ ] **Step 5: Run focused tests and commit**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "DevSpaceDashboard"
git add src/ViewModels/DevSpaceDashboard.cs src/ViewModels/DevSpaceDashboardModels.cs src/ViewModels/DevSpaces.cs tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: add DevSpaces dashboard state model"
```

---

### Task 3: Add delegated Dashboard navigation and Quick Start actions

**Files:**
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Modify: `src/DevSpaces/DevSpaceAgent.cs`
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces: `OpenSession(DevSpaceTerminal)`, `OpenFiles()`, `StartDefaultTerminal()`, `StartProfile(DevSpaceTerminalProfile)`, `StartAgent(DevSpaceAgent)`, `CloseAllSessions()`.
- Consumes: existing terminal/profile launch paths, `ActivateTerminal`, and `StopAll`.

- [ ] **Step 1: Write failing delegation tests with a fake launcher**

Implement a test launcher for `IDevSpaceSessionLauncher` that records launches. Assert default-terminal launch, profile startup command/path behavior, built-in Copilot/Codex/Antigravity mapping, exact-object session activation, and Close All delegation.

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpacesDashboardTests
```

- [ ] **Step 3: Implement only delegation methods**

```csharp
public void OpenSession(DevSpaceTerminal terminal)
{
    _owner.ActivateTerminal(terminal);
}

public DevSpaceTerminal StartDefaultTerminal()
{
    var terminal = _owner.CreateTerminal();
    _owner.ActivateTerminals();
    return terminal;
}
```

Expose/reuse one built-in agent lookup from `DevSpaceAgent.cs`; do not duplicate command strings in Dashboard.

- [ ] **Step 4: Run focused tests and commit**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpacesDashboardTests
git add src/ViewModels/DevSpaceDashboard.cs src/ViewModels/DevSpaces.cs src/DevSpaces/DevSpaceAgent.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: add DevSpaces dashboard quick actions"
```

---

### Task 4: Wire repository/Git/worktree summary without duplicate polling

**Files:**
- Modify: `src/DevSpaces/DevSpaceRegistry.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Test: `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs`

**Interfaces:**
- `DevSpaces` retains the owning `ViewModels.Repository` reference in addition to `FullPath`.
- Dashboard exposes workspace name/path/current branch/base branch/ahead-behind/Git summary as bindable properties.

- [ ] **Step 1: Write failing summary tests around explicit repository/change inputs**

Avoid shelling out to Git. Test the aggregation helper with representative status objects, including rename and mixed staged/unstaged state.

- [ ] **Step 2: Update registry/model construction**

Change entry creation from `new ViewModels.DevSpaces(repository.FullPath)` to an overload that also receives `repository`, while keeping `_spaces` keyed by `repository.FullPath`.

Keep the existing path-only constructor for tests and forward it to the new overload with `repository: null`.

- [ ] **Step 3: Subscribe to existing repository notifications**

Project values already held/refreshed by SourceGit. Do not add a timer or continuous Git invocation. Reuse the existing worktree base-branch capability; expose null/empty when unavailable.

- [ ] **Step 4: Implement `RefreshGitSummary()` as a pure projection**

Map current working-copy/status collections to `DevSpaceGitSummary` and notify changed properties only.

- [ ] **Step 5: Run focused tests and commit**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpaceDashboardSummaryTests
git add src/DevSpaces/DevSpaceRegistry.cs src/ViewModels/DevSpaces.cs src/ViewModels/DevSpaceDashboard.cs tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs
git commit -m "feat: summarize workspace state on DevSpaces dashboard"
```

---

### Task 5: Add optional Roslyn and tool-health capability providers

**Files:**
- Create: `src/DevSpaces/IDevSpaceRoslynStatusProvider.cs`
- Create: `src/DevSpaces/NullDevSpaceRoslynStatusProvider.cs`
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces:

```csharp
public interface IDevSpaceRoslynStatusProvider
{
    DevSpaceCapabilityState Capability { get; }
    string Target { get; }
    string AnalysisState { get; }
    int ErrorCount { get; }
    int WarningCount { get; }
    int InfoCount { get; }
    DateTimeOffset? LastAnalysisAt { get; }
    event EventHandler Changed;
    Task AnalyzeAsync(CancellationToken cancellationToken = default);
}
```

- `NullDevSpaceRoslynStatusProvider` returns `Unavailable`, zero counts, null metadata, and a completed `AnalyzeAsync`.
- `DevSpaces` accepts an optional provider and defaults to the null provider. A concrete Roslyn feature can supply an adapter later without changing Dashboard.

- [ ] **Step 1: Write failing capability tests**

Assert the null Roslyn provider produces a non-fatal unavailable state and that Dashboard remains usable. Add a fake Roslyn provider test that changes counts, raises `Changed`, and verifies Dashboard refreshes its Roslyn summary. Add tests for `Checking`, `Available`, `Unavailable`, and `Failed` tool-health states.

- [ ] **Step 2: Implement the provider contract and null provider**

The null provider must have no process launch, timer, or side effect.

- [ ] **Step 3: Add lazy cached AI CLI health checks**

Check each CLI at most once per DevSpaces lifetime, cache `DevSpaceCapabilityState`, and never run detection from a property getter/render loop.

- [ ] **Step 4: Project provider state into Dashboard**

Subscribe to `IDevSpaceRoslynStatusProvider.Changed`, copy target/count/state/time into bindable properties, and unsubscribe during Dashboard disposal. `AnalyzeAsync` delegates to the provider and then selects `DevSpacePage.Roslyn` only when capability is available.

- [ ] **Step 5: Run focused tests and commit**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpacesDashboardTests
git add src/DevSpaces/IDevSpaceRoslynStatusProvider.cs src/DevSpaces/NullDevSpaceRoslynStatusProvider.cs src/ViewModels/DevSpaceDashboard.cs src/ViewModels/DevSpaces.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: surface DevSpaces capability health"
```

---

### Task 6: Build the Dashboard UI while keeping terminal controls mounted

**Files:**
- Create: `src/Views/DevSpaceDashboard.axaml`
- Create: `src/Views/DevSpaceDashboard.axaml.cs`
- Modify: `src/Views/DevSpaces.axaml`
- Modify: `src/Views/DevSpaces.axaml.cs`

**Interfaces:**
- Consumes Task 1-5 dashboard properties/actions.
- Produces internal navigation: Dashboard, Files, Terminals, conditional Roslyn.

- [ ] **Step 1: Add internal page navigation**

Replace the current Files/session-tab-only top-bar behavior with compact Dashboard, Files, Terminals and conditional Roslyn navigation. Keep terminal session tabs/layout/+ controls visible on the Terminals page only.

- [ ] **Step 2: Host page surfaces without breaking terminal persistence**

Dashboard and Files may use normal visibility. The existing terminal tree must continue using the current mounted/opacity/input strategy: switching away must not remove or recreate terminal controls or PTYs.

- [ ] **Step 3: Create responsive Dashboard cards**

`DevSpaceDashboard.axaml` contains workspace header, Active Spaces, Quick Start, Git Changes, Roslyn Diagnostics, and Recent Activity. Use existing SourceGit layout/theme resources and stack cards at narrow width.

- [ ] **Step 4: Wire session row selection to the exact existing terminal**

Use binding/commands where possible. If code-behind is needed, pass the bound `DevSpaceTerminal` object to `OpenSession`; never clone it.

- [ ] **Step 5: Build and commit**

```bash
dotnet build src/SourceGit.csproj -c Debug
git add src/Views/DevSpaceDashboard.axaml src/Views/DevSpaceDashboard.axaml.cs src/Views/DevSpaces.axaml src/Views/DevSpaces.axaml.cs
git commit -m "feat: add DevSpaces dashboard UI"
```

Expected: no Avalonia XAML/binding compile errors.

---

### Task 7: Localize Dashboard strings and verify accessibility behavior

**Files:**
- Modify: `src/Resources/Locales/DevSpaces.axaml`
- Modify locale files required by the repository's localization validation.
- Modify: `src/Views/DevSpaceDashboard.axaml`
- Modify: `src/Views/DevSpaces.axaml`

**Interfaces:**
- Produces localized resources for Dashboard, Active Spaces, Quick Start, Workspace, Git Changes, Recent Activity, Workspace Health, states/actions/status labels.

- [ ] **Step 1: Add/reuse localization keys**

Prefer existing generic `Text.*` resources when wording already exists. Add DevSpaces-specific keys only when needed. Do not leave user-facing hard-coded English in Dashboard XAML.

- [ ] **Step 2: Add accessible labels/tooltips**

Copy Path, Open Folder, Close, Analyze and other icon-only controls receive localized tooltips/accessible text. Running/Exited/Failed are textual, not color-only.

- [ ] **Step 3: Verify keyboard navigation manually**

Keyboard through internal pages, Quick Start, session rows, Close All and card navigation. Confirm terminal focus/shortcuts are unaffected on Terminals.

- [ ] **Step 4: Run format/build and commit**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
git add src/Resources/Locales src/Views/DevSpaceDashboard.axaml src/Views/DevSpaces.axaml
git commit -m "feat: localize DevSpaces dashboard"
```

---

### Task 8: Complete regression tests and final verification

**Files:**
- Modify: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`
- Modify: `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs`
- Modify product files only for defects exposed by these tests.

**Interfaces:**
- Covers all V1 acceptance criteria from the spec that are testable without pixel/runtime-terminal validation.

- [ ] **Step 1: Complete acceptance coverage**

Ensure explicit tests exist for:
1. Dashboard default page.
2. Dashboard -> Files -> Terminals preserves the same session objects.
3. Dashboard session activation selects the same terminal reference.
4. Quick Start delegates to existing launcher/profile/agent paths.
5. Git summary counts are correct.
6. Different workspace paths keep independent dashboards/activity.
7. Activity cap is 20.
8. Dashboard disposal does not double-dispose terminal sessions.
9. Missing optional capabilities are non-fatal.
10. Existing layout/session behavior remains unchanged.

- [ ] **Step 2: Run full tests**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Run format verification**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
```

Expected: exit code 0.

- [ ] **Step 4: Run Release build**

```bash
dotnet build src/SourceGit.csproj -c Release
```

Expected: exit code 0.

- [ ] **Step 5: Perform manual DevSpaces acceptance**

On a real repository/worktree:
- open DevSpaces and confirm Dashboard appears first;
- verify the existing auto-created first terminal still exists once and is visible under Terminals;
- launch Copilot/Codex/Antigravity/default terminal/profile from Dashboard;
- switch Dashboard -> Files -> Terminals repeatedly and confirm every TUI retains state;
- open a second worktree tab and confirm independent dashboard/session/activity state;
- confirm Git counts update with working-copy changes;
- confirm Roslyn unavailable/failure does not affect terminals/Files;
- resize narrow/wide and verify cards stack cleanly;
- close the repository/worktree tab and confirm session cleanup still runs once.

- [ ] **Step 6: Inspect final diff**

```bash
git diff master...HEAD --check
git status --short
```

Expected: no whitespace errors; only Dashboard-related product/tests/localization/docs changes.

- [ ] **Step 7: Commit verification fixes if the previous steps changed code**

```bash
git add src tests
git commit -m "test: verify DevSpaces dashboard behavior"
```

---

## Final PR Acceptance Gate

Before opening/merging the implementation PR, require:

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj -c Release
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
```

and the repository's normal GitHub PR Check matrix on Windows x64/ARM64, Linux x64/ARM64, and macOS Intel/Apple Silicon where configured.

Do not claim terminal/TUI persistence purely from CI: complete the manual navigation smoke test because mounted native/Avalonia terminal behavior is runtime UI behavior.