# DevSpaces Native Terminal Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make embedded DevSpaces terminals feel like native desktop terminals for mouse selection, copy, paste, and keyboard selection while preserving terminal/TUI semantics and keeping SourceGit on Avalonia 11.3.20.

**Architecture:** Maintain a small public Avalonia-11 compatibility fork of `tomlm/Iciclecreek.Avalonia.Terminal`, based on upstream `main11` at `3da5aad71e02517afa40f187461349ffafb2497b`. Backport only the upstream input/selection/clipboard behavior required by the approved spec, cover it with tests in the fork, then consume the fork from SourceGit as `depends/Iciclecreek.Avalonia.Terminal` via `ProjectReference`. SourceGit owns only DevSpaces-specific context-menu wiring and terminal configuration; selection algorithms and clipboard primitives remain inside the terminal library.

**Tech Stack:** .NET 8 compatibility library, .NET 10 SourceGit host, Avalonia 11.3.20, XTerm.NET 1.x, Porta.Pty 1.x, NUnit 4, Avalonia.Headless.NUnit 11.3.20, Git submodules, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- Keep SourceGit on Avalonia `11.3.20`; do not migrate any SourceGit project to Avalonia 12.
- Base the terminal compatibility fork on upstream `main11` commit `3da5aad71e02517afa40f187461349ffafb2497b` (upstream version 1.0.12, `net8.0`, Avalonia 11.3.14), not upstream `main`.
- Align the compatibility fork's Avalonia packages to `11.3.20`, but do not upgrade Porta.Pty/XTerm.NET merely because newer upstream `main` does.
- Do not copy terminal source into SourceGit. The approved boundary is a public fork plus git submodule.
- Do not use reflection from SourceGit into private terminal fields. Add stable public APIs to the fork instead.
- Preserve `Ctrl+C` as process interrupt when no selection exists. Preserve `Ctrl+A` as application-owned on Windows/Linux.
- Preserve alternate-screen/TUI input ownership and terminal mouse-reporting behavior.
- Do not change Copilot CLI session-ID persistence in this milestone.
- Do not regress the already-merged DevSpaces rule that an existing terminal control stays parented for its full session lifetime.
- Terminal-fork behavior changes require automated tests. SourceGit itself still has no DevSpaces test project, so SourceGit verification is its existing multi-platform PR Check plus the required manual Copilot acceptance.
- Do not claim runtime/native-feel completion until the manual Windows Copilot acceptance in Task 8 has been performed.

---

## Task 1: Bootstrap the Avalonia-11 compatibility fork and test harness

**Repository:** `dhhieu113pro/Iciclecreek.Avalonia.Terminal`

**Prerequisite:** This public fork does not currently exist, and the connected GitHub actions available in this session cannot create or fork repositories. Before implementation starts, the user must create a public fork of `tomlm/Iciclecreek.Avalonia.Terminal` at `dhhieu113pro/Iciclecreek.Avalonia.Terminal`. If that repository is still missing, STOP here and ask the user to create it; do not vendor the source or substitute another person's fork.

**Files:**
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TestAppBuilder.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TerminalControlSmokeTests.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow.slnx`
- Create: `SOURCEGIT-COMPAT.md`

- [ ] **Step 1: Create the compatibility branch from the exact upstream Avalonia-11 line**

Use branch name:

```text
sourcegit/avalonia11-native-input
```

Its first parent must be upstream `main11` commit:

```text
3da5aad71e02517afa40f187461349ffafb2497b
```

Verify before editing:

```bash
git rev-parse HEAD
```

Expected:

```text
3da5aad71e02517afa40f187461349ffafb2497b
```

- [ ] **Step 2: Align Avalonia packages to SourceGit's 11.3.20 line without changing the PTY/emulator generation**

In `src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj`, keep:

```xml
<TargetFramework>net8.0</TargetFramework>
<PackageReference Include="Porta.Pty" Version="1.0.7" />
<PackageReference Include="XTerm.NET" Version="1.0.12" />
```

Change only the Avalonia package reference to:

```xml
<PackageReference Include="Avalonia" Version="11.3.20" />
```

Do not import the Avalonia-12 API changes from upstream `main`.

- [ ] **Step 3: Add the minimal Avalonia-11 headless test project**

Create `src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.5.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="Avalonia.Headless.NUnit" Version="11.3.20" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.20" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Iciclecreek.Avalonia.TerminalWindow\Iciclecreek.Avalonia.Terminal.csproj" />
  </ItemGroup>
