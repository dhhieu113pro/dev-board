# DevBoard MCP Local Coding Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LocalCodingMcp-compatible workspace, file, Git, shell, skill, and execution-history tools to DevBoard's existing authenticated MCP endpoint without exposing arbitrary host paths.

**Architecture:** Keep `SourceGitMcpHost` as the single in-process MCP server and register focused tool classes backed by DevBoard-native services. DevBoard-known repository/worktree paths become the only eligible roots; `open_workspace` returns a stable `workspace_id`, and every file/Git/shell call requires that explicit id so there is no shared mutable active-workspace state between MCP clients.

**Tech Stack:** .NET 10, C#, `ModelContextProtocol.AspNetCore` 2.2.0, ASP.NET Core slim host, xUnit 2.9.3, existing DevBoard/SourceGit repository models and application-data paths.

**Spec:** `docs/superpowers/specs/2026-08-29-devboard-mcp-local-coding-tools-design.md`

## Global Constraints

- Keep one DevBoard MCP endpoint and retain the current loopback binding, bearer authentication, request limiter, stateful HTTP transport, and legacy SSE compatibility.
- Keep all existing `sourcegit_*` terminal tool names and behavior unchanged.
- Do not add `local-coding-mcp` as a git submodule, package, executable, or runtime dependency.
- Allowed roots come only from repositories/worktrees/DevSpaces known to the running DevBoard instance; do not add a free-form allowed-root setting.
- Require `workspace_id` explicitly on every file, Git, and shell operation. `open_workspace` validates/opens a known root but does not mutate a process-global current workspace.
- Preserve sensitive-file blocking, canonical traversal checks, symlink/reparse-point escape protection, command timeouts/output bounds, remote-skill HTTPS validation, bounded downloads, provenance hashes, and history redaction.
- Store skills and execution history under DevBoard's existing application-data directory (`Native.OS.DataDir`), not inside repositories.
- Use TDD for security-sensitive behavior and make a focused commit after each task.

---

## File Structure

Create focused implementation files rather than enlarging `SourceGitMcpTools.cs`:

- `src/Mcp/Services/McpWorkspaceRegistry.cs` — known-root discovery plus `workspace_id` lifecycle.
- `src/Mcp/Services/McpPathSandbox.cs` — canonical path and link/reparse confinement.
- `src/Mcp/Services/McpSensitiveFileFilter.cs` — centralized sensitive filename rules.
- `src/Mcp/Services/McpPatchApplier.cs` — unified-diff application.
- `src/Mcp/Services/McpFileService.cs` — bounded file/directory/search operations.
- `src/Mcp/Services/McpCommandService.cs` — process execution, timeout, cancellation, and output bounds.
- `src/Mcp/Services/McpGitService.cs` — status/diff/log over `McpCommandService`.
- `src/Mcp/Services/McpSkillStore.cs`, `McpSkillRouter.cs`, `McpRemoteSkillFetcher.cs`, `McpSkillService.cs`, `McpBuiltInSkillCatalog.cs` — local and remote skill behavior.
- `src/Mcp/Services/McpExecutionHistory.cs` — redacted JSONL history and retention.
- `src/Mcp/Tools/McpWorkspaceTools.cs`, `McpFileTools.cs`, `McpGitTools.cs`, `McpShellTools.cs`, `McpSkillTools.cs`, `McpHistoryTools.cs` — thin MCP adapters.
- `tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs`, `McpPathSandboxTests.cs`, `McpFileServiceTests.cs`, `McpCommandServiceTests.cs`, `McpGitServiceTests.cs`, `McpSkillServiceTests.cs`, `McpExecutionHistoryTests.cs`, `McpToolRegistrationTests.cs`, `McpCodingIntegrationTests.cs` — behavior and contract tests.

Modify:

- `src/Mcp/SourceGitMcpOptions.cs` — bounded coding-tool defaults.
- `src/Mcp/SourceGitMcpHost.cs` — DI, tool registration, and call-history filter.
- `src/Mcp/SourceGitMcpService.cs` only if options need to flow from settings; prefer constants/defaults first.
- `README.md` — MCP coding tool documentation.
- `THIRD-PARTY-LICENSES.md` only if implementation adds a new third-party package; expected implementation should add none.

