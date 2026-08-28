# DevSpaces Native Terminal Input Design

## Status

Approved architecture direction. This document defines the implementation boundary for making DevSpaces terminal selection and clipboard behavior feel like a native desktop terminal while keeping SourceGit on Avalonia 11.

## Problem

DevSpaces currently embeds `Iciclecreek.Avalonia.Terminal` 1.0.11. The PTY integration works, but text interaction does not feel native enough for daily Copilot CLI use. In particular, mouse selection is difficult, selection across blank terminal areas is unreliable, and copy/paste behavior is less discoverable and less consistent with Windows Terminal, macOS Terminal, and modern desktop applications.

Newer upstream terminal code has already solved several of these interaction problems, but current upstream package versions that contain the full set of improvements target Avalonia 12. SourceGit currently targets Avalonia 11.3.20, so upgrading the whole application to Avalonia 12 is outside the scope of this feature.

## Goals

1. Make mouse text selection reliable across the entire visible terminal surface.
2. Preserve normal terminal semantics for Ctrl+C, full-screen TUIs, and applications that request mouse reporting.
3. Provide predictable native copy/paste shortcuts on Windows, Linux, and macOS.
4. Add a discoverable context menu for Copy, Paste, and Select All.
5. Keep the current PTY/session lifecycle and DevSpaces persistence behavior unchanged.
6. Keep SourceGit on Avalonia 11.
7. Keep the terminal dependency upgrade isolated so future upstream synchronization remains possible.

## Non-goals

- Migrating SourceGit to Avalonia 12.
- Replacing XTerm.NET or Porta.Pty.
- Rewriting the terminal emulator inside SourceGit.
- Changing Copilot CLI behavior or session persistence in this milestone.
- Adding terminal profiles, themes, font configuration, or shell configuration beyond existing DevSpaces behavior.
- Changing DevSpaces grid/session lifecycle work covered by the preceding DevSpaces PRs.

## Architecture Decision

Maintain a small SourceGit-specific fork/backport of `tomlm/Iciclecreek.Avalonia.Terminal` that remains compatible with Avalonia 11, and consume it as a git submodule under `depends/` using the same dependency style SourceGit already uses for AvaloniaEdit.

The fork is a compatibility branch, not an independent terminal implementation. It should start from the code line compatible with the current 1.0.11 package and selectively port only the interaction fixes needed by SourceGit.

SourceGit will remove the `Iciclecreek.Avalonia.Terminal` NuGet package reference and add a `ProjectReference` to the terminal project inside the new submodule. CI already checks out submodules recursively, so the dependency remains reproducible without requiring a private package feed or custom NuGet publishing workflow.

### Why this approach

A custom NuGet package would add package publishing, authentication, and versioning work that does not improve the product. Copying terminal source directly into SourceGit would erase the upstream boundary and make future synchronization much harder. An Avalonia 12 migration would be disproportionately large and risky for a clipboard/selection feature.

A forked submodule keeps the terminal changes isolated, reviewable, testable upstream-style, and easy to replace later when SourceGit eventually moves to a compatible upstream terminal version.

## Fork Scope

The backport should reproduce behavior from newer upstream commits rather than blindly cherry-picking commits whose surrounding code may already assume Avalonia 12.

The relevant upstream behavior includes:

- whole-control hit testing so blank terminal cells and padding remain valid pointer targets;
- reliable mouse drag selection;
- selection extension with keyboard navigation where supported;
- public clipboard APIs (`CopyAsync`, `PasteAsync`) on the terminal wrapper;
- native macOS Cmd+C / Cmd+V behavior;
- configurable terminal-vs-desktop shortcut behavior;
- selection remaining visible after copy;
- correct handling of alternate-screen/full-screen applications so application-owned input is not stolen.

Each backport must be adapted to the Avalonia 11-compatible branch and covered by tests in the terminal fork.

## Interaction Model

### Pointer selection

The entire terminal rectangle is an input surface, including blank areas to the right of short lines and below the last rendered line.

A normal left-button drag selects terminal text. Selection must continue to update while the pointer crosses blank cells. Existing TUI mouse-reporting behavior remains authoritative when the running application enables mouse reporting; DevSpaces must not override application-owned mouse input.

Double-click selects a word using the terminal emulator's word-boundary rules. Triple-click selects the complete terminal line where supported by the backported selection implementation.

Copying does not clear the visible selection. A normal click or new selection replaces the previous selection according to standard terminal behavior.

### Windows and Linux shortcuts

Default shortcut mode is terminal-friendly desktop behavior:

- `Ctrl+Shift+C`: copy selected text.
- `Ctrl+Shift+V`: paste clipboard text.
- `Ctrl+C`: copy when a selection exists; otherwise pass Ctrl+C to the running process so Copilot/shell interrupt remains available.
- `Shift+Insert`: paste.
- `Ctrl+A`: remains available to the running shell by default unless the terminal's desktop shortcut mode explicitly owns it.

The implementation must not make SIGINT inaccessible.

### macOS shortcuts

- `Cmd+C`: copy.
- `Cmd+V`: paste.
- `Cmd+A`: select according to the terminal's supported desktop-selection behavior.
- Ctrl-based shell editing shortcuts remain application-owned.

### Context menu