</Project>
```

Create `src/Iciclecreek.Avalonia.Terminal.Tests/TestAppBuilder.cs` using the upstream test-harness pattern, adapted to Avalonia 11.3.20:

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Iciclecreek.Terminal.Tests.TestAppBuilder))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace Iciclecreek.Terminal.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .AfterSetup(builder => builder.Instance?.Styles.Add(new FluentTheme()));
}
```

- [ ] **Step 4: Add a baseline realization test before any backport**

Create `TerminalControlSmokeTests.cs`:

```csharp
using Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace Iciclecreek.Terminal.Tests;

[TestFixture]
public sealed class TerminalControlSmokeTests
{
    [AvaloniaTest]
    public void TerminalControl_can_apply_its_template()
    {
        var control = new TerminalControl();
        control.ApplyTemplate();

        Assert.That(control, Is.Not.Null);
    }
}
```

Add the test project to `src/Iciclecreek.Avalonia.TerminalWindow.slnx`.

- [ ] **Step 5: Add the fork maintenance contract**

Create `SOURCEGIT-COMPAT.md` containing these exact facts:

```markdown
# SourceGit Avalonia 11 compatibility branch

Upstream: https://github.com/tomlm/Iciclecreek.Avalonia.Terminal
Base branch: upstream/main11
Base commit: 3da5aad71e02517afa40f187461349ffafb2497b
Consumer: https://github.com/dhhieu113pro/sourcegit
Reason: SourceGit remains on Avalonia 11.3.20 while DevSpaces needs newer terminal input/selection behavior.

Backport references:
- 75b8ce24353ee568185f2dc4efffc1d091b035bf
- 468177130ef5a1daff79757cc0c49d5400e95066
- aa8b2fe629e8af4c0f338149d262f057c14bda50
- cb22471eeb290625707890489a418436c63da362
- e75aea69a5eea8645a932514408adcf0502dad4f
- 48ed663b98bf49eddb865a788d767c69bdba18ab

Removal condition: delete this compatibility fork/submodule when SourceGit moves to an Avalonia version supported by a current upstream terminal release that contains the required native input behavior.
```

- [ ] **Step 6: Run the baseline test suite**

```bash
dotnet restore src/Iciclecreek.Avalonia.TerminalWindow.slnx
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release --no-restore
```

Expected: restore succeeds and the new smoke test passes on the untouched input implementation.

- [ ] **Step 7: Commit the bootstrap**

```bash
git add src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj \
        src/Iciclecreek.Avalonia.Terminal.Tests \
        src/Iciclecreek.Avalonia.TerminalWindow.slnx \
        SOURCEGIT-COMPAT.md
git commit -m "test: bootstrap SourceGit terminal compatibility fork"
```

**Deliverable:** the compatibility fork builds on Avalonia 11.3.20 and has a functioning headless test harness before behavior changes begin.

---

## Task 2: Backport full-surface pointer hit testing and native mouse selection

**Repository:** `dhhieu113pro/Iciclecreek.Avalonia.Terminal`

**Files:**
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/PointerSelectionTests.cs`

**Upstream references:** `75b8ce24353ee568185f2dc4efffc1d091b035bf` plus the pointer-selection portions already present in upstream current `TerminalView.cs`.

- [ ] **Step 1: Add failing full-surface hit-test coverage**

Create `PointerSelectionTests.cs` with an Avalonia headless test that arranges a `TerminalView` to a non-zero size and asserts points in blank regions are hit-testable:

```csharp
[AvaloniaTest]
public void Blank_terminal_area_is_an_input_surface()
{
    var view = new TerminalView();
    view.Measure(new Size(800, 600));
    view.Arrange(new Rect(0, 0, 800, 600));

    Assert.That(((ICustomHitTest)view).HitTest(new Point(799, 599)), Is.True);
}
```

Run just this test and confirm RED before implementation:

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj \
  --configuration Release --filter "Blank_terminal_area_is_an_input_surface"
```

