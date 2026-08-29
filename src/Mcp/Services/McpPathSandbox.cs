using System;
using System.IO;

namespace SourceGit.Mcp.Services
{
    public sealed class McpPathSandbox
    {
        public string Resolve(string workspaceRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path is required.", nameof(relativePath));
            if (Path.IsPathRooted(relativePath))
                throw new UnauthorizedAccessException("Rooted paths are not allowed.");

            var canonicalRoot = CanonicalizeExistingDirectory(workspaceRoot);
            var candidate = Path.GetFullPath(Path.Combine(canonicalRoot, relativePath));
            EnsureInsideRoot(canonicalRoot, candidate);
            EnsureExistingSegmentsStayInsideRoot(canonicalRoot, relativePath);
            return candidate;
        }

        private static void EnsureExistingSegmentsStayInsideRoot(string canonicalRoot, string relativePath)
        {
            var current = canonicalRoot;
            var segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                if (segment == ".")
                    continue;
                if (segment == "..")
                    throw new UnauthorizedAccessException("Path traversal is not allowed.");

                current = Path.Combine(current, segment);
                FileSystemInfo info = null;
                if (Directory.Exists(current))
                    info = new DirectoryInfo(current);
                else if (File.Exists(current))
                    info = new FileInfo(current);

                if (info == null)
                    continue;

                FileSystemInfo target;
                try
                {
                    target = info.ResolveLinkTarget(true);
                }
                catch (IOException)
                {
                    target = null;
                }
                catch (UnauthorizedAccessException)
                {
                    target = null;
                }

                if (target == null)
                    continue;

                current = Path.GetFullPath(target.FullName);
                EnsureInsideRoot(canonicalRoot, current);
            }
        }

        private static string CanonicalizeExistingDirectory(string root)
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (!Directory.Exists(fullPath))
                return fullPath;

            try
            {
                var target = new DirectoryInfo(fullPath).ResolveLinkTarget(true);
                if (target != null)
                    fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return fullPath;
        }

        private static void EnsureInsideRoot(string root, string candidate)
        {
            if (_pathComparer.Equals(root, candidate))
                return;

            var prefix = root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, _pathComparison))
                throw new UnauthorizedAccessException("Path escapes the selected workspace.");
        }

        private static readonly StringComparer _pathComparer =
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        private static readonly StringComparison _pathComparison =
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
    }
}
