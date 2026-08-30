# Appearance Theme Schedule Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add System, Light, Dark, Sunset, and Custom time appearance modes that persist and switch DevBoard's theme without restart.

**Architecture:** Keep the existing `Preferences.Theme` behavior untouched for backward compatibility and layer scheduling on top with focused additive files. `ThemeSchedule` contains deterministic custom-time and solar calculations, `ThemeScheduleSettings` persists scheduling values beside the existing preference JSON, and `ThemeScheduleController` resolves/applies the effective Avalonia theme. A small Appearance control is injected into the existing theme row so the large upstream Preferences XAML/model files do not need invasive edits.

**Tech Stack:** .NET 10, Avalonia 11.3, xUnit.

**Spec:** Approved in conversation on 2026-08-30.

## Global Constraints

- Existing saved `Theme = "Default"` continues to behave as System.
- Theme changes apply immediately without app restart.
- Custom schedules handle overnight ranges.
- Sunset mode falls back to System when coordinates are unavailable or solar boundaries cannot be calculated.
- Saved sunset coordinates are sufficient for normal operation; network access is only used when the user explicitly requests approximate location detection.

---

### Task 1: Theme schedule resolver

**Files:**
- Create: `src/Models/ThemeSchedule.cs`
- Create: `tests/DevBoard.Tests/ThemeScheduleTests.cs`

**Interfaces:**
- Produces: `ThemeSchedule.Resolve(...)` returning `System`, `Light`, or `Dark`.
- Produces: `ThemeSchedule.GetSunriseSunset(...)` for local solar boundaries.

- [x] Define tests for manual/system modes, custom boundaries, overnight ranges, sunset calculation, and missing-coordinate fallback.
- [x] Add the minimal resolver and local solar calculation.
- [ ] Verify the final resolver tests in repository CI.

### Task 2: Persist and apply schedule settings

**Files:**
- Create: `src/Models/ThemeScheduleSettings.cs`
- Create: `src/ThemeScheduleController.cs`
- Create: `src/App.ThemeSchedule.cs`

**Interfaces:**
- Consumes: `ThemeSchedule.Resolve(...)`.
- Produces: persisted mode, light start, dark start, latitude, and longitude values in `theme-schedule.config`.

- [x] Preserve existing `Default` as an alias for System.
- [x] Persist schedule settings independently of the existing Preferences JSON schema.
- [x] Reapply the resolved theme while DevBoard is running so custom/sunset boundaries, resume, and clock/timezone changes do not require restart.
- [x] Keep existing theme override application in the resolution path.

### Task 3: Appearance UI

**Files:**
- Create: `src/Views/ThemeSchedulePreferences.axaml`
- Create: `src/Views/ThemeSchedulePreferences.axaml.cs`
- Create: `src/Views/ThemeSchedulePreferencesInjector.cs`

**Interfaces:**
- Consumes the persisted schedule settings through `ThemeScheduleController`.

- [x] Replace the visible theme selector in Appearance with System, Light, Dark, Sunset / sunrise, and Custom time choices while retaining the existing selector underneath for compatibility.
- [x] Show Light/Dark start inputs only for Custom time.
- [x] Show latitude/longitude inputs only for Sunset.
- [x] Provide explicit opt-in approximate location detection and manual coordinate entry.
- [x] Keep existing font/theme override controls unchanged below the theme row.

### Task 4: Verification and PR

- [x] Open PR #65 against `master` so GitHub Actions exercises the branch.
- [ ] Run the final repository CI/build/test workflows to completion.
- [ ] Review the final diff for backward compatibility, runtime lifecycle, and XAML correctness.
- [ ] Update the PR description with final verification evidence.
