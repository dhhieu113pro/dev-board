# Dev Stats + Logs Implementation Plan

**Goal:** Surface the existing SourceGit-derived repository statistics chart and command logs directly on the Dev dashboard.

**Architecture:** Keep `ViewModels.Statistics`, `Models.Statistics`, `ViewModels.ViewLogs`, `Views.Chart`, and `Views.CommandLogContentPresenter` as the single source of truth. `DevSpaceDashboard` only owns repository-scoped instances and exposes a small Weekly/Monthly/Total UI-order adapter. The dashboard XAML embeds those existing view models/components instead of duplicating git queries or aggregation.

## Tasks

1. Add focused dashboard tests for statistics mode ordering/default and repository-less safety.
2. Add repository statistics/log view-model properties to `DevSpaceDashboard`, defaulting dashboard statistics to `ThisWeek`.
3. Embed a Dev Stats section with Weekly / Monthly / Total selector, existing branch/author controls, and existing `Chart` control.
4. Embed a Logs section backed by existing `ViewLogs`, including log selection/content and clear action.
5. Verify the branch diff and GitHub Actions checks, then open a pull request.
