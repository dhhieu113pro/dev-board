# DevBoard MCP Local Coding Tools Design

## Status

Approved for implementation planning on 2026-08-29.

## Problem

DevBoard already hosts an in-process MCP server for DevSpace terminal discovery and transcript access, but it cannot yet act as a complete local coding MCP for the repository/worktree currently being used in DevBoard.

The existing `local-coding-mcp` project already provides the desired coding surface: workspace selection, sandboxed file operations, code search, Git inspection, shell execution, reusable skills, and redacted execution history. Running a second MCP server would duplicate lifecycle, authentication, configuration, and workspace knowledge that DevBoard already owns.

## Goal

Integrate the useful `local-coding-mcp` capabilities directly into DevBoard's existing MCP server while preserving DevBoard's current terminal tools and security model.

The resulting DevBoard MCP should let an MCP client select a repository/worktree known to DevBoard and then safely inspect or modify files, search code, inspect Git state, run commands, manage reusable skills, and inspect redacted execution history.

## Non-goals

- Do not embed or launch the `local-coding-mcp` executable as a second server.
- Do not add `local-coding-mcp` as a git submodule or runtime dependency.
- Do not replace or rename the existing `sourcegit_*` terminal tools in this change.
- Do not expose arbitrary host filesystem paths merely because the MCP caller supplies them.
- Do not grant skills any additional filesystem or shell privilege beyond the MCP tools already available.
- Do not redesign the DevBoard MCP transport, authentication, or settings UI unless required to expose the new safety options.

## Chosen Architecture

Port the reusable behavior from `local-coding-mcp` into DevBoard-native services and thin MCP tool classes.

DevBoard remains the single MCP host. Existing host behavior stays authoritative for loopback binding, bearer authentication, request concurrency limiting, MCP transport, startup, and shutdown.

New coding tools live under `src/Mcp/Tools/` and depend on focused services under `src/Mcp/Services/` (or the nearest existing project convention discovered during implementation). The current `SourceGitMcpTools` remains focused on DevSpace terminal operations.

No code-level dependency on the `local-coding-mcp` repository is introduced. This avoids coupling DevBoard packaging to another executable while keeping the wire contracts intentionally compatible where practical.

## Workspace Model

DevBoard's own repositories, worktrees, and DevSpaces are the source of truth for allowed coding roots.

The MCP coding subsystem maintains a workspace registry that exposes only roots currently known to DevBoard. A root may be eligible when it represents an opened repository, an opened worktree, or a DevSpace path backed by a known repository/worktree.

The client must select a workspace before relative file, Git, or shell operations are allowed. Workspace identifiers must be stable for the lifetime of the DevBoard process and resolve to a canonical path.

The intended workspace tools are:

- `open_workspace`
- `list_workspaces`
- `get_allowed_roots`

`open_workspace` accepts a workspace identifier or a path that canonicalizes to one of DevBoard's known roots. It must reject arbitrary paths outside the registry.

Workspace state is MCP-session-safe: selecting a workspace for one caller must not silently redirect another caller's operations. If the current MCP SDK/tool registration model does not provide reliable per-client state, coding tools must require an explicit `workspace` argument instead of relying on mutable global selection. The implementation plan must resolve this from the SDK capabilities before coding the public contract.

## Tool Surface

### Files

Port behavior equivalent to:

- `list_directory`
- `read_file`
- `write_file`
- `read_binary_file`
- `write_binary_file`
- `apply_patch`
- `search_files`
- `create_directory`
- `move_file`
- `delete_file`

All paths are workspace-relative at the wire boundary unless an existing LocalCodingMcp-compatible argument explicitly needs otherwise. Responses should stay close to LocalCodingMcp's shape so prompts and clients can transfer with minimal adjustment.

### Git

Expose:

- `git_status`
- `git_diff`
- `git_log`

Git commands execute with the selected workspace root as their repository context. A workspace that is not a Git repository returns a structured error rather than falling back to another directory.

