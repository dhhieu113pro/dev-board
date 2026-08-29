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

    [Fact]
    public void Search_does_not_follow_directory_links_outside_workspace()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-search-root-");
        var outside = Directory.CreateTempSubdirectory("devboard-mcp-search-outside-");
        try
        {
            File.WriteAllText(Path.Combine(outside.FullName, "outside.txt"), "outside-secret-marker");
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root.FullName, "linked"), outside.FullName);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                return;
            }

            var registry = new McpWorkspaceRegistry(() => new[] { root.FullName });
            var workspace = registry.Open(root.FullName);
            var service = new McpFileService(registry, new McpPathSandbox(), new McpSensitiveFileFilter());

            var result = JsonSerializer.Serialize(service.SearchFiles(workspace.Id, "outside-secret-marker"));

            Assert.DoesNotContain("outside-secret-marker", result);
        }
        finally
        {
            root.Delete(true);
            outside.Delete(true);
        }
    }
}
