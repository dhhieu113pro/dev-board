using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SourceGit.Mcp.Services;

namespace SourceGit.Mcp.Tools
{
    [McpServerToolType]
    public sealed class McpGitTools
    {
        public McpGitTools(McpGitService git) => _git = git;

        [McpServerTool(Name = "git_status")]
        public async Task<string> GitStatus(string workspace_id, CancellationToken cancellationToken = default) => JsonSerializer.Serialize(await _git.StatusAsync(workspace_id, cancellationToken).ConfigureAwait(false));
        [McpServerTool(Name = "git_diff")]
        public async Task<string> GitDiff(string workspace_id, bool staged = false, CancellationToken cancellationToken = default) => JsonSerializer.Serialize(await _git.DiffAsync(workspace_id, staged, cancellationToken).ConfigureAwait(false));
        [McpServerTool(Name = "git_log")]
        public async Task<string> GitLog(string workspace_id, int count = 20, CancellationToken cancellationToken = default) => JsonSerializer.Serialize(await _git.LogAsync(workspace_id, count, cancellationToken).ConfigureAwait(false));

        private readonly McpGitService _git;
    }
}
