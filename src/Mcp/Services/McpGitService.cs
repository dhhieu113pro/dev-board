using System;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Mcp.Services
{
    public sealed class McpGitService
    {
        public McpGitService(McpWorkspaceRegistry workspaces, McpCommandService commands)
        {
            _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public Task<McpCommandResult> StatusAsync(string workspaceId, CancellationToken ct = default) => Run(workspaceId, "git status --porcelain=v1 -b", ct);
        public Task<McpCommandResult> DiffAsync(string workspaceId, bool staged = false, CancellationToken ct = default) => Run(workspaceId, staged ? "git diff --cached" : "git diff", ct);
        public Task<McpCommandResult> LogAsync(string workspaceId, int count = 20, CancellationToken ct = default) => Run(workspaceId, $"git log -n {Math.Clamp(count, 1, 50)} --oneline", ct);

        private Task<McpCommandResult> Run(string workspaceId, string command, CancellationToken ct) => _commands.RunAsync(command, _workspaces.GetRoot(workspaceId), ct);

        private readonly McpWorkspaceRegistry _workspaces;
        private readonly McpCommandService _commands;
    }
}
