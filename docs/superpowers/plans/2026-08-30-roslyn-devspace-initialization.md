# Roslyn DevSpace Initialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a real, user-triggered Roslyn initialization action to DevSpace Workspace Health and report real loading state/failures.

**Architecture:** A DevSpace-scoped `RoslynDevSpaceService` owns deterministic workspace discovery and an `MSBuildWorkspace`. The dashboard observes that service and forwards Initialize/Retry actions; the Avalonia view only renders state and forwards clicks. MSBuild registration is process-wide and idempotent.

**Tech Stack:** .NET 10, Avalonia 11, CommunityToolkit.Mvvm, Microsoft.CodeAnalysis.Workspaces.MSBuild, Microsoft.Build.Locator, xUnit

**Spec:** `docs/superpowers/specs/2026-08-30-roslyn-devspace-initialization-design.md`

## Global Constraints

- Initialization is manual/on-demand; opening a DevSpace must not automatically load Roslyn.
- Workspace discovery priority is `.slnx`, then `.sln`, then `.csproj`; root candidates beat nested candidates and selection within each class is ordinal-path deterministic.
- Ignore `.git`, `bin`, `obj`, `node_modules`, `.vs`, `.idea`, and `.vscode` during nested discovery.
- Valid states are `Unavailable`, `Initializing`, `Available`, and `Failed`.
- `Available` is reached only after a real Roslyn workspace contains at least one project.
- Concurrent Initialize calls must await the same in-flight task.
- `Ctrl+T` remains New Tab; no shortcut remapping is part of this work.
- Release/AOT compatibility must be verified before claiming the feature complete.

---

### Task 1: Deterministic .NET workspace discovery

**Files:**
- Create: `src/DevSpaces/RoslynWorkspaceDiscovery.cs`
- Test: `tests/DevBoard.Tests/RoslynWorkspaceDiscoveryTests.cs`

**Interfaces:**
- Produces: `internal static string RoslynWorkspaceDiscovery.FindWorkspace(string workspaceRoot)` returning the selected absolute path or `null` when no candidate exists.

- [ ] **Step 1: Write failing discovery tests**

Create tests using a temporary directory. Cover: root `.slnx` wins over root `.sln`/`.csproj`; root candidate wins over nested higher-priority candidate; nested `.slnx` wins when no root candidate exists; ignored directories are skipped; same-extension candidates choose `StringComparer.Ordinal` path order; no candidate returns null.

```csharp
[Fact]
public void FindWorkspace_PrefersRootSlnx()
{
    using var dir = new TempDirectory();
    File.WriteAllText(Path.Combine(dir.Path, "z.csproj"), "<Project />");
    File.WriteAllText(Path.Combine(dir.Path, "a.sln"), string.Empty);
    var slnx = Path.Combine(dir.Path, "workspace.slnx");
    File.WriteAllText(slnx, "<Solution />");

    Assert.Equal(slnx, RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
}
```

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter RoslynWorkspaceDiscoveryTests`

Expected: FAIL because `RoslynWorkspaceDiscovery` does not exist.

- [ ] **Step 3: Implement minimal deterministic discovery**

Implement root-first selection, then recursive traversal that prunes ignored directory names. For each scope, order candidate classes `.slnx`, `.sln`, `.csproj`, and order paths with `StringComparer.Ordinal` before choosing the first.

```csharp
internal static class RoslynWorkspaceDiscovery
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", ".vs", ".idea", ".vscode"
    };

    public static string FindWorkspace(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return null;

        var rootFiles = Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.TopDirectoryOnly).ToArray();
        var root = Select(rootFiles);
        if (root != null)
            return root;

        var nested = EnumerateDescendants(workspaceRoot).ToArray();
        return Select(nested);
    }

    private static string Select(IEnumerable<string> paths)
    {
        foreach (var extension in new[] { ".slnx", ".sln", ".csproj" })
        {
            var match = paths.Where(x => string.Equals(Path.GetExtension(x), extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match != null)
                return match;
        }
        return null;
    }
}
```

`EnumerateDescendants` must recursively enumerate files without entering any directory in `IgnoredDirectories`.

- [ ] **Step 4: Run tests and verify GREEN**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter RoslynWorkspaceDiscoveryTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/DevSpaces/RoslynWorkspaceDiscovery.cs tests/DevBoard.Tests/RoslynWorkspaceDiscoveryTests.cs
git commit -m "feat: discover Roslyn DevSpace workspaces"
```

