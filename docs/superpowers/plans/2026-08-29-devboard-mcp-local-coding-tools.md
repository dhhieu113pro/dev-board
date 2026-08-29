# DevBoard MCP Local Coding Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LocalCodingMcp-compatible workspace, file, Git, shell, skill, and execution-history tools to DevBoard's existing authenticated MCP endpoint without exposing arbitrary host paths.

**Architecture:** Keep `SourceGitMcpHost` as the only MCP server. Add focused services plus thin MCP adapters. The current `ViewModels.Launcher` is the source of truth for allowed repository/worktree roots: each open `Repository.FullPath` and each `Repository.Worktrees[*].FullPath` is eligible. `open_workspace` returns a deterministic `workspace_id`, and every file/Git/shell call requires that id, so there is no process-global active workspace shared between MCP clients.

**Tech Stack:** .NET 10, C#, `ModelContextProtocol.AspNetCore` 2.2.0, ASP.NET Core slim host, xUnit 2.9.3, existing DevBoard launcher/repository/worktree models, `Native.OS.DataDir`.

**Spec:** `docs/superpowers/specs/2026-08-29-devboard-mcp-local-coding-tools-design.md`

## Global Constraints

- Preserve loopback binding, bearer auth, request limiting, stateful HTTP transport, legacy SSE, and existing MCP lifecycle.
- Preserve all existing `sourcegit_*` terminal tool contracts.
- Do not add LocalCodingMcp as a package, project reference, submodule, executable, or runtime dependency.
- Do not add free-form allowed roots; roots come from the currently open DevBoard launcher state only.
- Require `workspace_id` for file, Git, and shell calls.
- Reject rooted file paths, traversal, symlink/reparse escapes, and sensitive filenames before I/O.
- Bound reads, binary payloads, search, command output/time, remote skill downloads, and history files.
- Store skills/history below `Path.Combine(Native.OS.DataDir, "mcp")`; constructors accept explicit paths for isolated tests.
- Add no third-party dependency unless the plan is revised first.
- Use TDD and make a focused commit after every completed task.

## Planned Files

Create services in `src/Mcp/Services/`: `McpWorkspaceRegistry.cs`, `McpPathSandbox.cs`, `McpSensitiveFileFilter.cs`, `McpPatchApplier.cs`, `McpFileService.cs`, `McpCommandService.cs`, `McpGitService.cs`, `McpBuiltInSkillCatalog.cs`, `McpSkillStore.cs`, `McpSkillRouter.cs`, `McpRemoteSkillFetcher.cs`, `McpSkillService.cs`, `McpExecutionHistory.cs`.

Create adapters in `src/Mcp/Tools/`: `McpWorkspaceTools.cs`, `McpFileTools.cs`, `McpGitTools.cs`, `McpShellTools.cs`, `McpSkillTools.cs`, `McpHistoryTools.cs`.

Create tests in `tests/SourceGit.Tests/`: `McpWorkspaceRegistryTests.cs`, `McpPathSandboxTests.cs`, `McpFileServiceTests.cs`, `McpCommandServiceTests.cs`, `McpGitServiceTests.cs`, `McpSkillServiceTests.cs`, `McpExecutionHistoryTests.cs`, `McpToolRegistrationTests.cs`, `McpCodingIntegrationTests.cs`.

Modify `src/Mcp/SourceGitMcpOptions.cs`, `src/Mcp/SourceGitMcpHost.cs`, `src/Mcp/SourceGitMcpService.cs`, `src/Mcp/SourceGitMcpBootstrap.cs`, and `README.md`.

---

### Task 1: Add launcher-backed workspace discovery

**Files:** Create `src/Mcp/Services/McpWorkspaceRegistry.cs`, `src/Mcp/Tools/McpWorkspaceTools.cs`, `tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs`; modify `src/Mcp/SourceGitMcpBootstrap.cs`, `src/Mcp/SourceGitMcpService.cs`, `src/Mcp/SourceGitMcpHost.cs` for provider plumbing.

