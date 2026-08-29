using System;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using SourceGit.Mcp;
using SourceGit.Mcp.Tools;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpToolRegistrationTests
{
    [Fact]
    public void Tool_surface_contains_existing_and_local_coding_names()
    {
        var types = new[] { typeof(SourceGitMcpTools), typeof(McpWorkspaceTools), typeof(McpFileTools), typeof(McpGitTools), typeof(McpShellTools), typeof(McpSkillTools), typeof(McpHistoryTools) };
        var names = types.SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute != null)
            .Select(attribute => attribute.Name)
            .ToHashSet(StringComparer.Ordinal);

        var expected = new[]
        {
            "sourcegit_list_devspaces", "sourcegit_list_terminals", "sourcegit_terminal_status", "sourcegit_terminal_tail", "sourcegit_terminal_read",
            "open_workspace", "list_workspaces", "get_allowed_roots",
            "list_directory", "read_file", "write_file", "write_binary_file", "read_binary_file", "apply_patch", "search_files", "create_directory", "move_file", "delete_file",
            "git_status", "git_diff", "git_log", "run_command",
            "route_skills", "load_skills", "load_enabled_skills", "list_skills", "get_skill", "set_skill_enabled", "create_skill", "update_skill", "install_skill", "check_skill_updates", "update_skill_from_source", "delete_skill",
            "get_execution_history"
        };
        foreach (var name in expected) Assert.Contains(name, names);
    }
}
