# DevBoard MCP Local Coding Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LocalCodingMcp-compatible workspace, file, Git, shell, skill, and execution-history tools to DevBoard's existing authenticated MCP endpoint without exposing arbitrary host paths.

**Architecture:** Keep `SourceGitMcpHost` as the only MCP server. New MCP classes are thin adapters over focused DevBoard-native services. A launcher-backed root provider exposes only repository/worktree roots currently open in DevBoard; `open_workspace` returns a stable `workspace_id`, and every file/Git/shell call carries that id explicitly so one MCP client cannot redirect another client through shared active-workspace state.

**Tech Stack:** .NET 10, C#, `ModelContextProtocol.AspNetCore` 2.2.0, ASP.NET Core slim host, xUnit 2.9.3, existing DevBoard/SourceGit launcher/repository models, `Native.OS.DataDir` application-data storage.

**Spec:** `docs/superpowers/specs/2026-08-29-devboard-mcp-local-coding-tools-design.md`

## Global Constraints

- Preserve the current loopback binding, bearer authentication, request limiter, stateful HTTP transport, legacy SSE compatibility, startup, and shutdown behavior.
- Preserve all existing `sourcegit_*` terminal tool names and behavior.
- Do not add `local-coding-mcp` as a package, project reference, submodule, executable, or runtime dependency.
- Allowed roots come only from repository/worktree paths exposed by the current `ViewModels.Launcher`; do not add a free-form `AllowedRoots` setting.
- Require `workspace_id` on every file, Git, and shell operation.
- Reject path traversal, rooted file paths, symlink/reparse escapes, and sensitive filenames before file I/O.
- Bound file reads, binary payloads, search results, shell duration/output, remote-skill downloads, and history storage.
- Store skills and history under `Path.Combine(Native.OS.DataDir, "mcp")`, while constructors accept explicit paths so tests use temporary directories.
- Add no new third-party dependency unless implementation proves the existing framework/package set cannot satisfy a requirement; stop and revise the plan before adding one.
- Use TDD for each task and commit only after its focused tests pass.

## Planned Files

Create production files under `src/Mcp/Services/`: `McpWorkspaceRegistry.cs`, `McpPathSandbox.cs`, `McpSensitiveFileFilter.cs`, `McpPatchApplier.cs`, `McpFileService.cs`, `McpCommandService.cs`, `McpGitService.cs`, `McpBuiltInSkillCatalog.cs`, `McpSkillStore.cs`, `McpSkillRouter.cs`, `McpRemoteSkillFetcher.cs`, `McpSkillService.cs`, `McpExecutionHistory.cs`.

Create MCP adapters under `src/Mcp/Tools/`: `McpWorkspaceTools.cs`, `McpFileTools.cs`, `McpGitTools.cs`, `McpShellTools.cs`, `McpSkillTools.cs`, `McpHistoryTools.cs`.

Create tests under `tests/SourceGit.Tests/`: `McpWorkspaceRegistryTests.cs`, `McpPathSandboxTests.cs`, `McpFileServiceTests.cs`, `McpCommandServiceTests.cs`, `McpGitServiceTests.cs`, `McpSkillServiceTests.cs`, `McpExecutionHistoryTests.cs`, `McpToolRegistrationTests.cs`, `McpCodingIntegrationTests.cs`.

Modify `src/Mcp/SourceGitMcpOptions.cs`, `src/Mcp/SourceGitMcpHost.cs`, `src/Mcp/SourceGitMcpService.cs`, `src/Mcp/SourceGitMcpBootstrap.cs`, and `README.md`. Modify `THIRD-PARTY-LICENSES.md` only if a new dependency is actually introduced; the intended implementation adds none.

---

### Task 1: Feed current DevBoard repositories/worktrees into a stable MCP workspace registry

**Files:** Create `src/Mcp/Services/McpWorkspaceRegistry.cs`, `src/Mcp/Tools/McpWorkspaceTools.cs`, `tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs`; modify `src/Mcp/SourceGitMcpBootstrap.cs`, `src/Mcp/SourceGitMcpService.cs`, `src/Mcp/SourceGitMcpHost.cs` only for root-provider plumbing.

**Production contract:**