- [ ] **Step 1: Write failing registry tests.** Test exact known-root opening, unknown-root rejection, deterministic id reuse, id lookup, and removal of a root from the live provider.

```csharp
[Fact]
public void Open_Rejects_Unknown_Root()
{
    var known = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
    var unknown = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
    try
    {
        var registry = new McpWorkspaceRegistry(() => new[] { known });
        Assert.Throws<UnauthorizedAccessException>(() => registry.Open(unknown));
    }
    finally
    {
        Directory.Delete(known, true);
        Directory.Delete(unknown, true);
    }
}
```

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpWorkspaceRegistryTests`.** Expected: FAIL because the registry does not exist.

- [ ] **Step 3: Implement `McpWorkspaceRegistry`.** Constructor: `Func<IReadOnlyCollection<string>> knownRootsProvider`. Public operations: `Open`, `Get`, `GetRoot`, `List`, `GetAllowedRoots`. Canonicalize with `Path.GetFullPath`, resolve existing links, use case-insensitive comparison on Windows/macOS and ordinal comparison on Linux, and require an exact known root rather than a descendant. Derive ids as the first 12 lowercase hex characters of SHA-256 of the canonical path.

```csharp
private static string CreateId(string root)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(root));
    return Convert.ToHexString(hash).ToLowerInvariant()[..12];
}
```

- [ ] **Step 4: Wire the root provider from the launcher.** In `SourceGitMcpBootstrap.OnLauncherLoaded`, obtain `ViewModels.Launcher` from the view data context and pass it into `SourceGitMcpService.Initialize`. Build a provider that runs on `Dispatcher.UIThread` and returns every open `Repository.FullPath` plus every non-empty `Repository.Worktrees` item's `FullPath`. Deduplicate paths before returning them. Evaluate the provider per request so tab/worktree changes are visible without restarting MCP.

- [ ] **Step 5: Pass the provider into `SourceGitMcpHost` and register the registry with an explicit factory:**

```csharp
builder.Services.AddSingleton(_ => new McpWorkspaceRegistry(_knownRootsProvider));
```

- [ ] **Step 6: Implement `McpWorkspaceTools` wire names `open_workspace`, `list_workspaces`, `get_allowed_roots`.** `open_workspace` returns `workspace_id`, `root`, `opened_at`, and a message telling the client to pass that id to subsequent calls. Do not store an active workspace.

- [ ] **Step 7: Re-run the focused tests.** Expected: PASS.

- [ ] **Step 8: Commit.**

```bash
git add src/Mcp/Services/McpWorkspaceRegistry.cs src/Mcp/Tools/McpWorkspaceTools.cs src/Mcp/SourceGitMcpBootstrap.cs src/Mcp/SourceGitMcpService.cs src/Mcp/SourceGitMcpHost.cs tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs
git commit -m "feat: add MCP workspace registry"
```

### Task 2: Add path sandboxing and sensitive-file filtering

**Files:** Create `src/Mcp/Services/McpPathSandbox.cs`, `src/Mcp/Services/McpSensitiveFileFilter.cs`, `tests/SourceGit.Tests/McpPathSandboxTests.cs`.

- [ ] **Step 1: Write failing tests.** Reject `../outside.txt`, `../../outside.txt`, all rooted paths, and a symlink/reparse path that exits the workspace. Where symlink creation is unsupported, skip only that platform-specific test. Test the complete sensitive list: `.env`, `.env.local`, `.env.production`, `.env.development`, `id_rsa`, `id_rsa.pub`, `id_ed25519`, `id_ed25519.pub`, `id_ecdsa`, `id_ecdsa.pub`, `*.pem`, `*.pfx`, `*.p12`, `*.key`, `credentials.json`, `secrets.json`, `appsettings.Production.json`, `.npmrc`, `.netrc`, `authorized_keys`, `known_hosts`.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpPathSandboxTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpPathSandbox.Resolve(workspaceRoot, relativePath)`.** Reject null/blank/rooted paths. Resolve each existing segment with `FileSystemInfo.ResolveLinkTarget(true)`. Accept only candidates equal to the canonical root or beginning with canonical root plus a directory separator under the OS path comparer.

