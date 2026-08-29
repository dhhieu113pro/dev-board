using System;
using System.IO;
using System.Text.Json;
using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpFileServiceTests
{
    [Fact]
    public void Read_write_search_patch_and_sensitive_guards_work()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-files-");
        try
        {
            var registry = new McpWorkspaceRegistry(() => new[] { root.FullName });
            var workspace = registry.Open(root.FullName);
            var service = new McpFileService(registry, new McpPathSandbox(), new McpSensitiveFileFilter());
            service.WriteFile(workspace.Id, "a.txt", "hello\nworld\n");
            Assert.Contains("hello", service.ReadFile(workspace.Id, "a.txt").ToString());
            Assert.Contains("hello", JsonSerializer.Serialize(service.SearchFiles(workspace.Id, "hello")));
            service.ApplyPatch(workspace.Id, "a.txt", "@@ -1,2 +1,2 @@\n hello\n-world\n+devboard\n");
            Assert.Contains("devboard", service.ReadFile(workspace.Id, "a.txt").ToString());
            Assert.Throws<UnauthorizedAccessException>(() => service.WriteFile(workspace.Id, ".env", "secret"));
            Assert.Throws<UnauthorizedAccessException>(() => service.ReadFile(workspace.Id, "../outside.txt"));
        }
        finally { root.Delete(true); }
    }
}