---

### Task 2: Roslyn lifecycle state and loader abstraction

**Files:**
- Create: `src/DevSpaces/RoslynDevSpaceState.cs`
- Create: `src/DevSpaces/IRoslynWorkspaceLoader.cs`
- Create: `src/DevSpaces/RoslynDevSpaceService.cs`
- Test: `tests/DevBoard.Tests/RoslynDevSpaceServiceTests.cs`

**Interfaces:**
- Produces: `enum RoslynDevSpaceState { Unavailable, Initializing, Available, Failed }`.
- Produces: `IRoslynWorkspaceLoader.LoadAsync(string workspacePath, CancellationToken cancellationToken)` returning a disposable loaded-workspace handle whose `ProjectCount` is available.
- Produces: `RoslynDevSpaceService.InitializeAsync(CancellationToken cancellationToken = default)`, `State`, `FailureReason`, `WorkspacePath`, `PropertyChanged`, and `Dispose()`.

- [ ] **Step 1: Write failing lifecycle tests with a fake loader**

Tests must assert initial `Unavailable`; success observes `Initializing` then `Available`; loader exception becomes `Failed` with concise reason; zero projects becomes `Failed`; Retry after failure can become `Available`; two concurrent calls invoke loader exactly once and return/await the same in-flight operation.

```csharp
[Fact]
public async Task InitializeAsync_Success_TransitionsToAvailable()
{
    var loader = new FakeRoslynWorkspaceLoader(projectCount: 1);
    using var service = new RoslynDevSpaceService(_root, loader, _ => _workspaceFile);
    var states = new List<RoslynDevSpaceState>();
    service.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(service.State)) states.Add(service.State);
    };

    await service.InitializeAsync();

    Assert.Contains(RoslynDevSpaceState.Initializing, states);
    Assert.Equal(RoslynDevSpaceState.Available, service.State);
}
```

- [ ] **Step 2: Run lifecycle tests and verify RED**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter RoslynDevSpaceServiceTests`

Expected: FAIL because lifecycle types do not exist.

- [ ] **Step 3: Implement the state machine and in-flight task serialization**

`InitializeAsync` must lock only long enough to create/read `_initializationTask`; the actual load runs asynchronously. On retry, clear `FailureReason`, transition to `Initializing`, discover the path, load it, reject zero projects, then transition to `Available`. Catch expected exceptions into `Failed` without throwing into the UI event handler. Dispose a replaced/failed handle and the final handle on service disposal.

Use an injected `Func<string,string> discovery` in the internal testable constructor and default it to `RoslynWorkspaceDiscovery.FindWorkspace` in production.

- [ ] **Step 4: Run lifecycle tests and verify GREEN**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter RoslynDevSpaceServiceTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/DevSpaces/RoslynDevSpaceState.cs src/DevSpaces/IRoslynWorkspaceLoader.cs src/DevSpaces/RoslynDevSpaceService.cs tests/DevBoard.Tests/RoslynDevSpaceServiceTests.cs
git commit -m "feat: add Roslyn DevSpace lifecycle"
```

---

### Task 3: Real MSBuildWorkspace loader

**Files:**
- Modify: `src/DevBoard.csproj`
- Create: `src/DevSpaces/MSBuildRoslynWorkspaceLoader.cs`
- Test: `tests/DevBoard.Tests/MSBuildRoslynWorkspaceLoaderTests.cs`

**Interfaces:**
- Consumes: `IRoslynWorkspaceLoader` from Task 2.
- Produces: production `MSBuildRoslynWorkspaceLoader`, process-wide idempotent MSBuild registration, and a loaded handle retaining `MSBuildWorkspace` plus its current `Solution` and `ProjectCount`.

- [ ] **Step 1: Add dependencies and write a real-loader integration test**

Add compatible stable package references for `Microsoft.CodeAnalysis.Workspaces.MSBuild` and `Microsoft.Build.Locator`. The integration test creates a tiny SDK-style temporary project with `Program.cs`, calls the loader, and asserts `ProjectCount == 1`.

