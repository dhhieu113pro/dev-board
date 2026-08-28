# DevSpaces Native Terminal Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make DevSpaces terminal selection, copy, paste, and keyboard selection behave like a native desktop terminal without changing SourceGit's Avalonia 11.3.20 platform line or its existing PTY/session lifetime rules.

**Architecture:** Maintain a small public Avalonia-11 compatibility fork of `tomlm/Iciclecreek.Avalonia.Terminal`, based on upstream `main11` commit `3da5aad71e02517afa40f187461349ffafb2497b`. Backport only the input/selection/clipboard behavior named in the approved spec, prove it in the fork's automated tests, then pin that exact green fork commit into SourceGit as `depends/Iciclecreek.Avalonia.Terminal` and consume it with a `ProjectReference`. SourceGit owns only DevSpaces context-menu wiring and terminal configuration; terminal algorithms stay in the fork.

**Tech Stack:** .NET 8 terminal compatibility library, .NET 10 SourceGit host, Avalonia 11.3.20, XTerm.NET 1.0.12, Porta.Pty 1.0.7, NUnit 4, Avalonia.Headless.NUnit 11.3.20, Git submodules, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- Keep SourceGit on Avalonia `11.3.20`; never import the Avalonia-12 API changes from upstream `main`.
- Fork from upstream `main11` at `3da5aad71e02517afa40f187461349ffafb2497b` (version 1.0.12, `net8.0`, Avalonia 11.3.14), then align only Avalonia dependencies to 11.3.20.
- Keep `Porta.Pty` `1.0.7` and `XTerm.NET` `1.0.12` unless a required backport provably cannot be implemented without a targeted compatible update.
- No terminal source vendoring inside SourceGit and no reflection into private terminal fields.
- `Ctrl+C` with no selection must still reach the process. `Ctrl+A` stays application-owned on Windows/Linux.
- Alternate-screen applications and terminal mouse-reporting modes keep ownership of their input.
- Do not change Copilot CLI session-ID persistence in this milestone.
- Do not regress the merged rule that one `TerminalControl` remains parented for its session lifetime.
- Terminal-fork behavior changes use TDD. SourceGit has no DevSpaces test project, so its verification is source audit + existing six-platform PR Check + required manual Windows Copilot acceptance.
- Do not call the feature complete or merge-ready until Task 8's manual acceptance passes.

---

## Task 1: Bootstrap the Avalonia-11 compatibility fork and test harness

**Repository:** `dhhieu113pro/Iciclecreek.Avalonia.Terminal`

**Prerequisite:** The fork does not currently exist, and the connected GitHub actions available here cannot create/fork repositories. The user must create a **public** fork of `tomlm/Iciclecreek.Avalonia.Terminal` at `dhhieu113pro/Iciclecreek.Avalonia.Terminal`. If it is missing when execution starts, STOP and ask the user to create it; do not vendor the code and do not substitute another fork.

**Files:**
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TestAppBuilder.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TerminalControlSmokeTests.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow.slnx`
- Create: `SOURCEGIT-COMPAT.md`

- [ ] **Step 1: Create `sourcegit/avalonia11-native-input` from the exact base SHA**

```bash
git fetch upstream main11
git switch -c sourcegit/avalonia11-native-input 3da5aad71e02517afa40f187461349ffafb2497b
git rev-parse HEAD
```

Expected SHA: `3da5aad71e02517afa40f187461349ffafb2497b`.

- [ ] **Step 2: Align only Avalonia to 11.3.20**

Keep in the terminal project:

```xml
<TargetFramework>net8.0</TargetFramework>
<PackageReference Include="Porta.Pty" Version="1.0.7" />
<PackageReference Include="XTerm.NET" Version="1.0.12" />
```

Set:

```xml
<PackageReference Include="Avalonia" Version="11.3.20" />
```

- [ ] **Step 3: Add an Avalonia-11 headless NUnit project**

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

Create `TestAppBuilder.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Iciclecreek.Terminal.Tests.TestAppBuilder))]

