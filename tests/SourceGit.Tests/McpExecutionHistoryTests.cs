using System.IO;
using System.Threading.Tasks;
using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpExecutionHistoryTests
{
    [Fact]
    public async Task History_redacts_sensitive_arguments()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-history-");
        try
        {
            var path = Path.Combine(root.FullName, "history.jsonl");
            var history = new McpExecutionHistory(path, 2000, 1024 * 1024);
            await history.RecordAsync("write_file", new { token = "abc", content = "secret text", path = "a.txt" }, true, 5);
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("abc", text);
            Assert.DoesNotContain("secret text", text);
            Assert.Contains("[REDACTED]", text);
        }
        finally { root.Delete(true); }
    }
}