```csharp
File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
File.WriteAllText(Path.Combine(root, "Program.cs"), "Console.WriteLine(\"ok\");");
using var loaded = await new MSBuildRoslynWorkspaceLoader().LoadAsync(projectPath, CancellationToken.None);
Assert.Equal(1, loaded.ProjectCount);
```

- [ ] **Step 2: Run loader test and verify RED**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter MSBuildRoslynWorkspaceLoaderTests`

Expected: FAIL because the loader does not exist.

- [ ] **Step 3: Implement MSBuild registration and loading**

Register once under a static lock:

```csharp
if (!MSBuildLocator.IsRegistered)
{
    var instance = MSBuildLocator.QueryVisualStudioInstances()
        .OrderByDescending(x => x.Version)
        .FirstOrDefault() ?? throw new InvalidOperationException("No compatible .NET SDK/MSBuild installation was found.");
    MSBuildLocator.RegisterInstance(instance);
}
```

Create `MSBuildWorkspace`. For `.csproj`, call `OpenProjectAsync`; for `.sln`/`.slnx`, call `OpenSolutionAsync`. Retain the workspace until the returned handle is disposed. Translate workspace-load failures to useful `InvalidOperationException` messages while preserving the original exception as `InnerException`.

- [ ] **Step 4: Run loader integration test and verify GREEN**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter MSBuildRoslynWorkspaceLoaderTests`

Expected: PASS on a development/CI host with .NET SDK/MSBuild available.

- [ ] **Step 5: Run all Roslyn tests**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter "Roslyn"`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/DevBoard.csproj src/DevSpaces/MSBuildRoslynWorkspaceLoader.cs tests/DevBoard.Tests/MSBuildRoslynWorkspaceLoaderTests.cs
git commit -m "feat: load DevSpaces with Roslyn MSBuildWorkspace"
```

---

### Task 4: Bind Roslyn lifecycle into DevSpaceDashboard

**Files:**
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Test: `tests/DevBoard.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Consumes: `RoslynDevSpaceService.State`, `FailureReason`, `InitializeAsync`.
- Produces view-model properties: `RoslynState`, `RoslynStatusText`, `RoslynFailureReason`, `CanInitializeRoslyn`, `IsRoslynInitializing`, and `InitializeRoslynAsync()`.

- [ ] **Step 1: Write failing dashboard tests**

Inject a Roslyn service/factory through an internal constructor seam. Assert initial status text is `Unavailable` and Initialize is enabled; `Initializing` disables the action; `Failed` exposes failure reason and re-enables the action as Retry; `Available` hides/disables the action. Assert `Dispose()` disposes/unsubscribes the Roslyn service.

- [ ] **Step 2: Run dashboard tests and verify RED**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter DevSpacesDashboardTests`

Expected: FAIL on missing Roslyn dashboard members.

- [ ] **Step 3: Replace hard-coded capability with observable service state**

Remove `public DevSpaceCapabilityState RoslynCapability { get; } = DevSpaceCapabilityState.Unavailable;`. Construct a production `RoslynDevSpaceService(WorkspacePath, new MSBuildRoslynWorkspaceLoader())`; subscribe to its `PropertyChanged`; map state to status text and action properties; implement `InitializeRoslynAsync` as a simple await of the service.

Use exact English status values `Unavailable`, `Initializing…`, `Available`, and `Failed` for this phase; localization can wrap these strings if the repository's existing resource convention requires it.

- [ ] **Step 4: Run dashboard tests and verify GREEN**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter DevSpacesDashboardTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ViewModels/DevSpaceDashboard.cs tests/DevBoard.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: expose Roslyn state in DevSpace dashboard"
```

---

### Task 5: Add Initialize/Retry action to Workspace Health

**Files:**
- Modify: `src/Views/DevSpaceDashboard.axaml`
- Modify: `src/Views/DevSpaceDashboard.axaml.cs`
- Test: `tests/DevBoard.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Consumes: Task 4 dashboard properties and `InitializeRoslynAsync()`.
- Produces: Roslyn Workspace Health row with status, Initialize/Retry action, disabled progress state, and failure tooltip/detail.

- [ ] **Step 1: Add a failing UI/view-model contract test**

Extend the dashboard test to verify the action label maps to `Initialize` for `Unavailable`, `Retry` for `Failed`, and is not actionable for `Available`/`Initializing`. Expose `RoslynActionText` from the view model to keep XAML simple.