namespace Iciclecreek.Terminal.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .AfterSetup(builder => builder.Instance?.Styles.Add(new FluentTheme()));
}
```

Add the test project to `src/Iciclecreek.Avalonia.TerminalWindow.slnx`.

- [ ] **Step 4: Add and run a baseline realization test**

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

Run:

```bash
dotnet restore src/Iciclecreek.Avalonia.TerminalWindow.slnx
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release --no-restore
```

Expected: green before any input behavior is changed.

- [ ] **Step 5: Add `SOURCEGIT-COMPAT.md`**

Record: upstream URL, base branch `main11`, base SHA `3da5aad71e02517afa40f187461349ffafb2497b`, consumer repo, Avalonia-11 reason, removal condition, and these upstream behavior references:

```text
75b8ce24353ee568185f2dc4efffc1d091b035bf
468177130ef5a1daff79757cc0c49d5400e95066
aa8b2fe629e8af4c0f338149d262f057c14bda50
cb22471eeb290625707890489a418436c63da362
e75aea69a5eea8645a932514408adcf0502dad4f
48ed663b98bf49eddb865a788d767c69bdba18ab
```

- [ ] **Step 6: Commit**

```bash
git add src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj \
        src/Iciclecreek.Avalonia.Terminal.Tests \
        src/Iciclecreek.Avalonia.TerminalWindow.slnx SOURCEGIT-COMPAT.md
git commit -m "test: bootstrap SourceGit terminal compatibility fork"
```

**Deliverable:** Avalonia-11.3.20 fork builds and has a working headless test harness.

---

## Task 2: Backport full-surface pointer input and lock mouse-selection behavior

**Repository:** terminal fork

**Files:**
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/PointerSelectionTests.cs`

**Reference:** `75b8ce24353ee568185f2dc4efffc1d091b035bf` plus current upstream pointer behavior. Note: `main11` already contains deferred single-click selection and word/line click handling, so preserve those with characterization tests instead of re-implementing them blindly.

- [ ] **Step 1: Write the failing full-surface hit-test first**

```csharp
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using NUnit.Framework;

[AvaloniaTest]
public void Blank_terminal_area_is_an_input_surface()
{
    var view = new TerminalView();
    view.Measure(new Size(800, 600));
    view.Arrange(new Rect(0, 0, 800, 600));

    Assert.That(((ICustomHitTest)view).HitTest(new Point(799, 599)), Is.True);
}
```

Run it and confirm RED because `main11`'s `TerminalView` does not implement `ICustomHitTest`:

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj \
  --configuration Release --filter "Name=Blank_terminal_area_is_an_input_surface"
```

- [ ] **Step 2: Implement the whole rectangle as the input surface**

```csharp
public class TerminalView : Control, ICustomHitTest
{
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);
}
```

Do not add a fake background solely to alter hit testing.

- [ ] **Step 3: Add characterization tests for the existing `main11` gesture behavior**

Drive the headless pointer route and assert:

```text
single left-click does not leave a one-cell selection when ShowCaretOnClick=false
first drag movement starts Normal selection
double-click selects SelectionMode.Word content
triple-click selects the complete logical line
mouse-reporting mode prevents local pointer selection unless the library's existing explicit selection override applies
```

If any approved contract is missing, first make that specific assertion fail, then port only the required newer-upstream logic. Do not copy Avalonia-12 focus/event signatures.

- [ ] **Step 4: Run focused + full tests and commit**

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj \
  --configuration Release --filter "FullyQualifiedName~PointerSelectionTests"
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release
git add src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs \
        src/Iciclecreek.Avalonia.Terminal.Tests/PointerSelectionTests.cs
git commit -m "fix: make terminal selection cover the full surface"
```

**Deliverable:** full terminal rectangle is selectable while existing word/line/TUI pointer semantics remain covered.

---

## Task 3: Expose public clipboard, editable-selection, and mouse-ownership APIs

**Repository:** terminal fork

