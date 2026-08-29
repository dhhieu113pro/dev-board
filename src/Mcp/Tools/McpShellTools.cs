using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SourceGit.Mcp.Services;

namespace SourceGit.Mcp.Tools
{
    [McpServerToolType]
    public sealed class McpShellTools
    {
        public McpShellTools(McpWorkspaceRegistry workspaces, McpCommandService commands)
        {
            _workspaces = workspaces;
            _commands = commands;
        }

        [McpServerTool(Name = "run_command")]
        public async Task<string> RunCommand(string workspace_id, string command, CancellationToken cancellationToken = default)
        {
            var result = await _commands.RunAsync(command, _workspaces.GetRoot(workspace_id), cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { exit_code = result.ExitCode, stdout = result.Stdout, stderr = result.Stderr, duration_ms = result.DurationMs, timed_out = result.TimedOut, truncated = result.Truncated });
        }

        private readonly McpWorkspaceRegistry _workspaces;
        private readonly McpCommandService _commands;
    }
}
