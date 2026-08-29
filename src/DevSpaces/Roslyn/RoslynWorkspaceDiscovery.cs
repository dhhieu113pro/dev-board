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
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not ".slnx" and not ".sln" and not ".csproj")
                    continue;

                var relative = Path.GetRelativePath(root, file);
                var depth = relative.Count(x => x == Path.DirectorySeparatorChar || x == Path.AltDirectorySeparatorChar);
                var isRoot = depth == 0;
                var priority = extension switch
                {
                    ".slnx" when isRoot => 0,
                    ".sln" when isRoot => 1,
                    ".slnx" => 2,
                    ".sln" => 3,
                    ".csproj" when isRoot => 4,
                    ".csproj" => 5,
                    _ => 6,
                };

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