```csharp
public sealed record McpWorkspaceInfo(string Id, string RootPath, DateTimeOffset OpenedAt);

public sealed class McpWorkspaceRegistry
{
    public McpWorkspaceRegistry(Func<IReadOnlyCollection<string>> knownRootsProvider);
    public McpWorkspaceInfo Open(string pathOrId);
    public McpWorkspaceInfo Get(string workspaceId);
    public string GetRoot(string workspaceId);
    public IReadOnlyCollection<McpWorkspaceInfo> List();
    public IReadOnlyCollection<string> GetAllowedRoots();
}
```

Wire names: `open_workspace`, `list_workspaces`, `get_allowed_roots`.

- [ ] **Step 1: Write failing registry tests.** Use one temporary known root and a different unknown root. Assert an unknown path throws `UnauthorizedAccessException`; opening the same canonical root twice yields the same id; an already-open id resolves to the same record; and removing a root from the provider makes it disappear from `get_allowed_roots` and prevents future opening by path.

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

- [ ] **Step 3: Implement canonical roots and stable ids.** Canonicalize via `Path.GetFullPath`, resolve existing links, deduplicate with `StringComparer.OrdinalIgnoreCase` on Windows/macOS and `StringComparer.Ordinal` on Linux, require an exact known root rather than any descendant, and derive the id from the canonical path:

```csharp
private static string CreateId(string root)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(root));
    return Convert.ToHexString(bytes).ToLowerInvariant()[..12];
}
```

Keep opened records in a concurrent dictionary keyed by id. `Get` must also verify its root is still present in the current known-root snapshot before returning it.

- [ ] **Step 4: Make launcher state the production root provider.** Change `SourceGitMcpBootstrap.OnLauncherLoaded` to read `view.DataContext as ViewModels.Launcher` and pass it to `SourceGitMcpService.Initialize`. Change `SourceGitMcpService.Initialize` to retain a `Func<IReadOnlyCollection<string>>` that enumerates `launcher.Pages` where `page.Data is ViewModels.Repository`, adds each `repo.FullPath`, and adds every non-empty `repo.Worktrees[*].FullPath`/worktree path property verified from the existing worktree model during implementation. The provider is evaluated on each call so opening/closing tabs and worktrees is reflected without restarting MCP. Access launcher collections on `Dispatcher.UIThread` when the provider is invoked from the MCP server thread.

- [ ] **Step 5: Inject the provider into `SourceGitMcpHost` and then into `McpWorkspaceRegistry`.** Replace parameterless registration with a factory so constructor types match:

```csharp
builder.Services.AddSingleton(_ => new McpWorkspaceRegistry(_knownRootsProvider));
```

- [ ] **Step 6: Implement thin workspace tools.** `open_workspace(path)` returns `workspace_id`, `root`, `opened_at`, and the message `Workspace opened. Pass workspace_id to subsequent coding tool calls.` `list_workspaces` and `get_allowed_roots` serialize registry snapshots. No method stores a global active workspace.

- [ ] **Step 7: Re-run the focused tests.** Expected: PASS.

- [ ] **Step 8: Commit.**

```bash
git add src/Mcp/Services/McpWorkspaceRegistry.cs src/Mcp/Tools/McpWorkspaceTools.cs src/Mcp/SourceGitMcpBootstrap.cs src/Mcp/SourceGitMcpService.cs src/Mcp/SourceGitMcpHost.cs tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs
git commit -m "feat: add MCP workspace registry"
```

### Task 2: Add canonical path confinement and sensitive-file blocking

**Files:** Create `src/Mcp/Services/McpPathSandbox.cs`, `src/Mcp/Services/McpSensitiveFileFilter.cs`, `tests/SourceGit.Tests/McpPathSandboxTests.cs`.