Expected before fix: test cannot pass because `TerminalView` does not yet implement `ICustomHitTest`.

- [ ] **Step 2: Implement whole-control hit testing**

Change the class declaration to:

```csharp
public class TerminalView : Control, ICustomHitTest
```

Add:

```csharp
public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);
```

Do not add a fake background solely to manipulate Avalonia hit testing.

- [ ] **Step 3: Add failing selection gesture tests before porting gesture changes**

Cover these contracts in `PointerSelectionTests.cs` using the real selection manager/terminal buffer plus headless pointer events where possible:

```text
single drag: Normal selection starts only after movement
double click: SelectionMode.Word
triple click: SelectionMode.Line
copy source selection remains valid across blank cells
```

The triple-click test must assert the complete logical line rather than only the painted glyph run.

- [ ] **Step 4: Port only the needed Avalonia-11-compatible pointer-selection behavior**

Adapt the newer upstream pointer handlers into the existing Avalonia-11 `TerminalView`:

```text
OnPointerPressed
OnPointerMoved
OnPointerReleased
```

Preserve these invariants:

```text
- normal left-click defers selection until movement when ShowCaretOnClick=false
- double-click selects a word
- triple-click selects a logical line
- selection can begin/move through blank cells because the full control is hit-testable
- if terminal mouse-reporting mode owns the pointer, events continue to the running application instead of starting local selection
```

Do not copy Avalonia-12-only focus/event signatures.

- [ ] **Step 5: Run selection tests and full fork suite**

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj \
  --configuration Release --filter "PointerSelectionTests"
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release
```

Expected: all pointer-selection tests and baseline tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs \
        src/Iciclecreek.Avalonia.Terminal.Tests/PointerSelectionTests.cs
git commit -m "fix: make terminal selection cover the full surface"
```

**Deliverable:** mouse selection is no longer limited to drawn glyph pixels, with word/line gestures protected by tests.

---

## Task 3: Expose a stable public clipboard/selection host API

**Repository:** `dhhieu113pro/Iciclecreek.Avalonia.Terminal`

**Files:**
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TerminalClipboardContractTests.cs`

**Upstream reference:** `468177130ef5a1daff79757cc0c49d5400e95066`.

- [ ] **Step 1: Add failing API-contract tests**

The wrapper must expose these exact public members:

```csharp
public Task SendInputAsync(string text, CancellationToken cancellationToken = default);
public Task<bool> CopyAsync();
public Task PasteAsync();
public Task SelectInputAsync();
public bool HasSelection { get; }
public bool IsMouseReportingActive { get; }
```

Write reflection-based contract assertions in `TerminalClipboardContractTests.cs` before implementation so a future terminal sync cannot silently remove them.

- [ ] **Step 2: Port `SendInputAsync`, `CopyAsync`, and `PasteAsync` into `TerminalView`**

Use the Avalonia-11 clipboard API available on the compatibility branch. The behavior contract is:

```csharp
public async Task<bool> CopyAsync()
{
    if (!_terminal.Selection.HasSelection)
        return false;

    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
    if (clipboard == null)
        return false;

    var text = _terminal.Selection.GetSelectionText();
    if (string.IsNullOrEmpty(text))
        return false;

    await clipboard.SetTextAsync(text);
    RequestInvalidate();
    return true;
}
```

For Avalonia 11, use the non-12 clipboard read API available in that branch for paste. Keep bracketed-paste handling from the terminal's existing behavior when enabled.

Copy must **not clear the selection**.

- [ ] **Step 3: Add selection/mouse-state accessors inside `TerminalView`**

Add stable public properties/methods with these names:

```csharp
public bool HasSelection => _terminal?.Selection.HasSelection == true;

public bool IsMouseReportingActive =>
    _terminal != null && IsTerminalMouseReportingEnabled();

public Task SelectInputAsync()
{
    // Use the same editable-input selection domain as the newer upstream
    // desktop shortcut implementation; do not select the entire scrollback.
}
```

`IsTerminalMouseReportingEnabled()` must derive from the emulator's actual mouse mode(s), not from `IsAlternateBuffer` alone. A TUI may own mouse input because it enabled mouse reporting; that is the state SourceGit needs to decide whether to suppress its right-click menu.

- [ ] **Step 4: Forward the APIs from `TerminalControl` without reflection**

Implement null-safe wrapper members:

```csharp
public Task SendInputAsync(string text, CancellationToken cancellationToken = default)
    => _terminalView?.SendInputAsync(text, cancellationToken) ?? Task.CompletedTask;

