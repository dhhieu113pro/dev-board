using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SourceGit.Mcp.Services
{
    public sealed record McpWorkspace(string Id, string Root);

    public sealed class McpWorkspaceRegistry
    {
        public McpWorkspaceRegistry(Func<IReadOnlyCollection<string>> knownRootsProvider)
        {
            _knownRootsProvider = knownRootsProvider ?? throw new ArgumentNullException(nameof(knownRootsProvider));
        }

        public McpWorkspace Open(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("Workspace root is required.", nameof(root));

            var requested = Canonicalize(root);
            foreach (var workspace in Snapshot())
            {
                if (_pathComparer.Equals(workspace.Root, requested))
                    return workspace;
            }

            throw new UnauthorizedAccessException("The requested workspace is not open in DevBoard.");
        }

        public McpWorkspace Get(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                throw new ArgumentException("Workspace id is required.", nameof(workspaceId));

            foreach (var workspace in Snapshot())
            {
                if (string.Equals(workspace.Id, workspaceId, StringComparison.Ordinal))
                    return workspace;
            }

            throw new KeyNotFoundException($"Workspace '{workspaceId}' is not available.");
        }

        public string GetRoot(string workspaceId)
        {
            return Get(workspaceId).Root;
        }

        public IReadOnlyList<McpWorkspace> List()
        {
            return Snapshot();
        }

        public IReadOnlyList<string> GetAllowedRoots()
        {
            return Snapshot().Select(x => x.Root).ToArray();
        }

        private IReadOnlyList<McpWorkspace> Snapshot()
        {
            var roots = _knownRootsProvider() ?? Array.Empty<string>();
            var unique = new HashSet<string>(_pathComparer);
            var workspaces = new List<McpWorkspace>(roots.Count);

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                var canonical = Canonicalize(root);
                if (!unique.Add(canonical))
                    continue;

                workspaces.Add(new McpWorkspace(CreateId(canonical), canonical));
            }

            return workspaces;
        }

        private static string Canonicalize(string path)
        {
            var fullPath = Path.GetFullPath(path);

            try
            {
                if (Directory.Exists(fullPath))
                {
                    var target = new DirectoryInfo(fullPath).ResolveLinkTarget(true);
                    if (target != null)
                        fullPath = Path.GetFullPath(target.FullName);
                }
            }
            catch (IOException)
            {
                // Keep the canonical full path. Per-segment link escape checks belong to McpPathSandbox.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the canonical full path when metadata cannot be resolved.
            }

            return Path.TrimEndingDirectorySeparator(fullPath);
        }

        private static string CreateId(string root)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(root));
            return Convert.ToHexString(hash).ToLowerInvariant()[..12];
        }

        private static readonly StringComparer _pathComparer =
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        private readonly Func<IReadOnlyCollection<string>> _knownRootsProvider;
    }
}
