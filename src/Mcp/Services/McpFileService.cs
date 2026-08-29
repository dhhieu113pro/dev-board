using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Mcp.Services
{
    public sealed class McpFileService
    {
        public McpFileService(McpWorkspaceRegistry workspaces, McpPathSandbox sandbox, McpSensitiveFileFilter sensitive)
        {
            _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
            _sandbox = sandbox ?? throw new ArgumentNullException(nameof(sandbox));
            _sensitive = sensitive ?? throw new ArgumentNullException(nameof(sensitive));
        }

        public object ListDirectory(string workspaceId, string path = ".")
        {
            var full = Resolve(workspaceId, path, false);
            var entries = new DirectoryInfo(full).EnumerateFileSystemInfos().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new { name = x.Name, path = Path.GetRelativePath(_workspaces.GetRoot(workspaceId), x.FullName), type = x is DirectoryInfo ? "directory" : "file" }).ToArray();
            return new { path, entries };
        }

        public object ReadFile(string workspaceId, string path, int? startLine = null, int? endLine = null)
        {
            var full = Resolve(workspaceId, path, true);
            EnsureSize(full, SourceGitMcpOptions.DefaultMaxFileReadBytes);
            var text = File.ReadAllText(full);
            if (startLine.HasValue || endLine.HasValue)
            {
                var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                var start = Math.Max(1, startLine ?? 1);
                var end = Math.Min(lines.Length, endLine ?? lines.Length);
                text = start > end ? string.Empty : string.Join("\n", lines.Skip(start - 1).Take(end - start + 1));
            }
            return new { path, content = text };
        }

        public object ReadBinaryFile(string workspaceId, string path)
        {
            var full = Resolve(workspaceId, path, true);
            EnsureSize(full, SourceGitMcpOptions.DefaultMaxFileReadBytes);
            var bytes = File.ReadAllBytes(full);
            return new { path, base64_content = Convert.ToBase64String(bytes), size = bytes.Length };
        }

        public object WriteFile(string workspaceId, string path, string content)
        {
            var full = Resolve(workspaceId, path, true);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content ?? string.Empty, new UTF8Encoding(false));
            return new { path, bytes = Encoding.UTF8.GetByteCount(content ?? string.Empty) };
        }

        public object WriteBinaryFile(string workspaceId, string path, string base64Content)
        {
            var full = Resolve(workspaceId, path, true);
            var payload = base64Content ?? string.Empty;
            var comma = payload.IndexOf(',');
            if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
                payload = payload[(comma + 1)..];
            byte[] bytes;
            try { bytes = Convert.FromBase64String(payload); }
            catch (FormatException ex) { throw new ArgumentException("Invalid base64 content.", nameof(base64Content), ex); }
            if (bytes.Length > SourceGitMcpOptions.DefaultMaxFileReadBytes)
                throw new InvalidOperationException("Binary content exceeds the maximum allowed size.");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
            return new { path, bytes = bytes.Length };
        }

        public object ApplyPatch(string workspaceId, string path, string patch)
        {
            var full = Resolve(workspaceId, path, true);
            EnsureSize(full, SourceGitMcpOptions.DefaultMaxFileReadBytes);
            var original = File.ReadAllText(full);
            var changed = McpPatchApplier.Apply(original, patch);
            File.WriteAllText(full, changed, new UTF8Encoding(false));
            return new { path, changed = !string.Equals(original, changed, StringComparison.Ordinal) };
        }

        public object SearchFiles(string workspaceId, string query, string path = ".", bool regex = false, int maxResults = SourceGitMcpOptions.DefaultMaxSearchResults)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Search query is required.", nameof(query));
            var root = Resolve(workspaceId, path, false);
            var workspaceRoot = _workspaces.GetRoot(workspaceId);
            var limit = Math.Clamp(maxResults, 1, SourceGitMcpOptions.DefaultMaxSearchResults);
            Regex expression = null;
            if (regex)
                expression = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var results = new List<object>();
            var enumeration = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            foreach (var file in Directory.EnumerateFiles(root, "*", enumeration))
            {
                if (results.Count >= limit)
                    break;
                if (_sensitive.IsBlocked(file) || IsBinary(file))
                    continue;

                var relativePath = Path.GetRelativePath(workspaceRoot, file);
                try
                {
                    _sandbox.Resolve(workspaceRoot, relativePath);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                var info = new FileInfo(file);
                if (info.Length > SourceGitMcpOptions.DefaultMaxSearchFileBytes)
                    continue;
                string text;
                try { text = File.ReadAllText(file); } catch { continue; }
                var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                for (var i = 0; i < lines.Length && results.Count < limit; i++)
                {
                    var matched = expression?.IsMatch(lines[i]) ?? lines[i].Contains(query, StringComparison.OrdinalIgnoreCase);
                    if (matched)
                        results.Add(new { path = relativePath, line = i + 1, text = lines[i] });
                }
            }
            return new { count = results.Count, results };
        }

        public object CreateDirectory(string workspaceId, string path)
        {
            var full = Resolve(workspaceId, path, true);
            Directory.CreateDirectory(full);
            return new { path };
        }

        public object MoveFile(string workspaceId, string sourcePath, string destinationPath)
        {
            var source = Resolve(workspaceId, sourcePath, true);
            var destination = Resolve(workspaceId, destinationPath, true);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(source))
                File.Move(source, destination, false);
            else if (Directory.Exists(source))
                Directory.Move(source, destination);
            else
                throw new FileNotFoundException("Source path does not exist.", sourcePath);
            return new { source_path = sourcePath, destination_path = destinationPath };
        }

        public object DeleteFile(string workspaceId, string path)
        {
            var full = Resolve(workspaceId, path, true);
            if (File.Exists(full))
                File.Delete(full);
            else if (Directory.Exists(full))
            {
                if (Directory.EnumerateFileSystemEntries(full).Any())
                    throw new IOException("Refusing to delete a non-empty directory.");
                Directory.Delete(full);
            }
            else
                throw new FileNotFoundException("Path does not exist.", path);
            return new { path, deleted = true };
        }

        private string Resolve(string workspaceId, string path, bool enforceSensitive)
        {
            var full = _sandbox.Resolve(_workspaces.GetRoot(workspaceId), path);
            if (enforceSensitive && _sensitive.IsBlocked(full))
                throw new UnauthorizedAccessException("Access to sensitive files is blocked.");
            return full;
        }

        private static void EnsureSize(string path, long max)
        {
            var info = new FileInfo(path);
            if (info.Length > max)
                throw new InvalidOperationException("File exceeds the maximum allowed size.");
        }

        private static bool IsBinary(string path)
        {
            var ext = Path.GetExtension(path);
            return _binaryExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static readonly string[] _binaryExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".pdf", ".zip", ".7z", ".gz", ".dll", ".exe", ".so", ".dylib", ".woff", ".woff2", ".ttf"];
        private readonly McpWorkspaceRegistry _workspaces;
        private readonly McpPathSandbox _sandbox;
        private readonly McpSensitiveFileFilter _sensitive;
    }
}
