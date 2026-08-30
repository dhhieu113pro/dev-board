# Files Code Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the plain read-only Files preview with a theme-aware AvaloniaEdit code viewer that provides syntax highlighting, line numbers, current-line emphasis, and editor-style scrolling without changing existing file/diff behavior.

**Architecture:** Keep `DevSpaceFiles` responsible only for loading file contents and choosing `DiffContext` versus `DevSpaceWorkspaceFile`. Add a reusable `CodeViewer` control that owns AvaloniaEdit/TextMate setup, language grammar selection, and theme synchronization. Add a small pure resolver to translate DevBoard file names/extensions into TextMate grammar extensions and cover that resolver with unit tests.

**Tech Stack:** .NET 10, Avalonia 11.3.20, existing AvaloniaEdit + AvaloniaEdit.TextMate submodule, TextMateSharp.Grammars, xUnit.

## Constraints

- No new package dependency.
- Existing changed-file `DiffView` remains unchanged.
- Existing 1 MB preview and binary-file guards remain unchanged.
- Viewer stays read-only.
- Plain-text fallback when no TextMate grammar exists.
- Follow DevBoard light/dark theme changes while the viewer is open.

### Task 1: Language mapping test

**Files:**
- Create: `tests/DevBoard.Tests/DevSpaceCodeLanguageResolverTests.cs`
- Create: `src/ViewModels/DevSpaceCodeLanguageResolver.cs`

1. Add failing tests covering C#, Avalonia XAML, JSON, TypeScript, Markdown, Dockerfile, Makefile, and extensionless fallback.
2. Implement the smallest language-extension resolver that makes those tests pass.

### Task 2: Reusable code viewer

**Files:**
- Create: `src/Views/CodeViewer.axaml`
- Create: `src/Views/CodeViewer.axaml.cs`

1. Host `AvaloniaEdit.TextEditor` with line numbers, monospaced font, no wrapping, automatic scrollbars, read-only mode, and current-line highlighting.
2. Install TextMate using the already-referenced `AvaloniaEdit.TextMate` project.
3. Select grammar from `DevSpaceWorkspaceFile.Path`, falling back to plain text when unavailable.
4. Apply TextMate editor/selection/current-line/line-number colors.
5. Switch between `ThemeName.LightPlus` and `ThemeName.DarkPlus` when `ActualThemeVariantChanged` fires.
6. Dispose the TextMate installation when the viewer is detached.

### Task 3: Files integration

**Files:**
- Modify: `src/Views/DevSpaceFiles.axaml`

1. Preserve the existing file path/message header.
2. Replace only the plain preview `TextBox` with `<v:CodeViewer/>`.
3. Leave folder rendering and `DiffView` templates untouched.

### Task 4: Verification

1. Run/observe the repository build for all existing build targets through PR CI.
2. Run the focused resolver tests explicitly if CI does not include them by default.
3. Review the final diff for accidental Files behavior changes.
4. Open a PR against `master`; do not merge without explicit approval.