- [ ] **Step 1: Write failing confinement tests.** Assert `../outside.txt`, `../../outside.txt`, and any rooted path are rejected. Where `Directory.CreateSymbolicLink` succeeds, create `workspace/link` targeting a directory outside the workspace and assert `Resolve(workspace, "link/secret.txt")` is rejected. Assert the default filter blocks `.env`, `.env.local`, `.env.production`, `.env.development`, `id_rsa`, `id_rsa.pub`, `id_ed25519`, `id_ed25519.pub`, `id_ecdsa`, `id_ecdsa.pub`, `*.pem`, `*.pfx`, `*.p12`, `*.key`, `credentials.json`, `secrets.json`, `appsettings.Production.json`, `.npmrc`, `.netrc`, `authorized_keys`, and `known_hosts`.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpPathSandboxTests`.** Expected: FAIL.

- [ ] **Step 3: Implement relative-only sandboxing.** Reject null/blank/rooted input. Resolve both root and candidate segment-by-segment using `FileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)`. Accept the candidate only if it equals the root or begins with the normalized root plus a directory separator under the OS path comparison.

```csharp
public string Resolve(string workspaceRoot, string relativePath)
{
    if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        throw new UnauthorizedAccessException("MCP paths must be non-empty and workspace-relative.");

    var root = ResolveSymbolicLinks(Path.GetFullPath(workspaceRoot))
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var candidate = ResolveSymbolicLinks(Path.GetFullPath(Path.Combine(root, relativePath)));
    var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    var inside = candidate.Equals(root, comparison) ||
                 candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison) ||
                 candidate.StartsWith(root + Path.AltDirectorySeparatorChar, comparison);
    if (!inside)
        throw new UnauthorizedAccessException("Path is outside the selected workspace.");
    return candidate;
}
```

- [ ] **Step 4: Implement `McpSensitiveFileFilter` with the exact default list above, case-insensitive exact filename matching, and `*.<ext>` suffix matching.**

- [ ] **Step 5: Re-run focused tests.** Expected: PASS.

- [ ] **Step 6: Commit.**

```bash
git add src/Mcp/Services/McpPathSandbox.cs src/Mcp/Services/McpSensitiveFileFilter.cs tests/SourceGit.Tests/McpPathSandboxTests.cs
git commit -m "feat: sandbox MCP workspace paths"
```

### Task 3: Add bounded file, directory, search, and patch services/tools

**Files:** Create `src/Mcp/Services/McpPatchApplier.cs`, `src/Mcp/Services/McpFileService.cs`, `src/Mcp/Tools/McpFileTools.cs`, `tests/SourceGit.Tests/McpFileServiceTests.cs`; modify `src/Mcp/SourceGitMcpOptions.cs`.

**Wire names:** `list_directory`, `read_file`, `write_file`, `read_binary_file`, `write_binary_file`, `apply_patch`, `search_files`, `create_directory`, `move_file`, `delete_file`.

- [ ] **Step 1: Write failing file tests.** Cover sensitive read/write rejection; text and binary max-size rejection; base64 data-URL prefix stripping; search skips blocked/binary/oversized files; search caps results; invalid regex returns an argument error; valid unified diff changes only the expected hunk; invalid patch leaves original bytes unchanged; move/delete cannot escape; deleting a non-empty directory is rejected.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpFileServiceTests`.** Expected: FAIL.

- [ ] **Step 3: Add safe defaults to `SourceGitMcpOptions`.**

```csharp
public const int DefaultCommandTimeoutSeconds = 30;
public const int DefaultMaxCommandOutputBytes = 1_048_576;
public const int DefaultMaxFileReadBytes = 10 * 1024 * 1024;
public const int DefaultMaxSearchFileBytes = 2 * 1024 * 1024;
public const int DefaultMaxSearchResults = 50;
```

- [ ] **Step 4: Implement `McpPatchApplier.Apply(string original, string patch)` as a pure unified-diff operation.** Port the LocalCodingMcp hunk parsing behavior; reject malformed context/removal lines before returning modified content. `McpFileService` must not write until the complete patch succeeds.

- [ ] **Step 5: Implement `McpFileService`.** Every operation obtains the root from `McpWorkspaceRegistry.GetRoot`, resolves paths through `McpPathSandbox`, and invokes `McpSensitiveFileFilter` before I/O. Check `FileInfo.Length` before text/binary reads. Clamp `max_results` to `1..DefaultMaxSearchResults`; never read search files above `DefaultMaxSearchFileBytes`; skip common binary extensions. Use LocalCodingMcp-compatible response semantics.

```csharp
public sealed record McpSearchHit(string File, int Line, string Text);
```

- [ ] **Step 6: Implement `McpFileTools` as adapters only.** Keep wire parameter names `workspace_id`, `path`, `content`, `base64_content`, `start_line`, `end_line`, `query`, `max_results`, `source`, and `destination` as applicable.

- [ ] **Step 7: Re-run focused tests.** Expected: PASS.

- [ ] **Step 8: Commit.**

```bash
git add src/Mcp/SourceGitMcpOptions.cs src/Mcp/Services/McpPatchApplier.cs src/Mcp/Services/McpFileService.cs src/Mcp/Tools/McpFileTools.cs tests/SourceGit.Tests/McpFileServiceTests.cs
git commit -m "feat: add MCP file tools"
```

