using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevBoard.DevSpaces
{
    internal static class RoslynWorkspaceDiscovery
    {
        private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            "bin",
            "obj",
            "node_modules",
            ".vs",
            ".idea",
            ".vscode",
        };

        public static string FindWorkspace(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
                return null;

            var root = Select(Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.TopDirectoryOnly));
            if (root != null)
                return root;

            return Select(EnumerateDescendants(workspaceRoot));
        }

        private static IEnumerable<string> EnumerateDescendants(string root)
        {
            var pending = new Stack<string>(Directory.EnumerateDirectories(root)
                .Where(x => !IgnoredDirectories.Contains(Path.GetFileName(x)))
                .OrderByDescending(x => x, StringComparer.Ordinal));

            while (pending.Count > 0)
            {
                var directory = pending.Pop();

                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                    yield return file;

                foreach (var child in Directory.EnumerateDirectories(directory)
                    .Where(x => !IgnoredDirectories.Contains(Path.GetFileName(x)))
                    .OrderByDescending(x => x, StringComparer.Ordinal))
                {
                    pending.Push(child);
                }
            }
        }

        private static string Select(IEnumerable<string> paths)
        {
            var candidates = paths.ToArray();
            foreach (var extension in new[] { ".slnx", ".sln", ".csproj" })
            {
                var match = candidates
                    .Where(x => string.Equals(Path.GetExtension(x), extension, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