**Files:**
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TestConnections.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/TerminalClipboardContractTests.cs`

**Reference:** `468177130ef5a1daff79757cc0c49d5400e95066` and the current upstream `SelectInputAsync` implementation.

- [ ] **Step 1: Add failing public API contract tests**

Require these exact `TerminalControl` members:

```csharp
public Task SendInputAsync(string text, CancellationToken cancellationToken = default);
public Task<bool> CopyAsync();
public Task PasteAsync();
public Task<bool> SelectInputAsync();
public bool HasSelection { get; }
public bool IsMouseReportingActive { get; }
```

Use reflection only **inside the fork's contract test** to assert public surface shape; SourceGit production code must never use reflection.

- [ ] **Step 2: Port clipboard/input primitives into `TerminalView`**

Backport `SendInputAsync`, `CopyAsync`, and `PasteAsync` from newer upstream. For Avalonia 11, use its clipboard read API rather than copying `TryGetTextAsync` if that API is unavailable on 11.3.20. Preserve bracketed paste.

`CopyAsync` contract:

```text
false when no selection or clipboard is unavailable
copies Selection.GetSelectionText() otherwise
does not ClearSelection()
invalidates so the existing selection stays visibly rendered
```

- [ ] **Step 3: Make editable-input selection public and add exact state properties**

Change/port the upstream editable selection method to:

```csharp
public async Task<bool> SelectInputAsync()
```

It selects only the shell's editable input domain, not the whole scrollback.

Add:

```csharp
public bool HasSelection => _terminal?.Selection.HasSelection == true;

public bool IsMouseReportingActive =>
    _terminal != null && _terminal.MouseTrackingMode != XT.Input.MouseTrackingMode.None;
```

Do not infer mouse ownership from `IsAlternateBuffer`; the emulator's `MouseTrackingMode` is the authoritative signal.

- [ ] **Step 4: Forward the exact APIs from `TerminalControl`**

```csharp
public Task SendInputAsync(string text, CancellationToken cancellationToken = default)
    => _terminalView?.SendInputAsync(text, cancellationToken) ?? Task.CompletedTask;

public Task<bool> CopyAsync()
    => _terminalView?.CopyAsync() ?? Task.FromResult(false);

public Task PasteAsync()
    => _terminalView?.PasteAsync() ?? Task.CompletedTask;

public Task<bool> SelectInputAsync()
    => _terminalView?.SelectInputAsync() ?? Task.FromResult(false);

public bool HasSelection => _terminalView?.HasSelection ?? false;
public bool IsMouseReportingActive => _terminalView?.IsMouseReportingActive ?? false;
```

- [ ] **Step 5: Test behavior through a realized headless terminal**

Reuse the upstream-style `RecordingConnection` seam in `TestConnections.cs`. Use a headless `Window` to obtain a real clipboard, matching upstream tests:

```csharp
var view = new TerminalView { Process = "" };
var window = new Window { Width = 800, Height = 600, Content = view };
window.Show();
window.UpdateLayout();
var pty = new RecordingConnection();
view.AttachConnection(pty);
```

Assert:

```text
SendInputAsync("abc") -> recording PTY receives "abc"
CopyAsync -> clipboard receives selected text and HasSelection remains true
PasteAsync -> headless clipboard text reaches recording PTY
SelectInputAsync -> selects current editable input only
MouseTrackingMode.None -> IsMouseReportingActive false
non-None mouse tracking -> IsMouseReportingActive true
```

Close every test window in `finally`.

- [ ] **Step 6: Run and commit**

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj --configuration Release
git add src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs \
        src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs \
        src/Iciclecreek.Avalonia.Terminal.Tests
git commit -m "feat: expose terminal clipboard and selection APIs"
```

**Deliverable:** SourceGit can use stable public APIs for clipboard, editable selection, and application mouse ownership.

---

## Task 4: Backport terminal-friendly shortcuts and keyboard selection

**Repository:** terminal fork

**Files:**
- Create: `src/Iciclecreek.Avalonia.TerminalWindow/ShortcutMode.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs`
- Modify: `src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/ShortcutModeTests.cs`
- Create: `src/Iciclecreek.Avalonia.Terminal.Tests/ShiftSelectionTests.cs`
- Create or modify: `src/Iciclecreek.Avalonia.Terminal.Tests/KeyboardChordTests.cs`

**References:** `aa8b2fe629e8af4c0f338149d262f057c14bda50`, `cb22471eeb290625707890489a418436c63da362`, `e75aea69a5eea8645a932514408adcf0502dad4f`, `48ed663b98bf49eddb865a788d767c69bdba18ab`.

- [ ] **Step 1: Add the exact shortcut enum/property**

