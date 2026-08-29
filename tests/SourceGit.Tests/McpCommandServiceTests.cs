using System;
using System.IO;
using System.Threading.Tasks;
using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpCommandServiceTests
{
    [Fact]
    public async Task Command_runs_in_selected_workspace()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-command-");
        try
        {
            var service = new McpCommandService(10, 64 * 1024);
            var command = OperatingSystem.IsWindows() ? "cd" : "pwd";
            var result = await service.RunAsync(command, root.FullName);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(root.FullName.Replace('\\', '/'), result.Stdout.Replace('\\', '/'));
        }
        finally { root.Delete(true); }
    }
}
