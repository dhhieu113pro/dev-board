# LocalLLM Hugging Face Download Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add background Hugging Face GGUF discovery/download to LocalLLM settings with progress, cancel/retry, and automatic model selection.

**Architecture:** Add a singleton downloader in `src/AI` that owns active transfers independently of the Preferences window. Add a compact Avalonia panel in a partial Preferences implementation and inject it into the existing LocalLLM settings editor when the window opens.

**Tech Stack:** .NET 10, C#, Avalonia 11.x, CommunityToolkit.Mvvm, HttpClient, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-30-local-llm-huggingface-download-design.md`

## Global Constraints
- Public Hugging Face repositories/direct `.gguf` URLs only in v1.
- Only `https://huggingface.co` sources are accepted.
- Downloads use `.part` files and never select partial files as models.
- Closing Preferences must not cancel an active download.
- Completed downloads set the selected `AI.Service.LocalModelPath`.
- Keep the existing LocalLLM runtime/backend implementation unchanged.

---

### Task 1: Source parsing and model discovery

**Files:**
- Create: `src/AI/HuggingFaceModelDownloader.cs`
- Test: `tests/SourceGit.Tests/HuggingFaceModelDownloaderTests.cs`

**Interfaces:**
- Produces: `HuggingFaceModelFile(string FileName, string DownloadUrl, long? Size)`.
- Produces: `HuggingFaceModelDownloader.ParseSource(string)` and `ResolveFilesAsync(string, CancellationToken)`.

- [ ] **Step 1: Write failing parser tests** for `owner/repo`, repository URLs, direct resolve URLs, invalid hosts, and non-GGUF direct URLs.
- [ ] **Step 2: Run** `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter HuggingFaceModelDownloaderTests` and verify RED.
- [ ] **Step 3: Implement parser and Hugging Face API discovery**, filtering siblings to `.gguf`.
- [ ] **Step 4: Re-run focused tests** and verify PASS.
- [ ] **Step 5: Commit** `feat: add Hugging Face GGUF discovery`.

### Task 2: Background download lifecycle

**Files:**
- Modify: `src/AI/HuggingFaceModelDownloader.cs`
- Test: `tests/SourceGit.Tests/HuggingFaceModelDownloaderTests.cs`

**Interfaces:**
- Produces: `HuggingFaceDownloadState` observable progress model.
- Produces: `StartDownload(Service, HuggingFaceModelFile)` and `Cancel(Service)`.

- [ ] **Step 1: Add failing tests** covering `.part` destination naming, completed-file promotion, cancellation state, retry/resume request construction, and auto-selection callback behavior.
- [ ] **Step 2: Run focused tests** and verify RED.
- [ ] **Step 3: Implement singleton transfer ownership** with streaming HttpClient reads, Range resume, progress/speed/ETA calculation, cancellation, retry, and final rename.
- [ ] **Step 4: Run focused tests** and verify PASS.
- [ ] **Step 5: Commit** `feat: download GGUF models in background`.

### Task 3: LocalLLM Preferences UI

**Files:**
- Create: `src/Views/HuggingFaceDownloadPanel.cs`
- Create: `src/Views/Preferences.HuggingFace.cs`

**Interfaces:**
- Consumes: `HuggingFaceModelDownloader.Instance` and `AI.Service`.
- Produces: source input, GGUF picker, Load Files, Download, Cancel, Retry, progress bar, bytes/speed/ETA/status display.

- [ ] **Step 1: Add the programmatic Avalonia panel** and bind it to the selected LocalLLM service/downloader state.
- [ ] **Step 2: Inject one panel** immediately after `Default Model (.gguf)`/status in each rendered LocalLLM service editor from `Preferences.OnOpened`.
- [ ] **Step 3: Verify reopening Preferences reconnects to any active transfer** owned by the singleton.
- [ ] **Step 4: Commit** `feat: add Hugging Face downloader to LocalLLM settings`.

### Task 4: Verification

**Files:** none

- [ ] **Step 1: Run** `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter HuggingFaceModelDownloaderTests`.
- [ ] **Step 2: Run** `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj`.
- [ ] **Step 3: Run** `dotnet build`.
- [ ] **Step 4: Create a PR** targeting `feature/local-llm-provider` and inspect GitHub Actions before claiming completion.
