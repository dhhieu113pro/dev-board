using System;
using System.IO;
using System.Text;
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

    [Fact]
    public async Task Command_output_is_bounded_while_process_continues_to_completion()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-command-output-");
        try
        {
            const int maxBytes = 1024;
            var service = new McpCommandService(10, maxBytes);
            var command = OperatingSystem.IsWindows()
                ? "for /L %i in (1,1,5000) do @echo 1234567890"
                : "yes 1234567890 | head -n 5000";

            var result = await service.RunAsync(command, root.FullName);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Truncated);
            Assert.True(Encoding.UTF8.GetByteCount(result.Stdout) <= maxBytes);
        }
        finally { root.Delete(true); }
    }
}