- [ ] **Step 4: Implement `McpSensitiveFileFilter`.** Use case-insensitive exact-name matches and case-insensitive suffix matching for `*.<extension>` patterns.

- [ ] **Step 5: Re-run focused tests.** Expected: PASS.

- [ ] **Step 6: Commit.**

```bash
git add src/Mcp/Services/McpPathSandbox.cs src/Mcp/Services/McpSensitiveFileFilter.cs tests/SourceGit.Tests/McpPathSandboxTests.cs
git commit -m "feat: sandbox MCP workspace paths"
```

### Task 3: Add bounded file, directory, search, and patch tools

**Files:** Create `src/Mcp/Services/McpPatchApplier.cs`, `src/Mcp/Services/McpFileService.cs`, `src/Mcp/Tools/McpFileTools.cs`, `tests/SourceGit.Tests/McpFileServiceTests.cs`; modify `src/Mcp/SourceGitMcpOptions.cs`.

**Wire names:** `list_directory`, `read_file`, `write_file`, `read_binary_file`, `write_binary_file`, `apply_patch`, `search_files`, `create_directory`, `move_file`, `delete_file`.

- [ ] **Step 1: Write failing tests.** Cover sensitive-file read/write rejection, text/binary size rejection, data-URL base64 stripping, invalid base64, bounded search, skipped sensitive/binary/oversized search files, invalid regex, valid patch, invalid patch atomicity, confined move/delete, and non-empty-directory delete rejection.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpFileServiceTests`.** Expected: FAIL.

- [ ] **Step 3: Add constants to `SourceGitMcpOptions`:**

```csharp
public const int DefaultCommandTimeoutSeconds = 30;
public const int DefaultMaxCommandOutputBytes = 1_048_576;
public const int DefaultMaxFileReadBytes = 10 * 1024 * 1024;
public const int DefaultMaxSearchFileBytes = 2 * 1024 * 1024;
public const int DefaultMaxSearchResults = 50;
```

- [ ] **Step 4: Implement `McpPatchApplier.Apply(original, patch)` as a pure unified-diff operation.** Port LocalCodingMcp hunk semantics. Reject malformed context/removal lines before returning changed text; write only after the whole patch succeeds.

- [ ] **Step 5: Implement `McpFileService`.** Every operation resolves the `workspace_id` through `McpWorkspaceRegistry`, then the relative path through `McpPathSandbox`, then applies `McpSensitiveFileFilter` before I/O. Check file size before reads. Clamp `max_results` to `1..DefaultMaxSearchResults`. Do not search files above `DefaultMaxSearchFileBytes` or common binary extensions.

- [ ] **Step 6: Implement `McpFileTools` as serialization-only adapters.** Keep LocalCodingMcp parameter names such as `workspace_id`, `base64_content`, `start_line`, `end_line`, and `max_results`.

- [ ] **Step 7: Re-run focused tests.** Expected: PASS.

- [ ] **Step 8: Commit.**

```bash
git add src/Mcp/SourceGitMcpOptions.cs src/Mcp/Services/McpPatchApplier.cs src/Mcp/Services/McpFileService.cs src/Mcp/Tools/McpFileTools.cs tests/SourceGit.Tests/McpFileServiceTests.cs
git commit -m "feat: add MCP file tools"
```

### Task 4: Add shell and Git tools

**Files:** Create `src/Mcp/Services/McpCommandService.cs`, `src/Mcp/Services/McpGitService.cs`, `src/Mcp/Tools/McpShellTools.cs`, `src/Mcp/Tools/McpGitTools.cs`, `tests/SourceGit.Tests/McpCommandServiceTests.cs`, `tests/SourceGit.Tests/McpGitServiceTests.cs`.

- [ ] **Step 1: Write failing command tests.** Verify cwd equals the selected workspace, non-zero exit is returned, stdout/stderr are bounded, timeout terminates the process tree and reports `TimedOut`, and external cancellation terminates the process and propagates cancellation.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpCommandServiceTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpCommandService(timeoutSeconds, maxOutputBytes)`.** Use `cmd.exe /d /s /c` on Windows and `/bin/sh -lc` on Unix. Set `WorkingDirectory` to the canonical workspace root, redirect both output streams, use a linked cancellation token with `CancelAfter`, and call `Kill(entireProcessTree: true)` for timeout/cancellation. Return `ExitCode`, `Stdout`, `Stderr`, `DurationMs`, `TimedOut`, `Truncated`.