Right-clicking a DevSpaces terminal exposes:

- Copy — enabled only when a selection exists.
- Paste — enabled when clipboard text is available where practical; otherwise invoking it is a safe no-op.
- Select All — uses the terminal control's selection API and does not terminate or restart the PTY.

The context menu is hosted by SourceGit/DevSpaces but delegates clipboard and selection operations to public terminal-control APIs. SourceGit must not reach into private terminal fields with reflection.

## SourceGit Integration Boundary

`Views/DevSpaceTerminal.axaml` continues to own a single embedded `TerminalControl` per DevSpace session.

`Views/DevSpaceTerminal.axaml.cs` may add SourceGit-specific context-menu wiring and shortcut-mode configuration, but it must not implement terminal-selection algorithms itself.

All low-level hit testing, selection ranges, clipboard extraction, paste injection, alternate-buffer handling, and application mouse-mode handling belong in the terminal fork.

This boundary is important: SourceGit should consume terminal behavior through stable public APIs so the fork can later be removed with minimal DevSpaces changes.

## Dependency Layout

Planned repository structure:

```text
sourcegit/
  depends/
    AvaloniaEdit/
    Iciclecreek.Avalonia.Terminal/   # git submodule -> SourceGit-compatible fork
  src/
    SourceGit.csproj
```

`src/SourceGit.csproj` will replace:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.11" />
```

with the appropriate project reference from the submodule.

`.gitmodules` will record the public fork URL. No credentials may be required for checkout or CI restore.

## Error Handling

Clipboard operations must fail safely. If the platform clipboard is unavailable, Copy/Paste does nothing and the terminal remains usable.

A terminal input exception must not crash SourceGit or terminate unrelated DevSpaces sessions. Existing DevSpace terminal start/exit handling remains unchanged.

If the submodule is missing in a developer checkout, the build should fail clearly at project-reference resolution just as the existing AvaloniaEdit submodule does. README/developer notes should mention recursive submodule initialization if the new dependency makes the existing instructions incomplete.

## Testing Strategy

### Terminal fork tests

The compatibility fork must retain or backport focused automated tests for:

- pointer hit testing across blank terminal regions;
- mouse drag selection;
- copy returns selected text and preserves visible selection;
- paste writes clipboard text to the PTY;
- Windows/Linux Ctrl+Shift+C and Ctrl+Shift+V;
- Ctrl+C still reaches the PTY when no selection exists;
- macOS Cmd+C and Cmd+V platform paths;
- selection/input behavior in alternate-screen mode;
- keyboard selection behavior included in the chosen backport set.

Where practical, tests should assert bytes/input reaching a recording PTY instead of private implementation state.

### SourceGit verification

SourceGit PR verification must include its existing multi-platform PR Check:

- Windows x64
- Windows ARM64
- macOS Intel
- macOS Apple Silicon
- Linux x64
- Linux ARM64
- format check

Manual acceptance on Windows should verify Copilot CLI specifically:

1. Start Copilot in a DevSpace.
2. Drag-select text across both rendered text and blank terminal cells.
3. Copy with Ctrl+Shift+C and paste into another application.
4. Paste into Copilot with Ctrl+Shift+V and Shift+Insert.
5. With no selection, press Ctrl+C and confirm Copilot receives interrupt behavior.
6. Right-click and exercise Copy, Paste, and Select All.
7. Add/switch DevSpaces terminals and repository pages and confirm the terminal session is not restarted as a side effect of clipboard interaction.

Manual runtime acceptance is required because CI proves compilation/tests but cannot prove native desktop feel.

## Delivery Sequence

1. Create the public terminal fork and an Avalonia-11 compatibility branch based on the 1.0.11-compatible code line.
2. Backport the native input/selection changes with terminal-library tests passing.
3. Add the fork as a SourceGit submodule and replace the NuGet package reference with a project reference.
4. Configure DevSpaces to use the backported shortcut mode and public clipboard APIs.
5. Add SourceGit context-menu integration.
6. Run SourceGit PR Check on all supported targets.
7. Perform Windows manual acceptance with Copilot CLI before merge.

## Compatibility and Maintenance

The fork should contain a short maintenance note identifying:

- the upstream repository and base commit/tag;
- which upstream behavior/commits were backported;
- the reason the fork exists: Avalonia 11 compatibility;
- the removal condition: SourceGit adopts an Avalonia version supported by a current upstream terminal release containing the required interaction behavior.

No unrelated terminal features should be backported. The compatibility branch exists only to close the native-input gap needed by DevSpaces.

## Acceptance Criteria

The feature is complete when all of the following are true:

- selecting terminal text with the mouse feels continuous across the full terminal surface;
- copying selected Copilot output is reliable;
- pasting into Copilot is reliable through native terminal shortcuts and context menu;
- Ctrl+C still interrupts the running process when there is no active selection;
- full-screen terminal applications keep ownership of their input modes;
- clipboard operations never restart the PTY or clear unrelated DevSpaces state;
- SourceGit remains on Avalonia 11.3.20;
- SourceGit consumes the compatibility terminal via the public fork/submodule, with no reflection into private terminal internals;
- terminal-fork tests and SourceGit PR checks are green;
- Windows manual Copilot acceptance passes.
