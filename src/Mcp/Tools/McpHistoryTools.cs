using System.Text.Json;
using ModelContextProtocol.Server;
using SourceGit.Mcp.Services;

namespace SourceGit.Mcp.Tools
{
    [McpServerToolType]
    public sealed class McpHistoryTools
    {
        public McpHistoryTools(McpExecutionHistory history) => _history = history;

        [McpServerTool(Name = "get_execution_history")]
        public string GetExecutionHistory(int count = 100, string tool = null, bool? success = null) => JsonSerializer.Serialize(_history.Query(count, tool, success));

        private readonly McpExecutionHistory _history;
    }
}