### Task 1: Known DevBoard workspace registry

**Files:**
- Create: `src/Mcp/Services/McpWorkspaceRegistry.cs`
- Create: `src/Mcp/Tools/McpWorkspaceTools.cs`
- Create: `tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs`

**Interfaces:**
- Produces: `McpWorkspaceInfo { string Id, string RootPath, DateTimeOffset OpenedAt }`
- Produces: `McpWorkspaceRegistry.Open(string pathOrId)`, `Get(string workspaceId)`, `GetRoot(string workspaceId)`, `List()`, `GetAllowedRoots()`.
- Wire tools: `open_workspace`, `list_workspaces`, `get_allowed_roots`.

- [ ] **Step 1: Write failing tests for deterministic known-root opening and unknown-root rejection**

```csharp
[Fact]
public void Open_Rejects_Path_Not_Known_To_DevBoard()
{
    using var known = TempDirectory.Create();
    using var unknown = TempDirectory.Create();
    var registry = new McpWorkspaceRegistry(() => new[] { known.Path });

    Assert.Throws<UnauthorizedAccessException>(() => registry.Open(unknown.Path));
}

[Fact]
public void Open_Returns_Same_Id_For_Same_Canonical_Root()
{
    using var root = TempDirectory.Create();
    var registry = new McpWorkspaceRegistry(() => new[] { root.Path });

    var first = registry.Open(root.Path);
    var second = registry.Open(root.Path);

    Assert.Equal(first.Id, second.Id);
    Assert.Equal(Path.GetFullPath(root.Path), first.RootPath);
}
```

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpWorkspaceRegistryTests`

Expected: FAIL because `McpWorkspaceRegistry` does not exist.

- [ ] **Step 3: Implement registry with injected root provider and stable process-lifetime ids**

```csharp
public sealed record McpWorkspaceInfo(string Id, string RootPath, DateTimeOffset OpenedAt);

public sealed class McpWorkspaceRegistry
{
    public McpWorkspaceRegistry(Func<IReadOnlyCollection<string>> knownRootsProvider) { ... }
    public McpWorkspaceInfo Open(string pathOrId) { ... }
    public McpWorkspaceInfo Get(string workspaceId) { ... }
    public string GetRoot(string workspaceId) => Get(workspaceId).RootPath;
    public IReadOnlyCollection<McpWorkspaceInfo> List() { ... }
    public IReadOnlyCollection<string> GetAllowedRoots() { ... }
}
```

Canonicalize with `Path.GetFullPath`, resolve existing links before comparison, deduplicate with OS-appropriate path comparison, and derive a stable id from the canonical path (for example SHA-256 first 12 lowercase hex chars) instead of `Guid.NewGuid()`.

- [ ] **Step 4: Add thin MCP workspace adapters**

```csharp
[McpServerToolType]
public sealed class McpWorkspaceTools
{
    [McpServerTool(Name = "open_workspace")]
    public string OpenWorkspace(string path) { ... }

    [McpServerTool(Name = "list_workspaces")]
    public string ListWorkspaces() { ... }

    [McpServerTool(Name = "get_allowed_roots")]
    public string GetAllowedRoots() { ... }
}
```

`open_workspace` returns `{ workspace_id, root, opened_at, message }`; it validates only DevBoard-known roots and does not set shared current state.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpWorkspaceRegistryTests`

Expected: PASS.

```bash
git add src/Mcp/Services/McpWorkspaceRegistry.cs src/Mcp/Tools/McpWorkspaceTools.cs tests/SourceGit.Tests/McpWorkspaceRegistryTests.cs
git commit -m "feat: add MCP workspace registry"
```

### Task 2: Path sandbox and sensitive-file policy

**Files:**
- Create: `src/Mcp/Services/McpPathSandbox.cs`
- Create: `src/Mcp/Services/McpSensitiveFileFilter.cs`
- Create: `tests/SourceGit.Tests/McpPathSandboxTests.cs`