```csharp
namespace Iciclecreek.Terminal;

public enum ShortcutMode
{
    Terminal = 0,
    Desktop = 1,
    None = 2,
}
```

`TerminalView.ShortcutMode` and `TerminalControl.ShortcutMode` default to `Terminal`.

- [ ] **Step 2: Write failing/characterization shortcut tests before porting behavior**

Using the realized view + `RecordingConnection` pattern, lock these contracts:

```text
Windows/Linux Terminal mode:
Ctrl+Shift+C + selection -> copy; no control byte reaches PTY
Ctrl+Shift+V -> clipboard text reaches PTY
Ctrl+C + selection -> copy
Ctrl+C + no selection -> \u0003 reaches PTY
Shift+Insert -> clipboard text reaches PTY
Ctrl+A -> application-owned; its control input reaches PTY

macOS:
Cmd+C -> copy
Cmd+V -> paste
Cmd+A -> SelectInputAsync
Ctrl+A -> application-owned
```

Platform-specific tests use `Assert.Ignore`/`[Platform]` so unsupported paths are explicit skips, not false greens.

- [ ] **Step 3: Backport keyboard selection/navigation**

Port only the approved behavior:

```text
Shift+Left/Right/Home/End extends selection
Ctrl+Shift or Alt+Shift + horizontal arrows extends by XTerm word boundaries
Cmd+Shift+Left/Right selects to line edge on macOS
normal typing retires/replaces keyboard selection using editable-input boundaries
alternate screen leaves application key handling intact
```

Reuse XTerm's word-definition logic; do not invent a second word parser.

- [ ] **Step 4: Implement terminal-mode shortcut dispatch**

Preserve exactly:

```text
Ctrl+Shift+C = copy
Ctrl+Shift+V = paste
Ctrl+C = copy iff selection exists, otherwise process/SIGINT
Shift+Insert = paste
Ctrl+A = application-owned on Windows/Linux
Cmd+C / Cmd+V / Cmd+A = native macOS operations
ShortcutMode.None = keyboard remains application-owned
```

Do not activate upstream Desktop-mode `Ctrl+A` behavior in SourceGit; SourceGit will explicitly use `ShortcutMode.Terminal`.

- [ ] **Step 5: Run focused + full tests and commit**

```bash
dotnet test src/Iciclecreek.Avalonia.Terminal.Tests/Iciclecreek.Avalonia.Terminal.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~ShortcutModeTests|FullyQualifiedName~ShiftSelectionTests|FullyQualifiedName~KeyboardChordTests"
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release
git add src/Iciclecreek.Avalonia.TerminalWindow/ShortcutMode.cs \
        src/Iciclecreek.Avalonia.TerminalWindow/TerminalView.cs \
        src/Iciclecreek.Avalonia.TerminalWindow/TerminalControl.cs \
        src/Iciclecreek.Avalonia.Terminal.Tests
git commit -m "feat: backport native terminal clipboard shortcuts"
```

**Deliverable:** native shortcut/keyboard selection behavior is protected without stealing shell or TUI input.

---

## Task 5: Make the fork independently green before integration

**Repository:** terminal fork

**Files:** modify `.github/workflows/BuildAndRunTests.yml` only if the forked workflow does not run the new test project/branch.

- [ ] **Step 1: Ensure CI executes the compatibility solution tests**

Required commands:

```bash
dotnet restore src/Iciclecreek.Avalonia.TerminalWindow.slnx
dotnet test src/Iciclecreek.Avalonia.TerminalWindow.slnx --configuration Release --no-restore
```

Do not add NuGet publishing to this compatibility workflow.

- [ ] **Step 2: Open a fork PR titled `feat: backport native terminal input for SourceGit`**

Target fork branch `main11`. If the fork was created with only its default branch, create `main11` at upstream SHA `3da5aad71e02517afa40f187461349ffafb2497b` first. The PR body names all six upstream reference commits and states Avalonia stays 11.3.20.

- [ ] **Step 3: Require fresh green fork CI and record the exact tested head SHA**

No SourceGit dependency switch before this is green.

- [ ] **Step 4: Audit fork scope**

Allowed diff categories only:

```text
Avalonia 11.3.20 package alignment
test harness/tests
pointer hit testing and selection
public clipboard/selection APIs
shortcut mode and keyboard selection
compatibility maintenance note/workflow
```

