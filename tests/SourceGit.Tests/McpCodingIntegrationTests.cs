using System;
using System.IO;
using System.Threading.Tasks;
using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpCodingIntegrationTests
{
    [Fact]
    public async Task Coding_services_work_together_inside_an_open_workspace()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-integration-");
        try
        {
            var commands = new McpCommandService(15, 256 * 1024);
            var init = await commands.RunAsync("git init", root.FullName);
            if (init.ExitCode != 0) return;
            await commands.RunAsync("git config user.email mcp-tests@example.invalid", root.FullName);
            await commands.RunAsync("git config user.name MCP-Tests", root.FullName);
            File.WriteAllText(Path.Combine(root.FullName, "README.md"), "# MCP\n");
            await commands.RunAsync("git add README.md && git commit -m initial", root.FullName);

            var registry = new McpWorkspaceRegistry(() => new[] { root.FullName });
            var workspace = registry.Open(root.FullName);
            var files = new McpFileService(registry, new McpPathSandbox(), new McpSensitiveFileFilter());
            var git = new McpGitService(registry, commands);

            Assert.Contains("MCP", files.ReadFile(workspace.Id, "README.md").ToString());
            files.WriteFile(workspace.Id, "src.txt", "one\ntwo\n");
            Assert.Contains("one", files.SearchFiles(workspace.Id, "one").ToString());
            files.ApplyPatch(workspace.Id, "src.txt", "@@ -1,2 +1,2 @@\n-one\n+ONE\n two\n");
            Assert.Contains("ONE", files.ReadFile(workspace.Id, "src.txt").ToString());
            Assert.Equal(0, (await git.StatusAsync(workspace.Id)).ExitCode);
            Assert.Equal(0, (await git.LogAsync(workspace.Id)).ExitCode);
            var cwd = await commands.RunAsync(OperatingSystem.IsWindows() ? "cd" : "pwd", registry.GetRoot(workspace.Id));
            Assert.Contains(root.FullName.Replace('\\', '/'), cwd.Stdout.Replace('\\', '/'));
            Assert.Throws<UnauthorizedAccessException>(() => files.ReadFile(workspace.Id, "../outside"));
            Assert.Throws<UnauthorizedAccessException>(() => files.WriteFile(workspace.Id, ".env", "secret"));
        }
        finally { root.Delete(true); }
    }
}