### Task 4: Add bounded shell execution and fixed Git operations

**Files:** Create `src/Mcp/Services/McpCommandService.cs`, `src/Mcp/Services/McpGitService.cs`, `src/Mcp/Tools/McpShellTools.cs`, `src/Mcp/Tools/McpGitTools.cs`, `tests/SourceGit.Tests/McpCommandServiceTests.cs`, `tests/SourceGit.Tests/McpGitServiceTests.cs`.

```csharp
public sealed record McpCommandResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    long DurationMs,
    bool TimedOut,
    bool Truncated);
```

Wire names: `run_command`, `git_status`, `git_diff`, `git_log`.

- [ ] **Step 1: Write failing command tests.** Verify process cwd is the selected workspace; non-zero exit is returned; output above the configured byte limit is truncated and marks `Truncated`; timeout terminates the process tree and marks `TimedOut`; external cancellation terminates the process and propagates `OperationCanceledException`.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpCommandServiceTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpCommandService(int timeoutSeconds, int maxOutputBytes)`.** Use `cmd.exe /d /s /c` on Windows and `/bin/sh -lc` on Unix; set `WorkingDirectory` to the canonical workspace root; redirect stdout/stderr; read asynchronously; use a linked CTS with `CancelAfter`; call `process.Kill(entireProcessTree: true)` on timeout/cancel. Limit each captured stream to `maxOutputBytes` UTF-8 bytes and expose truncation.

- [ ] **Step 4: Re-run command tests.** Expected: PASS.

- [ ] **Step 5: Write failing Git tests.** Create a temporary repo, run `git init`, set local user name/email, commit one file, modify it, and verify status/diff/log. Also verify `count` clamps to `1..50` and a non-repository root returns non-zero/error rather than falling back to another directory.

- [ ] **Step 6: Implement `McpGitService` using only fixed templates:** `git status --porcelain=v1 -b`, `git diff`, `git diff --cached`, `git log -n {clampedCount} --oneline`. Do not expose arbitrary Git switches.

- [ ] **Step 7: Implement adapters.** Shell JSON fields: `exit_code`, `stdout`, `stderr`, `duration_ms`, `timed_out`, `truncated`. Git status: `exit_code`, `output`, `error`; diff: `exit_code`, `diff`, `error`; log: `exit_code`, `log`, `error`.

- [ ] **Step 8: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~McpCommandServiceTests|FullyQualifiedName~McpGitServiceTests"`.** Expected: PASS.

- [ ] **Step 9: Commit.**

```bash
git add src/Mcp/Services/McpCommandService.cs src/Mcp/Services/McpGitService.cs src/Mcp/Tools/McpShellTools.cs src/Mcp/Tools/McpGitTools.cs tests/SourceGit.Tests/McpCommandServiceTests.cs tests/SourceGit.Tests/McpGitServiceTests.cs
git commit -m "feat: add MCP shell and git tools"
```

### Task 5: Add skill storage, built-ins, routing, and safe remote updates

**Files:** Create `src/Mcp/Services/McpBuiltInSkillCatalog.cs`, `McpSkillStore.cs`, `McpSkillRouter.cs`, `McpRemoteSkillFetcher.cs`, `McpSkillService.cs`, `src/Mcp/Tools/McpSkillTools.cs`, `tests/SourceGit.Tests/McpSkillServiceTests.cs`.

**Wire names:** `route_skills`, `load_skills`, `load_enabled_skills`, `list_skills`, `get_skill`, `set_skill_enabled`, `create_skill`, `update_skill`, `install_skill`, `check_skill_updates`, `update_skill_from_source`, `delete_skill`.