**Interfaces:**
- Consumes: `McpWorkspaceRegistry.GetRoot(string)`.
- Produces: `McpPathSandbox.Resolve(string workspaceRoot, string relativePath)`.
- Produces: `McpSensitiveFileFilter.EnsureNotBlocked(string fullPath)`.

- [ ] **Step 1: Add failing traversal, rooted-path, symlink, and sensitive-file tests**

```csharp
[Theory]
[InlineData("../outside.txt")]
[InlineData("../../outside.txt")]
public void Resolve_Rejects_Traversal(string path) { ... }

[Fact]
public void Resolve_Rejects_Absolute_Path_Outside_Workspace() { ... }

[Fact]
public void Resolve_Rejects_Link_That_Escapes_Workspace() { ... }

[Theory]
[InlineData(".env")]
[InlineData("id_rsa")]
[InlineData("server.pem")]
[InlineData("credentials.json")]
[InlineData("appsettings.Production.json")]
public void Sensitive_Filter_Blocks_Defaults(string fileName) { ... }
```

Use `Directory.CreateSymbolicLink` where supported; skip only when the platform/filesystem reports that link creation is unavailable.

- [ ] **Step 2: Verify the tests fail**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~McpPathSandboxTests"`

Expected: FAIL because sandbox/filter classes are missing.

- [ ] **Step 3: Port and tighten LocalCodingMcp confinement behavior**

```csharp
public string Resolve(string workspaceRoot, string relativePath)
{
    if (Path.IsPathRooted(relativePath))
        throw new UnauthorizedAccessException("MCP file paths must be workspace-relative.");

    var root = ResolveSymbolicLinks(Path.GetFullPath(workspaceRoot));
    var candidate = ResolveSymbolicLinks(Path.GetFullPath(Path.Combine(root, relativePath)));
    if (!IsInside(candidate, root))
        throw new UnauthorizedAccessException("Path is outside the workspace.");
    return candidate;
}
```

Keep the LocalCodingMcp blocked list: `.env`, `.env.local`, `.env.production`, `.env.development`, `id_rsa*`, `id_ed25519*`, `id_ecdsa*`, `*.pem`, `*.pfx`, `*.p12`, `*.key`, `credentials.json`, `secrets.json`, `appsettings.Production.json`, `.npmrc`, `.netrc`, `authorized_keys`, `known_hosts`.

- [ ] **Step 4: Run tests and commit**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~McpPathSandboxTests"`

Expected: PASS.

```bash
git add src/Mcp/Services/McpPathSandbox.cs src/Mcp/Services/McpSensitiveFileFilter.cs tests/SourceGit.Tests/McpPathSandboxTests.cs
git commit -m "feat: sandbox MCP workspace paths"
```

### Task 3: File, directory, search, and patch tools

**Files:**
- Create: `src/Mcp/Services/McpPatchApplier.cs`
- Create: `src/Mcp/Services/McpFileService.cs`
- Create: `src/Mcp/Tools/McpFileTools.cs`
- Create: `tests/SourceGit.Tests/McpFileServiceTests.cs`
- Modify: `src/Mcp/SourceGitMcpOptions.cs`

**Interfaces:**
- Wire tools: `list_directory`, `read_file`, `write_file`, `read_binary_file`, `write_binary_file`, `apply_patch`, `search_files`, `create_directory`, `move_file`, `delete_file`.
- Every tool accepts `workspace_id`; all path arguments are relative.

- [ ] **Step 1: Add failing tests for read/write/search/patch limits and confinement**

```csharp
[Fact] public void ReadFile_Blocks_Sensitive_File() { ... }
[Fact] public void ReadFile_Rejects_File_Over_MaxBytes() { ... }
[Fact] public void WriteBinary_Rejects_Decoded_Content_Over_MaxBytes() { ... }
[Fact] public void SearchFiles_Skips_Binary_And_Sensitive_Files_And_Honors_Limit() { ... }
[Fact] public void ApplyPatch_Changes_Expected_Hunk() { ... }
[Fact] public void ApplyPatch_Does_Not_Write_When_Patch_Is_Invalid() { ... }
[Fact] public void MoveFile_Cannot_Escape_Workspace() { ... }
```