public Task<bool> CopyAsync()
    => _terminalView?.CopyAsync() ?? Task.FromResult(false);

public Task PasteAsync()
    => _terminalView?.PasteAsync() ?? Task.CompletedTask;

public Task SelectInputAsync()
    => _terminalView?.SelectInputAsync() ?? Task.CompletedTask;

public bool HasSelection => _terminalView?.HasSelection ?? false;

public bool IsMouseReportingActive => _terminalView?.IsMouseReportingActive ?? false;
```

- [ ] **Step 5: Test copy preservation and input delivery**

Add tests that:

```text
- build a real terminal selection, call CopyAsync, and assert HasSelection remains true;
- use a recording PTY connection, call SendInputAsync("abc"), and assert bytes "abc" reach the PTY;
- call PasteAsync with a test clipboard and assert pasted text reaches the PTY;
- call SelectInputAsync and assert it selects editable input, not unrelated scrollback;
- set emulator mouse-reporting mode and assert IsMouseReportingActive becomes true.
```

Where the compatibility line lacks a direct injectable PTY seam, add the smallest internal test seam rather than testing private fields by reflection.

- [ ] **Step 6: Run and commit**

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj --configuration Release
git add src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs \
        src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs \
        src/Iciclecreek.Avalonia.Terminal.Tests/TerminalClipboardContractTests.cs
git commit -m "feat: expose terminal clipboard and selection APIs"
```

**Deliverable:** SourceGit can consume all clipboard/selection/mouse-state behavior through public terminal APIs only.

---

## Task 4: Backport terminal-friendly shortcut mode and keyboard selection

**Repository:** `dhhieu113pro/Iciclecreek.Avalonia.Terminal`

**Files:**
- Create: `src/Iciclecreek.Avalonia.TerminalWindow/ShortcutMode.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TestConnections.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/ShortcutModeTests.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/ShiftSelectionTests.cs`
- Create or modify: `src/Iciclecreek.Avalonia.Terminal.Tests/KeyboardChordTests.cs`

**Upstream references:** `aa8b2fe629e8af4c0f338149d262f057c14bda50`, `cb22471eeb290625707890489a418436c63da362`, `e75aea69a5eea8645a932514408adcf0502dad4f`, `48ed663b98bf49eddb865a788d767c69bdba18ab`.

- [ ] **Step 1: Add the exact shortcut enum**

Create:

```csharp
namespace Iciclecreek.Terminal
{
    public enum ShortcutMode
    {
        Terminal = 0,
        Desktop = 1,
        None = 2,
    }
}
```

Add `ShortcutModeProperty` to `TerminalView` and `TerminalControl`; default must be `ShortcutMode.Terminal`.

- [ ] **Step 2: Write failing Windows/Linux terminal-mode tests first**

Using a recording PTY, assert:

```text
Ctrl+Shift+C + selection -> clipboard copy, no Ctrl+C byte sent
Ctrl+Shift+V -> clipboard text sent to PTY
Ctrl+C + selection -> copy selection
Ctrl+C + no selection -> 0x03 reaches PTY
Shift+Insert -> clipboard text reaches PTY
Ctrl+A -> Ctrl+A reaches PTY; it does NOT call SelectInputAsync
```

The last two contracts are SourceGit-specific acceptance requirements from the spec and must remain true even if upstream `Desktop` mode behaves differently.

- [ ] **Step 3: Write failing macOS-path tests**

Guard platform-specific paths as upstream does, and assert:

```text
Cmd+C -> copy
Cmd+V -> paste
Cmd+A -> SelectInputAsync
Ctrl+A -> application-owned
```

Do not apply the Windows/Linux Ctrl desktop map on macOS.

- [ ] **Step 4: Backport keyboard-selection/navigation behavior**

Port only the selection behavior needed by the spec:

```text
Shift+Left/Right/Home/End extends a terminal selection
Ctrl+Shift or Alt+Shift word navigation extends by word where the upstream behavior defines it
Cmd+Shift+Left/Right selects to line edge on macOS
normal typing retires/replaces keyboard selection according to editable-input rules
alternate screen keeps application-owned key handling
```

Reuse XTerm's existing word-definition logic rather than inventing a second word-boundary rule.

- [ ] **Step 5: Implement terminal-mode shortcut dispatch**

For Windows/Linux `ShortcutMode.Terminal`:

```text
Ctrl+Shift+C = copy
Ctrl+Shift+V = paste
Ctrl+C = copy iff selection exists; otherwise send Ctrl+C/SIGINT
Ctrl+A = application-owned
Shift+Insert = paste
```

For macOS, Cmd clipboard/select-input chords work regardless of desktop/terminal mode as specified.

When alternate-screen/full-screen applications own their keys, do not steal their contested bindings.

- [ ] **Step 6: Run focused and full tests**

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj \
  --configuration Release --filter "ShortcutModeTests|ShiftSelectionTests|KeyboardChordTests"
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release
```

Expected: all tests green, with platform-scoped tests skipped only on platforms they cannot execute on.

- [ ] **Step 7: Commit**

```bash
git add src/Iciclecreek.Avalonia.TerminalWindow/ShortcutMode.cs \
        src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs \
        src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs \
        src/Iciclecreek.Avalonia.Terminal.Tests
git commit -m "feat: backport native terminal clipboard shortcuts"
```

**Deliverable:** native terminal shortcut behavior and keyboard selection are tested without sacrificing shell/TUI ownership.

---

## Task 5: Verify the fork independently before SourceGit consumes it

**Repository:** `dhhieu113pro/Iciclecreek.Avalonia.Terminal`

**Files:**
- Modify if needed: `.github/workflows/BuildAndRunTests.yml`
- Modify if findings require: `SOURCEGIT-COMPAT.md`

- [ ] **Step 1: Ensure the fork workflow actually runs tests for the compatibility branch**

The workflow must run on PRs/updates relevant to `sourcegit/avalonia11-native-input` and execute:

```bash
dotnet restore src/Iciclecreek.Avalonia.TerminalWindow.slnx
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release --no-restore
```

Do not add NuGet publishing to this compatibility workflow.

- [ ] **Step 2: Open a fork PR**

Target the fork's Avalonia-11 maintenance branch (`main11`). If the GitHub fork was created with only the default branch, first push/create `main11` at upstream `3da5aad71e02517afa40f187461349ffafb2497b`, then target it.

PR title:

```text
feat: backport native terminal input for SourceGit
```

PR body must cite the six upstream reference commits and state that Avalonia remains 11.3.20.

- [ ] **Step 3: Require fresh green fork CI**

Record the exact green head SHA. Do not move SourceGit to the fork until the compatibility-branch test run is successful.

- [ ] **Step 4: Review the final fork diff for scope**

Expected categories only:

```text
Avalonia 11.3.20 compatibility package alignment
headless test harness
pointer hit testing/selection
public clipboard/selection APIs
shortcut mode/keyboard selection
maintenance note/workflow
```

Reject unrelated rendering, Sixel, palette, XTerm 2.x, Porta.Pty 2.x, or Avalonia 12 changes.

**Deliverable:** an independently green, scoped fork commit that SourceGit can pin exactly.

---

## Task 6: Switch SourceGit from NuGet to the pinned terminal submodule

**Repository:** `dhhieu113pro/sourcegit`

**Branch:** create `feat/devspaces-native-terminal-input` from the latest `master` only after Task 5 is green.

**Files:**
- Modify: `.gitmodules`
- Add gitlink: `depends/Iciclecreek.Avalonia.Terminal`
- Modify: `src/SourceGit.csproj`

- [ ] **Step 1: Add the public compatibility fork as a submodule pinned to Task 5's green commit**

`.gitmodules` must contain:

```ini
[submodule "depends/Iciclecreek.Avalonia.Terminal"]
	path = depends/Iciclecreek.Avalonia.Terminal
	url = https://github.com/dhhieu113pro/Iciclecreek.Avalonia.Terminal.git