- [ ] **Step 4: Re-run command tests.** Expected: PASS.

- [ ] **Step 5: Write failing Git tests.** In a temporary repo run `git init`, configure repository-local user/email, commit one file, modify/stage it, then assert status, unstaged diff, staged diff, and log. Verify log count clamps to `1..50`; non-repository roots return non-zero/error.

- [ ] **Step 6: Implement `McpGitService` with fixed commands only:** `git status --porcelain=v1 -b`, `git diff`, `git diff --cached`, `git log -n {clampedCount} --oneline`.

- [ ] **Step 7: Implement adapters.** Wire names: `run_command`, `git_status`, `git_diff`, `git_log`. Preserve LocalCodingMcp response properties; add `timed_out` and `truncated` to shell output.

- [ ] **Step 8: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~McpCommandServiceTests|FullyQualifiedName~McpGitServiceTests"`.** Expected: PASS.

- [ ] **Step 9: Commit.**

```bash
git add src/Mcp/Services/McpCommandService.cs src/Mcp/Services/McpGitService.cs src/Mcp/Tools/McpShellTools.cs src/Mcp/Tools/McpGitTools.cs tests/SourceGit.Tests/McpCommandServiceTests.cs tests/SourceGit.Tests/McpGitServiceTests.cs
git commit -m "feat: add MCP shell and git tools"
```

### Task 5: Add skill storage, routing, built-ins, and remote updates

**Files:** Create `src/Mcp/Services/McpBuiltInSkillCatalog.cs`, `McpSkillStore.cs`, `McpSkillRouter.cs`, `McpRemoteSkillFetcher.cs`, `McpSkillService.cs`, `src/Mcp/Tools/McpSkillTools.cs`, `tests/SourceGit.Tests/McpSkillServiceTests.cs`.

**Wire names:** `route_skills`, `load_skills`, `load_enabled_skills`, `list_skills`, `get_skill`, `set_skill_enabled`, `create_skill`, `update_skill`, `install_skill`, `check_skill_updates`, `update_skill_from_source`, `delete_skill`.