- [ ] **Step 2: Verify failure**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpFileServiceTests`

Expected: FAIL because service/tool classes are missing.

- [ ] **Step 3: Add bounded coding defaults**

```csharp
public const int DefaultCommandTimeoutSeconds = 30;
public const int DefaultMaxCommandOutputBytes = 1_048_576;
public const int DefaultMaxFileReadBytes = 10 * 1024 * 1024;
public const int DefaultMaxSearchResults = 50;
public const int DefaultMaxSearchFileBytes = 2 * 1024 * 1024;
```

- [ ] **Step 4: Implement `McpFileService` and thin adapters**

`McpFileService` owns file I/O, size checks, binary detection, regex compilation, result bounding, and atomic patch writes. Tools only resolve `workspace_id`, delegate, and serialize response shapes compatible with LocalCodingMcp.

```csharp
public string ReadFile(string workspaceId, string path, int? startLine, int? endLine) { ... }
public string WriteFile(string workspaceId, string path, string content) { ... }
public string ReadBinaryFile(string workspaceId, string path) { ... }
public string WriteBinaryFile(string workspaceId, string path, string base64Content) { ... }
public IReadOnlyList<McpSearchHit> SearchFiles(string workspaceId, string query, string path, int? maxResults) { ... }
```

- [ ] **Step 5: Run focused tests and commit**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpFileServiceTests`

Expected: PASS.

```bash
git add src/Mcp/SourceGitMcpOptions.cs src/Mcp/Services/McpPatchApplier.cs src/Mcp/Services/McpFileService.cs src/Mcp/Tools/McpFileTools.cs tests/SourceGit.Tests/McpFileServiceTests.cs
git commit -m "feat: add MCP file tools"
```

### Task 4: Shell and Git execution

**Files:**
- Create: `src/Mcp/Services/McpCommandService.cs`
- Create: `src/Mcp/Services/McpGitService.cs`
- Create: `src/Mcp/Tools/McpShellTools.cs`
- Create: `src/Mcp/Tools/McpGitTools.cs`
- Create: `tests/SourceGit.Tests/McpCommandServiceTests.cs`
- Create: `tests/SourceGit.Tests/McpGitServiceTests.cs`

**Interfaces:**
- Produces: `Task<McpCommandResult> RunAsync(string command, string workspaceRoot, CancellationToken)`.
- Wire tools: `run_command`, `git_status`, `git_diff`, `git_log`.

- [ ] **Step 1: Write failing process tests**

```csharp
[Fact] public async Task RunAsync_Uses_Workspace_As_Cwd() { ... }
[Fact] public async Task RunAsync_Truncates_Output_At_Configured_Limit() { ... }
[Fact] public async Task RunAsync_Times_Out_And_Kills_Process_Tree() { ... }
[Fact] public async Task RunAsync_Reports_NonZero_Exit_Code() { ... }
```

Use platform-neutral commands selected by `OperatingSystem.IsWindows()` (`cmd /d /s /c ...`) versus `/bin/sh -lc ...`.

- [ ] **Step 2: Verify command tests fail, implement minimal command service, and rerun**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpCommandServiceTests`

`McpCommandResult` must contain `ExitCode`, `Stdout`, `Stderr`, `DurationMs`, `TimedOut`, `Truncated`.

- [ ] **Step 3: Write failing Git tests using a temporary repository**

```csharp
[Fact] public async Task GitStatus_Returns_Porcelain_Branch_And_Changes() { ... }
[Fact] public async Task GitDiff_Returns_Staged_When_Requested() { ... }
[Fact] public async Task GitLog_Clamps_Count_To_One_Through_Fifty() { ... }
[Fact] public async Task GitStatus_Returns_Structured_Error_For_NonRepo() { ... }
```

- [ ] **Step 4: Implement Git service/tools with argument-list-safe invocation where possible**

Use DevBoard's existing Git executable resolution if exposed by current command infrastructure; otherwise invoke `git` through `McpCommandService` with constant command templates only. Never splice caller-controlled arbitrary Git switches into the command.

- [ ] **Step 5: Run both suites and commit**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~McpCommandServiceTests|FullyQualifiedName~McpGitServiceTests"`

