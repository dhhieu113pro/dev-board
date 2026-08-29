using System;
using System.ComponentModel;
using System.Text.Json;

using ModelContextProtocol.Server;

using SourceGit.Mcp.Services;

namespace SourceGit.Mcp.Tools
{
    [McpServerToolType]
    public sealed class McpWorkspaceTools
    {
        public McpWorkspaceTools(McpWorkspaceRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        [McpServerTool(Name = "open_workspace")]
        [Description("Opens a repository or worktree already known to the running DevBoard instance and returns its workspace id.")]
        public string OpenWorkspace(
            [Description("Exact DevBoard-known repository/worktree path.")] string workspace)
        {
            try
            {
                var opened = _registry.Open(workspace);
                return JsonSerializer.Serialize(new
                {
                    workspace_id = opened.Id,
                    root = opened.Root,
                    opened_at = DateTimeOffset.UtcNow,
                    message = "Pass workspace_id to subsequent file, Git, and shell calls.",
                });
            }
            catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "workspace_not_found",
                    message = ex.Message,
                });
            }
        }

        [McpServerTool(Name = "list_workspaces")]
        [Description("Lists repositories and worktrees currently known to DevBoard.")]
        public string ListWorkspaces()
        {
            return JsonSerializer.Serialize(new
            {
                workspaces = _registry.List(),
            });
        }

        [McpServerTool(Name = "get_allowed_roots")]
        [Description("Lists the exact repository/worktree roots that the running DevBoard instance allows MCP coding tools to access.")]
        public string GetAllowedRoots()
        {
            return JsonSerializer.Serialize(new
            {
                allowed_roots = _registry.GetAllowedRoots(),
            });
        }

        private readonly McpWorkspaceRegistry _registry;
    }
}