### Shell

Expose `run_command` with the selected workspace as the working-directory boundary.

Command execution must have a configured timeout and bounded captured output. The command cannot set a working directory outside the active workspace. Cancellation must terminate the child process tree where the platform allows it.

### Skills

Port the complete LocalCodingMcp skill surface:

- `route_skills`
- `load_skills`
- `load_enabled_skills`
- `list_skills`
- `get_skill`
- `set_skill_enabled`
- `create_skill`
- `update_skill`
- `install_skill`
- `check_skill_updates`
- `update_skill_from_source`
- `delete_skill`

Keep the built-in skills and behavior compatible with LocalCodingMcp: `caveman`, `hallmark`, `superpowers`, and `ponytail`, initially disabled unless DevBoard already has persisted state for them.

Skill storage belongs to DevBoard application data, not inside the currently selected repository, so changing workspaces does not change the user's skill library.

Remote skill operations remain explicit. HTTPS-only sources, bounded text downloads, redirect validation, content validation, SHA-256 provenance, and non-mutating update checks are required.

### Execution History

Expose `get_execution_history`.

History is persisted in DevBoard application data and records coding-tool calls with timestamps, tool identity, success/failure metadata, and bounded/redacted arguments. Passwords, tokens, secrets, authorization values, and sensitive file contents must not be persisted.

Existing DevSpace terminal transcript tools are not automatically added to coding-tool history in this change unless doing so requires no behavioral change and passes the same redaction guarantees.

## Service Boundaries

The integration should prefer small services with one responsibility:

- `McpWorkspaceRegistry`: maps DevBoard repository/worktree/DevSpace state to canonical allowed roots.
- `McpPathSandbox`: canonicalizes and validates workspace-relative paths, including symlink/reparse-point escape checks.
- `McpFileService`: file and directory operations plus search and patch application.
- `McpGitService`: bounded Git status/diff/log execution.
- `McpCommandService`: shell process lifecycle, timeout, cancellation, output bounding, and working-directory enforcement.
- `McpSkillService`: local skill persistence, routing/loading, built-ins, remote provenance, and update checks.
- `McpExecutionHistory`: append/read/redaction and retention handling.

Exact class names may follow DevBoard naming conventions, but these responsibilities must remain separable and independently testable.

MCP tool classes are adapters only: validate wire-level arguments, call services, and return structured results. Security logic must not be duplicated across individual tool methods.

## Security Model

The following controls are requirements, not optional hardening:

1. **Known-root restriction.** A coding operation must resolve under a repository/worktree root known to DevBoard.
2. **Canonical path validation.** Reject `..`, rooted path escapes, separator tricks, and paths that canonicalize outside the workspace.
3. **Symlink/reparse escape protection.** Existing filesystem links must not permit traversal outside the workspace root.
4. **Sensitive-file blocking.** Preserve LocalCodingMcp-style blocking for `.env`, private keys, credential files, `*.pem`, `*.pfx`, and equivalent high-risk patterns. The implementation plan should centralize the pattern list and test it explicitly.
5. **Bounded reads/writes/search.** Prevent accidentally loading unbounded files or returning unbounded search/output payloads through MCP.
6. **Command timeout and output bounds.** Shell execution cannot run indefinitely or return unlimited stdout/stderr.
7. **No privilege expansion from skills.** Skill text is instructions/data only.
8. **Remote skill safety.** HTTPS only; embedded credentials rejected; redirects remain HTTPS; size/content limits enforced; failed install/update leaves the existing skill unchanged.
9. **History redaction.** Secrets and sensitive content are removed before persistence.
10. **Existing MCP auth retained.** The current loopback and bearer-token checks remain in force for all new tools.

## MCP Registration

DevBoard's MCP server should register multiple tool classes rather than growing `SourceGitMcpTools` into a monolith.

Conceptually:

- existing terminal tools
- workspace tools
- file tools
- Git tools
- shell tools
- skill tools
- history tools

Registration stays inside the existing `SourceGitMcpHost`/bootstrap path so all tools share one endpoint, authentication layer, request limiter, and process lifetime.

The existing terminal wire names remain unchanged for backward compatibility. Coding/skill/history wire names should match LocalCodingMcp names where practical.

## Error Model

Expected caller errors return structured MCP/tool results rather than throwing through the host. At minimum distinguish:

- workspace not found/not selected
- path outside workspace
- sensitive path blocked
- file/directory not found
- invalid patch
- Git unavailable/not a repository
- command timeout/cancelled/non-zero exit
- output/read limit exceeded or truncated
- invalid skill name/content/source
- remote fetch/update failure

Unexpected internal exceptions should be logged through DevBoard's existing logging path without leaking secrets into the response.

## Configuration

Reuse existing DevBoard MCP settings where possible. Add only settings needed by the coding surface, with safe defaults, such as:

- command timeout
- maximum command output bytes
- maximum text/binary file read size
- search result limits
- skills directory under DevBoard app data
- remote skill fetch size/timeout/redirect limits
- execution-history path/retention limits

Allowed roots are deliberately not a free-form MCP configuration list; they derive from DevBoard workspace state.

## Compatibility and Migration

This is additive. Existing MCP users keep their current endpoint, token, terminal tools, and terminal-sharing setting.

LocalCodingMcp-compatible tool names and argument semantics should be preserved unless they conflict with DevBoard's workspace safety model. Any intentional incompatibility must be documented in the implementation plan and tests.

Because the code is ported rather than referenced, license attribution for copied/adapted LocalCodingMcp code remains the repository's own MIT-authored code. Existing third-party dependencies introduced by the port must still be recorded according to DevBoard's existing third-party license policy.

## Testing Strategy

Use test-driven implementation for security-sensitive behavior.

Unit tests must cover:

- known-root workspace discovery and rejection of unknown roots
- canonical `..`/absolute escape rejection
- symlink/reparse-point escape rejection on supported platforms
- sensitive-file patterns
- text/binary size limits
- file write/move/delete confinement
- patch success and invalid/escaping patches
- search confinement and result limits
- Git success/error cases
- command working-directory confinement, timeout, cancellation, non-zero exit, and output truncation
- skill CRUD, built-in restrictions, routing/loading, persistence, remote validation/provenance, failed-update atomicity
- execution-history redaction and retention

MCP registration/contract tests must verify every intended wire tool is discoverable and existing `sourcegit_*` tools remain present.

Integration tests should create a temporary Git repository registered as a DevBoard workspace and prove an end-to-end sequence:

1. discover/open workspace
2. list/read/search files
3. write/patch a permitted file
4. inspect Git status/diff/log
5. execute a harmless command in the workspace
6. read redacted execution history
7. verify a path outside the workspace and a sensitive file are rejected

## Rollout

Implement behind the existing MCP enablement setting. No separate feature toggle is required unless implementation discovers a material security or compatibility reason.

Documentation should update DevBoard's MCP section with the new coding-tool categories, security boundaries, and examples. It should clearly state that MCP coding access is limited to repositories/worktrees known to the running DevBoard instance.

## Acceptance Criteria

The feature is complete when:

- one DevBoard MCP endpoint exposes both the existing DevSpace terminal tools and the new coding tools;
- an MCP client can safely operate on a DevBoard-known repository/worktree without manually configuring arbitrary allowed roots;
- all LocalCodingMcp tool categories requested in this design are represented;
- filesystem and shell operations cannot escape the chosen DevBoard workspace;
- sensitive paths and secrets are blocked/redacted as designed;
- existing `sourcegit_*` tool contracts remain compatible;
- automated tests cover the new service boundaries and MCP registration;
- normal DevBoard build/test CI is green.