```

The gitlink must point to the exact green compatibility-fork SHA recorded in Task 5, not to a moving branch name.

- [ ] **Step 2: Replace the NuGet package with a project reference**

Remove from `src/SourceGit.csproj`:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.11" />
```

Add beside the existing AvaloniaEdit project reference:

```xml
<ProjectReference Include="../depends/Iciclecreek.Avalonia.Terminal/src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj" />
```

- [ ] **Step 3: Verify dependency coherence**

```bash
git submodule update --init --recursive
dotnet restore SourceGit.slnx
dotnet build SourceGit.slnx --configuration Release --no-restore
```

Then inspect restore/build output or `obj/project.assets.json` and confirm no Avalonia `12.x` dependency is introduced by the terminal project.

Expected SourceGit Avalonia line remains `11.3.20`.

- [ ] **Step 4: Commit only the dependency boundary change**

```bash
git add .gitmodules depends/Iciclecreek.Avalonia.Terminal src/SourceGit.csproj
git commit -m "build: pin Avalonia 11 terminal compatibility fork"
```

**Deliverable:** SourceGit builds against the tested public fork through a reproducible submodule/project reference.

---

## Task 7: Wire native clipboard/context-menu behavior into DevSpaces

**Repository:** `dhhieu113pro/sourcegit`

**Files:**
- Modify: `src/Views/DevSpaceTerminal.axaml`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`

- [ ] **Step 1: Configure the embedded terminal explicitly for terminal-friendly shortcuts**

Update the terminal declaration to include:

```xml
<terminal:TerminalControl x:Name="Terminal"
                          FontFamily="{DynamicResource Fonts.Monospace}"
                          BufferSize="3000"
                          ShortcutMode="Terminal"
                          PointerPressed="OnTerminalPointerPressed"/>
```

Do not alter the terminal control's lifetime or replace it when menus open.

- [ ] **Step 2: Add a DevSpaces context-menu gate that respects application mouse ownership**

In `DevSpaceTerminal.axaml.cs`, handle only right-click:

```csharp
private void OnTerminalPointerPressed(object sender, PointerPressedEventArgs e)
{
    var point = e.GetCurrentPoint(Terminal);
    if (!point.Properties.IsRightButtonPressed)
        return;

    if (Terminal.IsMouseReportingActive)
        return;

    ShowTerminalContextMenu(Terminal);
    e.Handled = true;
}
```

The `IsMouseReportingActive` test is mandatory. Do not use `IsAlternateBuffer` as a substitute.

- [ ] **Step 3: Create Copy / Paste / Select All menu items backed only by public APIs**

Implement `ShowTerminalContextMenu(Control target)` using `MenuFlyout`:

```csharp
private void ShowTerminalContextMenu(Control target)
{
    var flyout = new MenuFlyout();

    var copy = new MenuItem
    {
        Header = App.Text("DevSpaces.Copy"),
        IsEnabled = Terminal.HasSelection,
    };
    copy.Click += async (_, _) => await SafeTerminalActionAsync(Terminal.CopyAsync);

    var paste = new MenuItem
    {
        Header = App.Text("DevSpaces.Paste"),
    };
    paste.Click += async (_, _) => await SafeTerminalActionAsync(async () =>
    {
        await Terminal.PasteAsync();
        return true;
    });

    var selectAll = new MenuItem
    {
        Header = App.Text("DevSpaces.SelectAll"),
    };
    selectAll.Click += async (_, _) => await SafeTerminalActionAsync(async () =>
    {
        await Terminal.SelectInputAsync();
        return true;
    });

    flyout.Items.Add(copy);
    flyout.Items.Add(paste);
    flyout.Items.Add(selectAll);
    flyout.ShowAt(target);
}
```

Use a small safe wrapper so clipboard failures do not crash SourceGit:

```csharp
private static async Task SafeTerminalActionAsync(Func<Task<bool>> action)
{
    try
    {
        await action();
    }
    catch
    {
        // Clipboard/input availability must not terminate the DevSpace session.
    }
}
```

If SourceGit localization does not yet contain `DevSpaces.Copy`, `DevSpaces.Paste`, and `DevSpaces.SelectAll`, add those three keys to `src/Resources/Locales/DevSpaces.axaml` in this same task rather than hard-coding UI strings.

- [ ] **Step 4: Keep Start/Stop/ProcessExited code unchanged**

Audit `DevSpaceTerminal.axaml.cs` after the menu change. These lifecycle operations must remain semantically identical:

```text
Start -> one Terminal.LaunchProcess(...)
Stop -> one Terminal.Kill()
ProcessExited -> update only that session state
```

Opening a menu, copying, pasting, or selecting must never call `LaunchProcess`, `Kill`, or replace the `TerminalControl` instance.

- [ ] **Step 5: Build locally if an SDK/runtime environment is available**

```bash
dotnet format SourceGit.slnx --verify-no-changes
dotnet build SourceGit.slnx --configuration Release --no-restore
```

Expected: no compile/format errors.

- [ ] **Step 6: Commit**

```bash
git add src/Views/DevSpaceTerminal.axaml \
        src/Views/DevSpaceTerminal.axaml.cs \
        src/Resources/Locales/DevSpaces.axaml
