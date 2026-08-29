using System.IO;
using System.Threading.Tasks;
using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpGitServiceTests
{
    [Fact]
    public async Task Status_reports_non_repository_error_without_throwing()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-git-");
        try
        {
            var registry = new McpWorkspaceRegistry(() => new[] { root.FullName });
            var workspace = registry.Open(root.FullName);
            var service = new McpGitService(registry, new McpCommandService(10, 64 * 1024));
            var result = await service.StatusAsync(workspace.Id);
            Assert.NotEqual(0, result.ExitCode);
        }
        finally { root.Delete(true); }
    }
}