Expected: PASS.

```bash
git add src/Mcp/Services/McpCommandService.cs src/Mcp/Services/McpGitService.cs src/Mcp/Tools/McpShellTools.cs src/Mcp/Tools/McpGitTools.cs tests/SourceGit.Tests/McpCommandServiceTests.cs tests/SourceGit.Tests/McpGitServiceTests.cs
git commit -m "feat: add MCP shell and git tools"
```

### Task 5: Skill store, built-ins, routing, and remote updates

**Files:**
- Create: `src/Mcp/Services/McpBuiltInSkillCatalog.cs`
- Create: `src/Mcp/Services/McpSkillStore.cs`
- Create: `src/Mcp/Services/McpSkillRouter.cs`
- Create: `src/Mcp/Services/McpRemoteSkillFetcher.cs`
- Create: `src/Mcp/Services/McpSkillService.cs`
- Create: `src/Mcp/Tools/McpSkillTools.cs`
- Create: `tests/SourceGit.Tests/McpSkillServiceTests.cs`

**Interfaces:**
- Wire tools: `route_skills`, `load_skills`, `load_enabled_skills`, `list_skills`, `get_skill`, `set_skill_enabled`, `create_skill`, `update_skill`, `install_skill`, `check_skill_updates`, `update_skill_from_source`, `delete_skill`.
- Built-ins: `caveman`, `hallmark`, `superpowers`, `ponytail`; disabled on first seed.

- [ ] **Step 1: Add failing local-skill tests**

```csharp
[Fact] public void BuiltIns_Are_Seeded_Disabled() { ... }
[Fact] public void BuiltIn_Cannot_Be_Deleted() { ... }
[Fact] public void User_Can_Enable_And_Disable_BuiltIn() { ... }
[Fact] public void Create_Update_Get_Delete_Custom_Skill_Persists() { ... }
[Fact] public void RouteSkills_Only_Ranks_Enabled_Skills() { ... }
```

- [ ] **Step 2: Implement local store/router and run tests**

Store each skill under `<Native.OS.DataDir>/mcp/skills/<name>/SKILL.md` plus a small metadata/state file. Validate names with `^[A-Za-z0-9._-]{1,64}$` and validate front matter before committing writes.

- [ ] **Step 3: Add failing remote-skill tests with a fake HTTP handler**

```csharp
[Fact] public async Task Install_Rejects_Http_Source() { ... }
[Fact] public async Task Install_Rejects_Url_With_UserInfo() { ... }
[Fact] public async Task Install_Rejects_Https_To_Http_Redirect() { ... }
[Fact] public async Task Install_Rejects_Response_Over_MaxBytes() { ... }
[Fact] public async Task CheckUpdates_Does_Not_Mutate_Installed_Content() { ... }
[Fact] public async Task Failed_Update_Leaves_Existing_Skill_Intact() { ... }
[Fact] public async Task Successful_Update_Records_Sha256_And_Resolved_Source() { ... }
```

- [ ] **Step 4: Implement remote fetch/update atomically**

Use `HttpClientHandler { AllowAutoRedirect = false }`, max 3 redirects, HTTPS on every hop, no URL user-info, 1 MiB default max response, 15-second default timeout, SHA-256 provenance, and temporary-file-plus-rename for updates.

