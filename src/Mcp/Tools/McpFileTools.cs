using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SourceGit.Mcp.Services;

namespace SourceGit.Mcp.Tools
{
    [McpServerToolType]
    public sealed class McpFileTools
    {
        public McpFileTools(McpFileService files) => _files = files;

        [McpServerTool(Name = "list_directory")]
        public string ListDirectory([Description("Workspace id returned by open_workspace.")] string workspace_id, string path = ".") => JsonSerializer.Serialize(_files.ListDirectory(workspace_id, path));
        [McpServerTool(Name = "read_file")]
        public string ReadFile(string workspace_id, string path, int? start_line = null, int? end_line = null) => JsonSerializer.Serialize(_files.ReadFile(workspace_id, path, start_line, end_line));
        [McpServerTool(Name = "write_file")]
        public string WriteFile(string workspace_id, string path, string content) => JsonSerializer.Serialize(_files.WriteFile(workspace_id, path, content));
        [McpServerTool(Name = "write_binary_file")]
        public string WriteBinaryFile(string workspace_id, string path, string base64_content) => JsonSerializer.Serialize(_files.WriteBinaryFile(workspace_id, path, base64_content));
        [McpServerTool(Name = "read_binary_file")]
        public string ReadBinaryFile(string workspace_id, string path) => JsonSerializer.Serialize(_files.ReadBinaryFile(workspace_id, path));
        [McpServerTool(Name = "apply_patch")]
        public string ApplyPatch(string workspace_id, string path, string patch) => JsonSerializer.Serialize(_files.ApplyPatch(workspace_id, path, patch));
        [McpServerTool(Name = "search_files")]
        public string SearchFiles(string workspace_id, string query, string path = ".", bool regex = false, int max_results = SourceGitMcpOptions.DefaultMaxSearchResults) => JsonSerializer.Serialize(_files.SearchFiles(workspace_id, query, path, regex, max_results));
        [McpServerTool(Name = "create_directory")]
        public string CreateDirectory(string workspace_id, string path) => JsonSerializer.Serialize(_files.CreateDirectory(workspace_id, path));
        [McpServerTool(Name = "move_file")]
        public string MoveFile(string workspace_id, string source_path, string destination_path) => JsonSerializer.Serialize(_files.MoveFile(workspace_id, source_path, destination_path));
        [McpServerTool(Name = "delete_file")]
        public string DeleteFile(string workspace_id, string path) => JsonSerializer.Serialize(_files.DeleteFile(workspace_id, path));

        private readonly McpFileService _files;
    }
}