- [ ] **Step 1: Write failing local tests.** Verify built-ins `caveman`, `hallmark`, `superpowers`, `ponytail` seed once and disabled; built-ins cannot be deleted but can be toggled; custom names match `^[A-Za-z0-9._-]{1,64}$`; CRUD persists across a new store instance; routing/loading excludes disabled skills.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpSkillServiceTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpSkillStore(string skillsDirectory)`.** Store `<skillsDirectory>/<name>/SKILL.md` and `metadata.json`; update via temporary file plus atomic rename; preserve enabled state; reject deletion of built-ins.

- [ ] **Step 4: Port the four built-in skill documents and implement routing.** Parse `name`, `description`, optional `license` from front matter; rank enabled skills by case-insensitive task/name/description token overlap, score descending then name.

- [ ] **Step 5: Write failing remote tests using a fake `HttpMessageHandler`.** Reject HTTP, URI user-info, HTTPS-to-HTTP redirect, more than 3 redirects, bodies over 1,048,576 bytes, and malformed skill documents. Verify update checks do not mutate, failed updates preserve old state, successful operations record source URL, resolved URL, SHA-256, license, and enabled state.

- [ ] **Step 6: Implement `McpRemoteSkillFetcher`.** Production uses `HttpClientHandler { AllowAutoRedirect = false }`, timeout 15 seconds, max bytes 1,048,576, max redirects 3. Every hop must be HTTPS with empty `UserInfo`.

- [ ] **Step 7: Implement `McpSkillService` and `McpSkillTools`.** Remote install/update is always explicit; checks are read-only; updates are atomic; response fields stay LocalCodingMcp-compatible.

- [ ] **Step 8: Re-run skill tests.** Expected: PASS.

- [ ] **Step 9: Commit.**

```bash
git add src/Mcp/Services/McpBuiltInSkillCatalog.cs src/Mcp/Services/McpSkillStore.cs src/Mcp/Services/McpSkillRouter.cs src/Mcp/Services/McpRemoteSkillFetcher.cs src/Mcp/Services/McpSkillService.cs src/Mcp/Tools/McpSkillTools.cs tests/SourceGit.Tests/McpSkillServiceTests.cs
git commit -m "feat: add MCP skill tools"
```

### Task 6: Add redacted execution history

**Files:** Create `src/Mcp/Services/McpExecutionHistory.cs`, `src/Mcp/Tools/McpHistoryTools.cs`, `tests/SourceGit.Tests/McpExecutionHistoryTests.cs`.

- [ ] **Step 1: Write failing tests.** Redact keys containing `token`, `password`, `secret`, `authorization`, `api_key`, `apikey`, `base64_content`, and write/update `content`; cap individual argument strings at 2,000 characters; clamp queries to `1..500`; filter by tool/success; return newest first; rotate before exceeding configured max file size.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpExecutionHistoryTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpExecutionHistory(filePath, maxArgumentLength, maxFileBytes)`.** Redact recursively before JSON serialization, write JSONL under a semaphore, and rotate active history to `.1`, replacing an older `.1`, before an append would exceed the cap. Production path: `Path.Combine(Native.OS.DataDir, "mcp", "execution-history.jsonl")`; limits: 2,000 characters and 10 MiB.

- [ ] **Step 4: Implement `get_execution_history`.** Return `{ count, history_file, entries }` and clamp count to `1..500`.

- [ ] **Step 5: Re-run focused tests.** Expected: PASS.

- [ ] **Step 6: Commit.**

```bash
git add src/Mcp/Services/McpExecutionHistory.cs src/Mcp/Tools/McpHistoryTools.cs tests/SourceGit.Tests/McpExecutionHistoryTests.cs
git commit -m "feat: add MCP execution history"
```

### Task 7: Register coding tools in the existing MCP host

**Files:** Modify `src/Mcp/SourceGitMcpHost.cs`; create `tests/SourceGit.Tests/McpToolRegistrationTests.cs`.

- [ ] **Step 1: Write a failing discovery test.** Assert the five existing `sourcegit_*` tools remain and all new workspace/file/Git/shell/skill/history wire names are present.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpToolRegistrationTests`.** Expected: FAIL.

- [ ] **Step 3: Register constructor-complete services.** Use:

```csharp
var mcpDataDir = Path.Combine(Native.OS.DataDir, "mcp");
var skillsDir = Path.Combine(mcpDataDir, "skills");
var historyPath = Path.Combine(mcpDataDir, "execution-history.jsonl");

builder.Services.AddSingleton(_registry);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(_ => new McpWorkspaceRegistry(_knownRootsProvider));
builder.Services.AddSingleton<McpPathSandbox>();
builder.Services.AddSingleton<McpSensitiveFileFilter>();
builder.Services.AddSingleton<McpFileService>();
builder.Services.AddSingleton(_ => new McpCommandService(
    SourceGitMcpOptions.DefaultCommandTimeoutSeconds,
    SourceGitMcpOptions.DefaultMaxCommandOutputBytes));