- [ ] **Step 5: Run skill tests and commit**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpSkillServiceTests`

Expected: PASS.

```bash
git add src/Mcp/Services/McpBuiltInSkillCatalog.cs src/Mcp/Services/McpSkillStore.cs src/Mcp/Services/McpSkillRouter.cs src/Mcp/Services/McpRemoteSkillFetcher.cs src/Mcp/Services/McpSkillService.cs src/Mcp/Tools/McpSkillTools.cs tests/SourceGit.Tests/McpSkillServiceTests.cs
git commit -m "feat: add MCP skill tools"
```

### Task 6: Redacted execution history

**Files:**
- Create: `src/Mcp/Services/McpExecutionHistory.cs`
- Create: `src/Mcp/Tools/McpHistoryTools.cs`
- Create: `tests/SourceGit.Tests/McpExecutionHistoryTests.cs`

**Interfaces:**
- Produces: `RecordAsync(string tool, JsonElement? arguments, bool success, long durationMs, string? error, CancellationToken)` and `GetRecentAsync(int count, string? tool, bool? success, CancellationToken)`.
- Wire tool: `get_execution_history`.

- [ ] **Step 1: Write failing redaction/retention tests**

```csharp
[Fact] public async Task RecordAsync_Redacts_Token_Password_Authorization_And_Content() { ... }
[Fact] public async Task RecordAsync_Truncates_Large_Argument_Values() { ... }
[Fact] public async Task Store_Rotates_Or_Trims_When_Max_File_Size_Is_Reached() { ... }
[Fact] public async Task GetRecent_Filters_By_Tool_And_Success_Newest_First() { ... }
```

Redact keys case-insensitively when they contain `token`, `password`, `secret`, `authorization`, `api_key`, `apikey`, `base64_content`, or raw file `content` for write/update calls.

- [ ] **Step 2: Verify failure, implement JSONL store, and rerun**

History path: `<Native.OS.DataDir>/mcp/execution-history.jsonl`; default max argument text 2,000 chars and max file size 10 MiB.

- [ ] **Step 3: Add the history MCP adapter and commit**

```csharp
[McpServerTool(Name = "get_execution_history")]
public Task<string> GetExecutionHistory(int count = 50, string tool = null, bool? success = null, CancellationToken cancellationToken = default) { ... }
```

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpExecutionHistoryTests`

Expected: PASS.

```bash
git add src/Mcp/Services/McpExecutionHistory.cs src/Mcp/Tools/McpHistoryTools.cs tests/SourceGit.Tests/McpExecutionHistoryTests.cs
git commit -m "feat: add MCP execution history"
```

### Task 7: Register all coding tools in the existing DevBoard MCP host

**Files:**
- Modify: `src/Mcp/SourceGitMcpHost.cs`
- Create: `tests/SourceGit.Tests/McpToolRegistrationTests.cs`

**Interfaces:**
- Consumes all services/tools from Tasks 1-6.
- Existing `SourceGitMcpTools` remains registered unchanged.

- [ ] **Step 1: Add failing registration contract test**

Assert discovery includes exactly the existing `sourcegit_list_devspaces`, `sourcegit_list_terminals`, `sourcegit_terminal_status`, `sourcegit_terminal_tail`, `sourcegit_terminal_read` plus every new tool name in the spec.

- [ ] **Step 2: Verify failure**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpToolRegistrationTests`

Expected: FAIL because new tool classes are not registered.

- [ ] **Step 3: Register services and tools in `SourceGitMcpHost.StartAsync`**

```csharp
builder.Services.AddSingleton(_registry);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<McpWorkspaceRegistry>(...);
builder.Services.AddSingleton<McpPathSandbox>();
builder.Services.AddSingleton<McpSensitiveFileFilter>();
builder.Services.AddSingleton<McpFileService>();
builder.Services.AddSingleton<McpCommandService>();
builder.Services.AddSingleton<McpGitService>();
builder.Services.AddSingleton<McpSkillStore>();
builder.Services.AddSingleton<McpSkillService>();
builder.Services.AddSingleton<McpExecutionHistory>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(ConfigureTransport)
    .WithTools<SourceGitMcpTools>()
    .WithTools<McpWorkspaceTools>()
    .WithTools<McpFileTools>()
    .WithTools<McpGitTools>()
    .WithTools<McpShellTools>()
    .WithTools<McpSkillTools>()
    .WithTools<McpHistoryTools>()
    .WithRequestFilters(...);
```

The call filter records success/failure and elapsed time in `McpExecutionHistory` and must never record unredacted arguments.

- [ ] **Step 4: Re-run MCP auth/transport tests plus registration tests**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~Mcp"`