- [ ] **Step 1: Write failing local-skill tests.** Verify `caveman`, `hallmark`, `superpowers`, and `ponytail` seed once and disabled; built-ins cannot be deleted but can be enabled/disabled; custom names must match `^[A-Za-z0-9._-]{1,64}$`; CRUD persists across a new store instance; routing considers only enabled skills; loading disabled skills omits them.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpSkillServiceTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpSkillStore(string skillsDirectory)`.** Store `<skillsDirectory>/<name>/SKILL.md` plus `metadata.json`. Write both through temp-file-and-rename. Preserve enabled state when replacing content. Reject deletion when metadata says `BuiltIn=true`.

- [ ] **Step 4: Implement the built-in catalog and router.** Port the four built-in skill documents from LocalCodingMcp. Parse `name`, `description`, and optional `license` from YAML-like front matter with deterministic line parsing; route by case-insensitive token overlap across task, name, and description, then sort by score descending and name.

- [ ] **Step 5: Write failing remote tests using a custom `HttpMessageHandler`.** Verify plain HTTP rejection; URI user-info rejection; HTTPS-to-HTTP redirect rejection; redirect count above 3 rejection; payload above 1,048,576 bytes rejection; invalid skill document rejection; update check does not mutate; failed update preserves old content/metadata; successful install/update records source URL, resolved URL, SHA-256, license, and enabled state.

- [ ] **Step 6: Implement `McpRemoteSkillFetcher(HttpClient client, int maxBytes, int maxRedirects)`.** Production creates `HttpClientHandler { AllowAutoRedirect = false }`, `Timeout = TimeSpan.FromSeconds(15)`, max bytes `1_048_576`, redirects `3`. Require HTTPS and no `Uri.UserInfo` on every hop. Bound body reads before parsing.

- [ ] **Step 7: Implement `McpSkillService` and `McpSkillTools`.** Remote install/update is explicit only; checks are read-only; failed updates are atomic; skill text does not gain direct file/shell privilege. Preserve LocalCodingMcp JSON field names.

- [ ] **Step 8: Re-run skill tests.** Expected: PASS.

- [ ] **Step 9: Commit.**

```bash
git add src/Mcp/Services/McpBuiltInSkillCatalog.cs src/Mcp/Services/McpSkillStore.cs src/Mcp/Services/McpSkillRouter.cs src/Mcp/Services/McpRemoteSkillFetcher.cs src/Mcp/Services/McpSkillService.cs src/Mcp/Tools/McpSkillTools.cs tests/SourceGit.Tests/McpSkillServiceTests.cs
git commit -m "feat: add MCP skill tools"
```

### Task 6: Add redacted execution history

**Files:** Create `src/Mcp/Services/McpExecutionHistory.cs`, `src/Mcp/Tools/McpHistoryTools.cs`, `tests/SourceGit.Tests/McpExecutionHistoryTests.cs`.

Wire name: `get_execution_history`.

- [ ] **Step 1: Write failing tests.** Record arguments with keys containing `token`, `password`, `secret`, `authorization`, `api_key`, `apikey`, `base64_content`, and file/skill `content`; assert persisted JSON contains `[REDACTED]` but not original values. Assert individual argument strings are capped at 2,000 characters, query count clamps to `1..500`, filtering works, newest is first, and rotation prevents the active history file from growing beyond its configured cap.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpExecutionHistoryTests`.** Expected: FAIL.

- [ ] **Step 3: Implement `McpExecutionHistory(string filePath, int maxArgumentLength, long maxFileBytes)`.** Redact recursively before serialization, serialize one entry per JSONL line, coordinate append/read with a semaphore, and rotate the active file to `.1` before an append would exceed `maxFileBytes`, replacing any existing `.1`. Production values: `Path.Combine(Native.OS.DataDir, "mcp", "execution-history.jsonl")`, 2,000 chars, 10 MiB.

- [ ] **Step 4: Implement `McpHistoryTools`.** Return `{ count, history_file, entries }`; clamp `count` to `1..500`.

- [ ] **Step 5: Re-run focused tests.** Expected: PASS.

- [ ] **Step 6: Commit.**

```bash
git add src/Mcp/Services/McpExecutionHistory.cs src/Mcp/Tools/McpHistoryTools.cs tests/SourceGit.Tests/McpExecutionHistoryTests.cs
git commit -m "feat: add MCP execution history"
```

### Task 7: Register every new service/tool in the existing authenticated MCP host

**Files:** Modify `src/Mcp/SourceGitMcpHost.cs`; create `tests/SourceGit.Tests/McpToolRegistrationTests.cs`.

- [ ] **Step 1: Write a failing tool-discovery contract test.** Assert discovery includes existing `sourcegit_list_devspaces`, `sourcegit_list_terminals`, `sourcegit_terminal_status`, `sourcegit_terminal_tail`, `sourcegit_terminal_read`, plus all new wire names from Tasks 1, 3, 4, 5, and 6.

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpToolRegistrationTests`.** Expected: FAIL because the new tool classes are not registered.

- [ ] **Step 3: Register services with constructor-complete factories.** Use these production paths/options:

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
builder.Services.AddSingleton<McpSkillService>();
builder.Services.AddSingleton(_ => new McpExecutionHistory(historyPath, 2_000, 10L * 1024 * 1024));
```

