# Appearance Theme Schedule Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add System, Light, Dark, Sunset, and Custom time appearance modes that persist and switch DevBoard's theme without restart.

**Architecture:** Keep the existing `Preferences.Theme` persistence surface for backward compatibility and add schedule-specific preferences. Put time/solar decision logic in a small model that is deterministic and unit-testable, while `App` owns the Avalonia timer that reapplies the resolved theme at boundaries. Sunset mode uses stored coordinates and falls back to the system theme when coordinates are unavailable.

**Tech Stack:** .NET 10, Avalonia 11.3, xUnit.

**Spec:** Approved in conversation on 2026-08-30.

## Global Constraints

- Existing saved `Theme = "Default"` must continue to behave as System.
- Theme changes apply immediately without app restart.
- Custom schedules must handle overnight ranges.
- Sunset mode falls back to System when coordinates cannot be resolved.
- No network dependency is required to apply a saved schedule.

---

### Task 1: Theme schedule resolver

**Files:**
- Create: `src/Models/ThemeSchedule.cs`
- Create: `tests/DevBoard.Tests/ThemeScheduleTests.cs`

**Interfaces:**
- Produces: `ThemeSchedule.Resolve(...)` returning `System`, `Light`, or `Dark`.
- Produces: `ThemeSchedule.GetSunriseSunset(...)` for local solar boundaries.

- [ ] Write failing tests for custom time boundaries, overnight ranges, legacy Default/System, sunset resolution, and missing-coordinate fallback.
- [ ] Run CI and confirm the tests fail because `ThemeSchedule` does not exist.
- [ ] Add the minimal resolver and solar calculation.
- [ ] Run CI and confirm the resolver tests pass.

### Task 2: Persist and apply schedule settings

**Files:**
- Modify: `src/ViewModels/Preferences.cs`
- Modify: `src/App.axaml.cs`

**Interfaces:**
- Consumes: `ThemeSchedule.Resolve(...)`.
- Produces persisted `ThemeScheduleLightTime`, `ThemeScheduleDarkTime`, `ThemeScheduleLatitude`, and `ThemeScheduleLongitude` preferences.

- [ ] Add persistence tests where practical through the resolver-facing properties.
- [ ] Reapply theme whenever schedule settings change.
- [ ] Add a lightweight dispatcher timer to update at time boundaries and after resume/time jumps.
- [ ] Preserve `Default` as an alias for `System`.

### Task 3: Appearance UI

**Files:**
- Modify: `src/Views/Preferences.axaml`

**Interfaces:**
- Consumes the persisted schedule properties from `Preferences`.

- [ ] Replace the old ThemeVariant-only selector with System, Light, Dark, Sunset, and Custom time modes.
- [ ] Show Light/Dark start inputs only for Custom time.
- [ ] Show latitude/longitude inputs only for Sunset.
- [ ] Keep existing font/theme override controls unchanged below the new schedule controls.

### Task 4: Verification and PR

- [ ] Run repository CI/build/test workflows.
- [ ] Review the final diff for backward compatibility and XAML binding correctness.
- [ ] Open a PR against `master` with test evidence and known fallback behavior.