Expected: PASS with existing transport/auth tests still green and all tools discoverable.

- [ ] **Step 5: Commit**

```bash
git add src/Mcp/SourceGitMcpHost.cs tests/SourceGit.Tests/McpToolRegistrationTests.cs
git commit -m "feat: register coding tools in DevBoard MCP"
```

### Task 8: End-to-end coding workflow and documentation

**Files:**
- Create: `tests/SourceGit.Tests/McpCodingIntegrationTests.cs`
- Modify: `README.md`

**Interfaces:**
- Validates the complete public contract through service/tool boundaries.

- [ ] **Step 1: Add an end-to-end temporary-repository test**

The test must:

```text
1. create and git-init a temporary repository
2. expose that exact path through the known-root provider
3. open_workspace and capture workspace_id
4. list/read/search files
5. write and apply a patch to a permitted file
6. call git_status, git_diff, and git_log
7. run a harmless command and verify cwd is the workspace
8. verify ../ escape and .env access are rejected
9. fetch execution history and verify sensitive arguments are absent
```

- [ ] **Step 2: Run the integration test and fix only contract/integration defects**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter FullyQualifiedName~McpCodingIntegrationTests`

Expected: PASS.

- [ ] **Step 3: Document DevBoard MCP coding tools**

Add a `## MCP server` section to `README.md` covering the single endpoint, bearer token, known-workspace restriction, explicit `workspace_id` flow, categories/tool names, sensitive-file policy, and a short example:

```text
open_workspace(path) -> workspace_id
read_file(path: "src/Program.cs", workspace_id: "...")
git_status(workspace_id: "...")
run_command(command: "dotnet test", workspace_id: "...")
```

State clearly that MCP cannot access arbitrary filesystem paths that are not currently known to DevBoard.

- [ ] **Step 4: Run the full test project and build**

Run:

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj
dotnet build src/SourceGit.csproj
```

Expected: both commands exit 0.

- [ ] **Step 5: Commit**

```bash
git add tests/SourceGit.Tests/McpCodingIntegrationTests.cs README.md
git commit -m "test: verify DevBoard MCP coding workflow"
```

### Task 9: Final compatibility and security verification

**Files:**
- Modify only files required by failures found in this verification task.

- [ ] **Step 1: Run all MCP-focused tests**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "FullyQualifiedName~Mcp"`

Expected: PASS.

- [ ] **Step 2: Run all SourceGit.Tests**

Run: `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj`

Expected: PASS.

- [ ] **Step 3: Build Debug and Release without changing current package versions**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug
dotnet build src/SourceGit.csproj -c Release -p:DisableAOT=true
```

Expected: PASS.

- [ ] **Step 4: Confirm no LocalCodingMcp runtime dependency was introduced**

Run:

```bash
git diff master...HEAD -- src/SourceGit.csproj .gitmodules
git grep -n "LocalCodingMcp" -- ':!docs/superpowers/*'
```

Expected: no project/submodule/package dependency on `local-coding-mcp`; occurrences are limited to compatibility documentation/comments where useful.

- [ ] **Step 5: Review changed files and commit any verification fixes**

Run: `git status --short && git diff --check master...HEAD`

Expected: clean whitespace check. If verification required fixes, commit them with a narrow message such as `fix: harden MCP coding tool integration`; otherwise do not create an empty commit.

## Intentional Compatibility Difference from LocalCodingMcp

LocalCodingMcp already requires `workspace_id` on its file/Git/shell tools, but its `open_workspace` accepts any path under configured `AllowedRoots`. DevBoard changes that root model: `open_workspace` accepts only a canonical repository/worktree/DevSpace root already known to the running DevBoard instance. This is the deliberate security boundary and should be asserted in tests and documentation.

Codebase Memory tools from LocalCodingMcp are not part of this feature because the approved design enumerates workspace, files, Git, shell, skills, and execution history. The separate `CodebaseMemoryTools.cs` proxy is therefore not ported by this plan.
