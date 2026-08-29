using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SourceGit.DevSpaces.Roslyn
{
    public static class RoslynWorkspaceDiscovery
    {
        public static IReadOnlyList<string> FindCandidates(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
                return [];

            var root = Path.GetFullPath(workspaceRoot);
            var candidates = new List<Candidate>();
            Collect(root, root, candidates);

            return candidates
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Depth)
                .ThenBy(x => x.Path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .Select(x => x.Path)
                .ToArray();
        }

        private static void Collect(string root, string directory, List<Candidate> candidates)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory);
            }
            catch
            {
                return;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                var priority = extension.ToLowerInvariant() switch
                {
                    ".slnx" => 0,
                    ".sln" => 1,
                    ".csproj" => 2,
                    _ => -1,
                };

                if (priority < 0)
                    continue;

                var relative = Path.GetRelativePath(root, file);
                var depth = relative.Count(x => x == Path.DirectorySeparatorChar || x == Path.AltDirectorySeparatorChar);
                candidates.Add(new Candidate(Path.GetFullPath(file), priority, depth));
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory);
            }
            catch
            {
                return;
            }

            foreach (var child in directories)
            {
                var name = Path.GetFileName(child);
                if (_ignoredDirectories.Contains(name))
                    continue;

                Collect(root, child, candidates);
            }
        }

        private sealed record Candidate(string Path, int Priority, int Depth);

        private static readonly HashSet<string> _ignoredDirectories = new(
            [".git", ".vs", "bin", "obj", "node_modules"],
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }
}
