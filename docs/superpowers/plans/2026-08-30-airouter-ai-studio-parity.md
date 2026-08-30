# AI Router AI Studio Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port AI Studio's provider-routing behavior into DevBoard so `all` mode discovers and tries provider models, OpenCode/DeepSeek gets provider-specific compatibility behavior, and `/v1/chat/completions` plus `/v1/responses` share one routing path.

**Architecture:** Keep DevBoard's Avalonia settings UI, `ai-router.json` persistence, and embedded ASP.NET host as adapters. Replace the simplified single-model routing behavior with AI Studio-style provider metadata, provider model discovery, strict `{providerId}/{model}` resolution, provider/model fallback, and provider-specific OpenCode/DeepSeek handling.

**Tech Stack:** .NET 10, C#, ASP.NET Core minimal host, xUnit, `System.Text.Json`, `HttpClient`.

**Spec:** Approved chat design from 2026-08-30: bring AI Studio AI Router behavior into DevBoard rather than layering more one-off compatibility patches.

## Global Constraints

- Do not implement on `master`; use `refactor/airouter-ai-studio-parity`.
- Preserve existing DevBoard AI Router UI and persisted provider settings compatibility.
- Preserve explicit upstream errors for explicitly selected provider/model requests.
- `model: "all"` must try additional configured/live models when one model returns an upstream failure such as HTTP 400 `Model is unavailable`.
- Explicit `{providerId}/{model}` requests must not silently switch to another model.
- `/v1/responses` must continue to translate through Chat Completions rather than requiring providers to expose Responses natively.
- OpenCode/DeepSeek requests must use `thinking: { "type": "disabled" }` compatibility behavior.

---

### Task 1: Lock routing parity with failing tests

**Files:**
- Modify: `tests/DevBoard.Tests/AIRouterTests.cs`
- Modify: `tests/DevBoard.Tests/AIRouterProviderSettingsTests.cs`

**Interfaces:**
- Consumes: existing `AIRouter`, `IAIProvider`, `AIRouterProviderSettings`.
- Produces: regression expectations for configured model fallback, explicit-model strictness, and imported AI Studio routing metadata.

- [ ] **Step 1: Add a failing `all`-mode model fallback test**

Add a provider test double that exposes `Models = ["deepseek-v4-flash-free", "fallback-model"]` and returns HTTP 400 for the first model and success for the second. Assert that `AIRouter.RouteAsync(new AIRouterRequest("all", ...))` tries both models and succeeds on `fallback-model`.

- [ ] **Step 2: Add a failing explicit-model strictness test**

Request `opencode/deepseek-v4-flash-free`; return HTTP 400 for that model; assert the router does not try `fallback-model`.

- [ ] **Step 3: Add a failing AI Studio metadata import test**

Import provider JSON containing `mode`, `passthroughModels`, `useAutoProxy`, `models`, and `maxRetries`; assert DevBoard retains those values.

- [ ] **Step 4: Verify RED in CI**

Push only the test changes and confirm the focused/unit CI fails because the production interfaces and behavior do not yet satisfy these tests.

- [ ] **Step 5: Commit**

Commit message: `test: lock AI Studio router parity`

### Task 2: Port AI Studio provider configuration semantics

**Files:**
- Modify: `src/AI/Routing/AIRouterProviderSettings.cs`

**Interfaces:**
- Consumes: existing JSON settings/import/export.
- Produces: `Mode`, `PassthroughModels`, `UseAutoProxy`, configured model list, retries, timeout, headers available to the router/host.

- [ ] **Step 1: Extend provider settings**

Add `PassthroughModels`, `UseAutoProxy`, and `Mode` (`fallback` default) while preserving current defaults and serialization compatibility.

- [ ] **Step 2: Port AI Studio import fields**

Read `mode`, `passthroughModels`, `useAutoProxy`, `maxRetries`, and timeout when present in AI Studio provider arrays.

- [ ] **Step 3: Preserve fields in `Clone`**

Ensure duplicate/export/save paths retain the new values.

- [ ] **Step 4: Run focused tests**