builder.Services.AddSingleton<McpGitService>();
builder.Services.AddSingleton(_ => new McpSkillStore(skillsDir));
builder.Services.AddSingleton<McpSkillRouter>();
builder.Services.AddSingleton(_ => new McpExecutionHistory(historyPath, 2_000, 10L * 1024 * 1024));
```

Register `McpRemoteSkillFetcher` and `McpSkillService` with explicit factories so their `HttpClient`, store, and router dependencies are supplied.

- [ ] **Step 4: Extend the existing MCP builder:**

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport(ConfigureTransport)
    .WithTools<SourceGitMcpTools>()
    .WithTools<McpWorkspaceTools>()
    .WithTools<McpFileTools>()
    .WithTools<McpGitTools>()
    .WithTools<McpShellTools>()
    .WithTools<McpSkillTools>()
    .WithTools<McpHistoryTools>();
```

Add a `WithRequestFilters` call-tool filter like LocalCodingMcp: time `next`, record tool name, arguments, success/error, and duration through `McpExecutionHistory`. The store redacts before persistence. History failures must be logged/swallowed and never alter the actual tool result.

- [ ] **Step 5: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~Mcp"`.** Expected: PASS, including current auth/transport tests.

- [ ] **Step 6: Commit.**

```bash
git add src/Mcp/SourceGitMcpHost.cs tests/SourceGit.Tests/McpToolRegistrationTests.cs
git commit -m "feat: register coding tools in DevBoard MCP"
```

### Task 8: Add end-to-end coverage and README usage

**Files:** Create `tests/SourceGit.Tests/McpCodingIntegrationTests.cs`; modify `README.md`.

- [ ] **Step 1: Write an end-to-end temporary-repository test.** Run `git init`; configure `user.email=mcp-tests@example.invalid` and `user.name=MCP-Tests`; commit `README.md`; expose/open the exact root; list/read/search; write and patch a permitted file; call status/diff/log; run a harmless command and verify cwd; read history; reject `../outside`; reject `.env`.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpCodingIntegrationTests`.** Expected: PASS after Tasks 1-7.

- [ ] **Step 3: Add `## MCP server` to `README.md`.** Document endpoint/token, exact-known-workspace restriction, explicit `workspace_id` flow, all tool categories, sensitive-file blocking, and these example calls:

```text
open_workspace(path: "D:/Development/example")
read_file(path: "src/Program.cs", workspace_id: "returned-id")
git_status(workspace_id: "returned-id")
run_command(command: "dotnet test", workspace_id: "returned-id")
```

State that arbitrary filesystem paths not represented by open DevBoard repository/worktree state are unavailable to MCP.

- [ ] **Step 4: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj` and `dotnet build src/SourceGit.csproj`.** Expected: both exit 0.

- [ ] **Step 5: Commit.**

```bash
git add tests/SourceGit.Tests/McpCodingIntegrationTests.cs README.md
git commit -m "test: verify DevBoard MCP coding workflow"
```

### Task 9: Final compatibility and security verification

- [ ] Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~Mcp"`. Expected: PASS.
- [ ] Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj`. Expected: PASS.
- [ ] Run `dotnet build src/SourceGit.csproj -c Debug`. Expected: exit 0.
- [ ] Run `dotnet build src/SourceGit.csproj -c Release -p:DisableAOT=true`. Expected: exit 0.
- [ ] Run `git diff master...HEAD -- src/SourceGit.csproj .gitmodules` and `git grep -n "LocalCodingMcp" -- ':!docs/superpowers/*'`. Expected: no runtime dependency on LocalCodingMcp.
- [ ] Run `git diff --check master...HEAD` and `git status --short`. Expected: no whitespace errors and only intentional changes.
- [ ] If verification exposes a defect, fix only that defect, rerun its focused test plus the full test project, and commit with a narrow `fix:` message. Do not create an empty verification commit.

## Intentional Compatibility Difference

LocalCodingMcp allows `open_workspace` for any path beneath configured allowed roots. DevBoard deliberately replaces that model: `open_workspace` accepts only an exact canonical repository/worktree root currently represented by the running DevBoard launcher. File, Git, and shell argument names remain LocalCodingMcp-compatible, including explicit `workspace_id`.

`CodebaseMemoryTools.cs` is not ported in this feature because the approved design covers workspace, files, Git, shell, skills, and execution history; Codebase Memory remains a separate optional integration.