Reject unrelated Sixel, palette/rendering, Avalonia 12, Porta.Pty 2.x, or XTerm 2.x changes.

**Deliverable:** independently green compatibility-fork SHA suitable for pinning.

---

## Task 6: Pin the green fork into SourceGit as a submodule

**Repository:** `dhhieu113pro/sourcegit`

**Branch:** `feat/devspaces-native-terminal-input` from the latest `master` after Task 5 is green.

**Files:**
- Modify: `.gitmodules`
- Add gitlink: `depends/Iciclecreek.Avalonia.Terminal`
- Modify: `src/SourceGit.csproj`

- [ ] **Step 1: Add the public submodule pinned to Task 5's exact green SHA**

Append:

```ini
[submodule "depends/Iciclecreek.Avalonia.Terminal"]
	path = depends/Iciclecreek.Avalonia.Terminal
	url = https://github.com/dhhieu113pro/Iciclecreek.Avalonia.Terminal.git
```

The gitlink points to the tested SHA, never a moving branch ref.

- [ ] **Step 2: Replace the package reference**

Remove:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.11" />
```

Add:

```xml
<ProjectReference Include="../depends/Iciclecreek.Avalonia.Terminal/src/Iciclecreek.Avalonia.TerminalWindow/Iciclecreek.Avalonia.Terminal.csproj" />
```

- [ ] **Step 3: Verify dependency coherence**

```bash
git submodule update --init --recursive
dotnet restore SourceGit.slnx
dotnet build SourceGit.slnx --configuration Release --no-restore
```

Inspect restore output or `src/obj/project.assets.json`; there must be no Avalonia `12.x` package introduced by the terminal project.

- [ ] **Step 4: Commit**

```bash
git add .gitmodules depends/Iciclecreek.Avalonia.Terminal src/SourceGit.csproj
git commit -m "build: pin Avalonia 11 terminal compatibility fork"
```

**Deliverable:** SourceGit builds against a reproducible, tested terminal fork while remaining on Avalonia 11.3.20.

---

## Task 7: Wire native clipboard/context-menu behavior into DevSpaces

**Repository:** SourceGit

**Files:**
- Modify: `src/Views/DevSpaceTerminal.axaml`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`
- Modify if needed: `src/Resources/Locales/DevSpaces.axaml`

- [ ] **Step 1: Configure the existing terminal control for terminal-mode shortcuts**

```xml
<terminal:TerminalControl x:Name="Terminal"
                          FontFamily="{DynamicResource Fonts.Monospace}"
                          BufferSize="3000"
                          ShortcutMode="Terminal"/>
```

Do not replace the control or alter Start/Stop lifetime semantics.

- [ ] **Step 2: Observe right-click through a tunnel handler so the inner terminal cannot hide it**

After `InitializeComponent()` in `DevSpaceTerminal`:

```csharp
Terminal.AddHandler(
    InputElement.PointerPressedEvent,
    OnTerminalPointerPressed,
    RoutingStrategies.Tunnel,
    handledEventsToo: true);
```

Add the required `Avalonia.Input` and `Avalonia.Interactivity` usings.

Handler:

```csharp
private void OnTerminalPointerPressed(object sender, PointerPressedEventArgs e)
{
    var point = e.GetCurrentPoint(Terminal);
    if (!point.Properties.IsRightButtonPressed)
        return;

    if (Terminal.IsMouseReportingActive)
        return; // leave unhandled so the TUI receives its mouse event

    ShowTerminalContextMenu(Terminal);
    e.Handled = true; // suppress the terminal library's legacy right-click copy/paste path
}
```

- [ ] **Step 3: Add Copy / Paste / Select All using only public terminal APIs**

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

    var paste = new MenuItem { Header = App.Text("DevSpaces.Paste") };
    paste.Click += async (_, _) => await SafeTerminalActionAsync(async () =>
    {
        await Terminal.PasteAsync();
        return true;
    });

    var selectAll = new MenuItem { Header = App.Text("DevSpaces.SelectAll") };
    selectAll.Click += async (_, _) => await SafeTerminalActionAsync(Terminal.SelectInputAsync);

    flyout.Items.Add(copy);
    flyout.Items.Add(paste);
    flyout.Items.Add(selectAll);
    flyout.ShowAt(target);
}