Register the remote skill fetcher/service through an explicit `HttpClient` factory using the Task 5 limits rather than a parameterless singleton.

- [ ] **Step 4: Extend the existing MCP builder without replacing auth/transport.**

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

Add `WithRequestFilters` call-tool history recording like LocalCodingMcp: start a stopwatch, call `next`, record tool name, arguments, success/error, and duration through `McpExecutionHistory`. The store performs redaction before persistence. If history recording itself fails, log/swallow that failure so it cannot change the tool result.

- [ ] **Step 5: Re-run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~Mcp"`.** Expected: PASS, including current authorization/transport tests.

- [ ] **Step 6: Commit.**

```bash
git add src/Mcp/SourceGitMcpHost.cs tests/SourceGit.Tests/McpToolRegistrationTests.cs
git commit -m "feat: register coding tools in DevBoard MCP"
```

### Task 8: Add an end-to-end coding workflow test and README documentation

**Files:** Create `tests/SourceGit.Tests/McpCodingIntegrationTests.cs`; modify `README.md`.

- [ ] **Step 1: Write one integration test using a temporary Git repository.** Initialize Git, set repository-local identity, commit `README.md`, expose the root through `McpWorkspaceRegistry`, open it, list/read/search, write and patch a permitted file, call status/diff/log, run a harmless command and verify cwd, read history, reject `../outside`, and reject `.env`.

```text
git init
git config user.email mcp-tests@example.invalid
git config user.name MCP-Tests
git add README.md
git commit -m initial
```

- [ ] **Step 2: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpCodingIntegrationTests`.** Expected: PASS after Tasks 1-7.

- [ ] **Step 3: Add `## MCP server` to `README.md`.** Document the single DevBoard endpoint/token, exact-known-workspace restriction, explicit `workspace_id` flow, all tool categories, blocked sensitive files, and these examples:

```text
open_workspace(path: "D:/Development/example")
read_file(path: "src/Program.cs", workspace_id: "returned-id")
git_status(workspace_id: "returned-id")
run_command(command: "dotnet test", workspace_id: "returned-id")
```

State explicitly that arbitrary filesystem paths not represented by currently open DevBoard repository/worktree state are unavailable to MCP.

- [ ] **Step 4: Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj` and `dotnet build src/SourceGit.csproj`.** Expected: both exit 0.

- [ ] **Step 5: Commit.**

```bash
git add tests/SourceGit.Tests/McpCodingIntegrationTests.cs README.md
git commit -m "test: verify DevBoard MCP coding workflow"
```

### Task 9: Final compatibility/security verification

- [ ] **Step 1:** Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~Mcp"`. Expected: PASS.
- [ ] **Step 2:** Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj`. Expected: PASS.
- [ ] **Step 3:** Run `dotnet build src/SourceGit.csproj -c Debug` and `dotnet build src/SourceGit.csproj -c Release -p:DisableAOT=true`. Expected: both exit 0.
- [ ] **Step 4:** Run `git diff master...HEAD -- src/SourceGit.csproj .gitmodules` and `git grep -n "LocalCodingMcp" -- ':!docs/superpowers/*'`. Expected: no package/project/submodule/executable/runtime dependency on LocalCodingMcp.
- [ ] **Step 5:** Run `git diff --check master...HEAD` and `git status --short`. Expected: no whitespace errors and only intentional changes.
- [ ] **Step 6:** If verification exposes a defect, fix only that defect, rerun its focused test plus the complete test project, and commit with a narrow `fix:` message. Do not create an empty verification commit.

## Intentional Compatibility Difference

LocalCodingMcp accepts `open_workspace` for any path beneath configured allowed roots. DevBoard deliberately replaces that configuration model: `open_workspace` accepts only an exact canonical repository/worktree root currently represented by the running DevBoard launcher. File, Git, and shell tool argument names remain LocalCodingMcp-compatible, including explicit `workspace_id`.

`CodebaseMemoryTools.cs` is not ported in this feature because the approved design covers workspace, files, Git, shell, skills, and execution history; Codebase Memory remains a separate optional integration.