- [ ] **Step 2: Run test and verify RED**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter DevSpacesDashboardTests`

Expected: FAIL because `RoslynActionText` is missing.

- [ ] **Step 3: Implement the Roslyn health row**

Replace the current two-column Roslyn grid with a three-column row (`*,Auto,Auto`). Bind status text, show a compact button when `CanInitializeRoslyn`, bind button text to `RoslynActionText`, and bind tooltip to `RoslynFailureReason`. Add `OnInitializeRoslyn` as `async void` code-behind that awaits `Model.InitializeRoslynAsync()` and marks the routed event handled.

Also replace the permanent Quick Start `RoslynUnavailable` warning with text bound to the current state/failure so it no longer claims integration is absent after success.

- [ ] **Step 4: Run dashboard tests and build**

Run:

```bash
dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter DevSpacesDashboardTests
dotnet build
```

Expected: tests PASS and build succeeds with no new warnings.

- [ ] **Step 5: Commit**

```bash
git add src/Views/DevSpaceDashboard.axaml src/Views/DevSpaceDashboard.axaml.cs src/ViewModels/DevSpaceDashboard.cs tests/DevBoard.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: initialize Roslyn from Workspace Health"
```

---

### Task 6: Verify full suite and release/AOT packaging

**Files:**
- Modify only if verification exposes a Roslyn-specific packaging requirement: `src/DevBoard.csproj`
- Modify only if necessary to document a verified platform limitation: `docs/superpowers/specs/2026-08-30-roslyn-devspace-initialization-design.md`

**Interfaces:**
- Consumes all previous tasks.
- Produces evidence that Debug tests/build and Release publish remain valid, or an explicit supported-runtime guard if Roslyn cannot run under NativeAOT.

- [ ] **Step 1: Run focused Roslyn tests**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter "Roslyn"`

Expected: PASS.

- [ ] **Step 2: Run complete test suite**

Run: `dotnet test`

Expected: PASS with no new warnings introduced by this feature.

- [ ] **Step 3: Run normal build**

Run: `dotnet build`

Expected: succeeds.

- [ ] **Step 4: Verify Release publish/AOT path**

Run the repository's existing Release publish command/CI-equivalent for the host RID. If no wrapper exists, on Windows x64 run:

```bash
dotnet publish src/DevBoard.csproj -c Release -r win-x64 --self-contained true
```

Expected: publish succeeds and the application can initialize the tiny/real DevBoard workspace with Roslyn. If NativeAOT cannot support `MSBuildWorkspace`, do not fake success: add an explicit runtime capability guard that leaves Roslyn `Unavailable` with a precise reason on unsupported builds, then add a test for that guard and rerun Steps 1–4.

- [ ] **Step 5: Manually smoke-test the approved UX**

Open a .NET DevSpace. Verify Workspace Health starts `Roslyn  Unavailable  Initialize`; clicking Initialize changes to `Initializing…`; success becomes `Available`; a non-.NET folder becomes `Failed` with a useful reason and `Retry`; repeated clicks do not duplicate loading; Ctrl+T still opens a New Tab.

- [ ] **Step 6: Commit any verification-only fixes**

```bash
git add src/DevBoard.csproj docs/superpowers/specs/2026-08-30-roslyn-devspace-initialization-design.md tests/DevBoard.Tests
git commit -m "fix: support Roslyn DevSpace release packaging"
```

Skip this commit when verification required no changes.

---

### Task 7: Final review and PR

**Files:**
- Review all files changed from `master...feat/roslyn-devspace-initialization`.

**Interfaces:**
- Produces a reviewable PR containing the design, plan, implementation, tests, and verification evidence.

- [ ] **Step 1: Inspect the complete diff**

Run: `git diff master...HEAD --check` and `git diff --stat master...HEAD`.

Expected: no whitespace errors; changes are limited to the Roslyn feature/design/plan/tests.

- [ ] **Step 2: Re-run completion verification**

Run:

```bash
dotnet test
dotnet build
```

Expected: both succeed. Do not claim completion from earlier output.

- [ ] **Step 3: Create the PR**

Create a PR from `feat/roslyn-devspace-initialization` to `master` titled `feat: initialize Roslyn in DevSpaces`. Include the UX states, real MSBuildWorkspace loading, test evidence, and Release/AOT result in the PR body.