private static async Task SafeTerminalActionAsync(Func<Task<bool>> action)
{
    try
    {
        await action();
    }
    catch
    {
        // Clipboard/input availability must never terminate the DevSpace PTY.
    }
}
```

If the localization keys are absent, add `DevSpaces.Copy`, `DevSpaces.Paste`, and `DevSpaces.SelectAll` to `src/Resources/Locales/DevSpaces.axaml` rather than hard-coding labels.

- [ ] **Step 4: Audit lifecycle invariants**

After the edit, confirm clipboard/menu paths contain no calls to:

```text
Terminal.LaunchProcess
Terminal.Kill
new TerminalControl
```

Existing `Start`, `Stop`, and `ProcessExited` behavior remains unchanged.

- [ ] **Step 5: Build/format and commit**

```bash
dotnet format SourceGit.slnx --verify-no-changes
dotnet build SourceGit.slnx --configuration Release --no-restore
git add src/Views/DevSpaceTerminal.axaml \
        src/Views/DevSpaceTerminal.axaml.cs \
        src/Resources/Locales/DevSpaces.axaml
git commit -m "feat: add native DevSpaces terminal clipboard controls"
```

If the local SDK is unavailable, do not claim these commands passed; rely on Task 8 CI for build/format proof.

**Deliverable:** DevSpaces exposes discoverable Copy/Paste/Select All without stealing TUI mouse input or touching PTY lifetime.

---

## Task 8: Verify both repositories and prepare the SourceGit PR

**Repository:** SourceGit plus the pinned terminal fork commit

- [ ] **Step 1: Audit the SourceGit diff**

Expected production paths only:

```text
.gitmodules
depends/Iciclecreek.Avalonia.Terminal (gitlink)
src/SourceGit.csproj
src/Views/DevSpaceTerminal.axaml
src/Views/DevSpaceTerminal.axaml.cs
src/Resources/Locales/DevSpaces.axaml (only if new keys were needed)
```

Approved spec/plan docs may also be included if not merged separately.

- [ ] **Step 2: Open SourceGit PR `feat: add native DevSpaces terminal selection and clipboard`**

PR body records:

```text
compatibility fork URL + exact pinned SHA
fork test run result
SourceGit remains Avalonia 11.3.20
no Copilot session-ID persistence change in this PR
manual Windows acceptance status
```

- [ ] **Step 3: Require fresh SourceGit PR Check success for the exact head**

All must succeed:

```text
Windows x64
Windows ARM64
macOS Intel
macOS Apple Silicon
Linux x64
Linux ARM64
Format Check
```

Any failure triggers systematic-debugging; do not merge around a red/cancelled/in-progress required check.

- [ ] **Step 4: Perform all ten Windows Copilot acceptance checks from the spec**

```text
1. Start Copilot in a DevSpace.
2. Drag-select across rendered text and blank terminal cells.
3. Double-click a word and triple-click a logical line.
4. Ctrl+Shift+C copies without clearing the visible selection.
5. Ctrl+Shift+V and Shift+Insert paste into Copilot.
6. With no selection, Ctrl+C still interrupts Copilot/the shell.
7. Ctrl+A remains application-owned in Copilot/shell input.
8. Right-click Copy, Paste, Select All work.
9. In a mouse-aware alternate-screen app, right-click stays application-owned and SourceGit shows no menu.
10. Add/switch terminals and switch History/Local Changes/Stashes/DevSpaces; clipboard interaction never restarts the process or loses terminal state.
```

Record pass/fail for all ten in the PR conversation. A failed item is a bug to fix before merge.

- [ ] **Step 5: Final verification-before-completion**

Fetch fresh state and confirm:

```text
fork CI green on the exact pinned SHA
SourceGit PR open and mergeable
SourceGit head equals the tested SHA
all required PR Check jobs successful
all review threads resolved
manual Copilot acceptance 10/10
```

Only then call the PR merge-ready. Do not merge until the user explicitly asks.

**Deliverable:** tested compatibility fork + green SourceGit PR + confirmed native Copilot terminal interaction on Windows.