Expected: provider settings tests pass; routing parity tests remain red.

- [ ] **Step 5: Commit**

Commit message: `feat: align AI Router provider settings`

### Task 3: Port provider model discovery and provider-specific compatibility

**Files:**
- Modify: `src/AI/Routing/AIRouter.cs`
- Modify: `src/AI/Routing/OpenAICompatibleProvider.cs`
- Modify: `src/AI/Hosting/AIRouterHostService.cs`
- Create: `src/AI/Routing/DeepSeekCompatibleProvider.cs`

**Interfaces:**
- Consumes: `AIRouterProviderSettings.Models`, `DefaultModel`, `Mode`, provider `BaseUrl`.
- Produces: provider model candidates from static configuration first and live `/v1/models` second; provider-specific OpenCode/DeepSeek payload handling.

- [ ] **Step 1: Extend `IAIProvider` with model discovery**

Expose provider model candidates and `ListModelsAsync`. Keep test doubles easy to implement with default interface behavior where practical.

- [ ] **Step 2: Make `OpenAICompatibleProvider` list models**

Resolve `/models` exactly as AI Studio does for base URLs ending in `/v1` and parse OpenAI-compatible `{ "data": [{ "id": ... }] }` responses.

- [ ] **Step 3: Add `DeepSeekCompatibleProvider`**

Subclass/wrap the generic provider and inject `thinking: { "type": "disabled" }` for Chat Completions. Use it for provider id `opencode`, ids starting with `deepseek`, or DeepSeek-like base URLs.

- [ ] **Step 4: Wire host settings into providers**

Pass configured models/default model to each provider and choose the provider implementation using AI Studio's registration rule.

- [ ] **Step 5: Run focused tests**

Expected: provider/model plumbing tests pass; `all` routing fallback test may still be red until Task 4.

- [ ] **Step 6: Commit**

Commit message: `feat: port AI Studio provider model discovery`

### Task 4: Port AI Studio routing semantics

**Files:**
- Modify: `src/AI/Routing/AIRouter.cs`
- Modify: `tests/DevBoard.Tests/AIRouterTests.cs`

**Interfaces:**
- Consumes: provider id, configured/live models, provider mode, HTTP results.
- Produces: strict explicit-model routing and AI Studio-style all-mode provider/model fallback.

- [ ] **Step 1: Implement strict model resolution**

Support `all`, `{providerId}`, and `{providerId}/{model}`. Keep `oc` as an alias for `opencode` if present.

- [ ] **Step 2: Implement provider-model candidate iteration**

For `all`, get configured models first, then live models, then default/provider id fallback. Try each model before moving to the next provider.

- [ ] **Step 3: Preserve explicit request strictness**

For `{providerId}/{model}`, try only that model and return its upstream error unchanged.

- [ ] **Step 4: Preserve Responses translation**

Keep the existing `/v1/responses` -> Chat Completions -> Responses translation around the newly selected provider/model result.

- [ ] **Step 5: Verify GREEN in CI**

Run the focused AI Router regression suite and full unit suite. Expected: all new and existing AI Router tests pass.

- [ ] **Step 6: Commit**

Commit message: `fix: port AI Studio router fallback semantics`

### Task 5: Final verification and PR

**Files:**
- Modify only if verification exposes a regression.

**Interfaces:**
- Consumes: complete branch.
- Produces: reviewable PR with evidence.

- [ ] **Step 1: Run full CI**

Confirm unit tests, build/publish matrix, and packaging checks triggered by the branch/PR.

- [ ] **Step 2: Verify the regression scenario**

Confirm tests cover OpenCode returning HTTP 400 `Model is unavailable` for one model while `all` mode succeeds with another model.

- [ ] **Step 3: Review diff**

Ensure the change is confined to AI Router source/tests/settings plus this plan.

- [ ] **Step 4: Open PR**

Title: `fix: port AI Studio AI Router semantics`

PR body must summarize provider model discovery, OpenCode/DeepSeek specialization, all-mode fallback, explicit-model strictness, Responses compatibility, and CI evidence.