git commit -m "feat: add native DevSpaces terminal clipboard controls"
```

**Deliverable:** DevSpaces exposes native-feeling clipboard actions while delegating all terminal semantics to the fork.

---

## Task 8: Run CI, perform manual Copilot acceptance, and prepare the SourceGit PR

**Repository:** `dhhieu113pro/sourcegit`

- [ ] **Step 1: Audit the SourceGit branch diff before opening the PR**

Expected SourceGit changes are limited to:

```text
.gitmodules
depends/Iciclecreek.Avalonia.Terminal gitlink
src/SourceGit.csproj
src/Views/DevSpaceTerminal.axaml
src/Views/DevSpaceTerminal.axaml.cs
src/Resources/Locales/DevSpaces.axaml (only if localization keys are needed)
```

The already-approved spec/plan docs may be included if they have not been merged separately.

- [ ] **Step 2: Open the SourceGit PR**

PR title:

```text
feat: add native DevSpaces terminal selection and clipboard
```

PR body must include:

```text
- compatibility fork URL and exact pinned SHA
- fork test workflow/run result
- SourceGit remains on Avalonia 11.3.20
- no Copilot session-ID persistence changes in this PR
- manual Windows acceptance still pending until completed
```

- [ ] **Step 3: Require fresh SourceGit PR Check success**

Verify all jobs from `.github/workflows/pr-check.yml` are successful for the exact PR head:

```text
Windows x64
Windows ARM64
macOS Intel
macOS Apple Silicon
Linux x64
Linux ARM64
Format Check
```

If any job fails, invoke systematic-debugging and fix the root cause on the same PR; do not merge around it.

- [ ] **Step 4: Perform the required Windows Copilot manual acceptance**

Run SourceGit from the PR build on Windows and perform all ten checks from the approved spec:

```text
1. Start Copilot in a DevSpace.
2. Drag-select across rendered text and blank terminal cells.
3. Double-click a word and triple-click a logical line.
4. Ctrl+Shift+C copies selected output without clearing the selection.
5. Ctrl+Shift+V and Shift+Insert paste into Copilot.
6. With no selection, Ctrl+C still interrupts Copilot/the shell.
7. Ctrl+A remains application-owned in Copilot/shell input.
8. Right-click Copy, Paste, Select All all work.
9. In a mouse-aware alternate-screen application, right-click remains application-owned and SourceGit does not open its menu.
10. Add/switch DevSpaces terminals and switch History/Local Changes/Stashes/DevSpaces; the existing terminal process, buffer, and selection state are not restarted by clipboard interaction.
```

Record pass/fail for each item in the PR conversation. A failure is a bug to fix before merge.

- [ ] **Step 5: Final verification before calling the PR merge-ready**

Fetch fresh PR metadata and confirm:

```text
PR is open
head SHA is the expected tested SHA
mergeable is true
fork CI is green on the pinned fork SHA
SourceGit PR Check is green on the current head
all review threads are resolved
manual Copilot acceptance has 10/10 passes
```

Only then describe the PR as merge-ready. Do not merge unless the user explicitly asks.

**Deliverable:** a SourceGit PR with independently tested terminal fork behavior, green multi-platform SourceGit CI, and confirmed native Copilot terminal interaction on Windows.
