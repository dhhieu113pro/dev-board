using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace DevBoard.DevSpaces
{
    public static class DevSpaceProjectDiscovery
    {
        private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".hg",
            ".svn",
            ".vs",
            ".angular",
            "node_modules",
            "bin",
            "obj",
            "dist",
        };

        public static IReadOnlyList<DevSpaceTerminalProfile> Discover(
            string workspacePath,
            IEnumerable<DevSpaceTerminalProfile> manualProfiles = null)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                throw new ArgumentException("Workspace path must not be empty.", nameof(workspacePath));

            var manual = (manualProfiles ?? [])
                .Where(x => x != null)
                .ToList();
            var manualNames = new HashSet<string>(
                manual.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => x.Name),
                StringComparer.OrdinalIgnoreCase);

            var workspace = Path.GetFullPath(workspacePath);
            if (!Directory.Exists(workspace))
                return manual;

            var discovered = new List<DevSpaceTerminalProfile>();
            foreach (var directory in EnumerateWorkspaceDirectories(workspace))
            {
                DiscoverDotNetProjects(workspace, directory, discovered);

                var angularJson = Path.Combine(directory, "angular.json");
                if (File.Exists(angularJson))
                    DiscoverAngularProjects(workspace, directory, angularJson, discovered);
            }

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var automatic = discovered
                .Where(x => !manualNames.Contains(x.Name))
                .Where(x => unique.Add($"{x.Path}\n{x.Name}\n{x.Command}"))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase);

            return manual.Concat(automatic).ToArray();
        }

        private static IEnumerable<string> EnumerateWorkspaceDirectories(string workspace)
        {
            var pending = new Stack<string>();
            pending.Push(workspace);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                yield return current;

                string[] children;
                try
                {
                    children = Directory.GetDirectories(current);
                }
                catch
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (IgnoredDirectories.Contains(Path.GetFileName(child)))
                        continue;

                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    pending.Push(child);
                }
            }
        }

        private static void DiscoverDotNetProjects(
            string workspace,
            string directory,
            ICollection<DevSpaceTerminalProfile> profiles)
        {
            string[] projectFiles;
            try
            {
                projectFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            foreach (var projectFile in projectFiles)
            {
                try
                {
                    var document = XDocument.Load(projectFile, LoadOptions.None);
                    var sdkNames = GetSdkNames(document).ToArray();
                    var isWeb = sdkNames.Any(x => IsSdk(x, "Microsoft.NET.Sdk.Web"));
                    var isWorker = sdkNames.Any(x => IsSdk(x, "Microsoft.NET.Sdk.Worker"));
                    if (!isWeb && !isWorker)
                        continue;

                    var relativeDirectory = GetRelativeProfilePath(workspace, directory);
                    var projectName = Path.GetFileNameWithoutExtension(projectFile);
                    var projectFileName = Path.GetFileName(projectFile);
                    var relativeProjectPath = Path.GetRelativePath(workspace, projectFile)
                        .Replace(Path.DirectorySeparatorChar, '/');

                    profiles.Add(new DevSpaceTerminalProfile
                    {
                        Id = $"discovered:dotnet:{relativeProjectPath}",
                        Name = projectName,
                        Icon = isWeb ? "🌐" : "⚙",
                        Path = relativeDirectory,
                        Command = $"dotnet run --project \"{projectFileName}\"",
                    });
                }
                catch
                {
                    // A malformed or unreadable project should not prevent discovery of the rest of the workspace.
                }
            }
        }

        private static IEnumerable<string> GetSdkNames(XDocument document)
        {
            var root = document.Root;
            if (root == null)
                yield break;

            var sdkAttribute = root.Attributes()
                .FirstOrDefault(x => string.Equals(x.Name.LocalName, "Sdk", StringComparison.OrdinalIgnoreCase));
            if (sdkAttribute != null)
            {
                foreach (var sdk in sdkAttribute.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    yield return sdk;
            }

            foreach (var sdkElement in root.Descendants().Where(x => string.Equals(x.Name.LocalName, "Sdk", StringComparison.OrdinalIgnoreCase)))
            {
                var name = sdkElement.Attributes()
                    .FirstOrDefault(x => string.Equals(x.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;
            }
        }

        private static bool IsSdk(string value, string expected)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim();
            var versionSeparator = normalized.IndexOf('/');
            if (versionSeparator >= 0)
                normalized = normalized[..versionSeparator];

            return string.Equals(normalized, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void DiscoverAngularProjects(
            string workspace,
            string angularDirectory,
            string angularJson,
            ICollection<DevSpaceTerminalProfile> profiles)
        {
            try
            {
                using var stream = File.OpenRead(angularJson);
                using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                if (!document.RootElement.TryGetProperty("projects", out var projects) ||
                    projects.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                var profilePath = GetRelativeProfilePath(workspace, angularDirectory);
                var packageManager = FindPackageManager(workspace, angularDirectory);
                foreach (var project in projects.EnumerateObject())
                {
                    if (project.Value.ValueKind != JsonValueKind.Object ||
                        !project.Value.TryGetProperty("projectType", out var projectType) ||
                        projectType.ValueKind != JsonValueKind.String ||
                        !string.Equals(projectType.GetString(), "application", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = project.Name;
                    profiles.Add(new DevSpaceTerminalProfile
                    {
                        Id = $"discovered:angular:{profilePath.Replace(Path.DirectorySeparatorChar, '/')}:{name}",
                        Name = name,
                        Icon = "🅰",
                        Path = profilePath,
                        Command = BuildAngularCommand(packageManager, name),
                    });
                }
            }
            catch
            {
                // Invalid angular.json files are ignored so other runnable projects can still be offered.
            }
        }

        private static string FindPackageManager(string workspace, string angularDirectory)
        {
            var current = Path.GetFullPath(angularDirectory);
            var workspaceRoot = Path.GetFullPath(workspace);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            while (IsWithinWorkspace(workspaceRoot, current, comparison))
            {
                if (File.Exists(Path.Combine(current, "pnpm-lock.yaml")))
                    return "pnpm";
                if (File.Exists(Path.Combine(current, "yarn.lock")))
                    return "yarn";
                if (File.Exists(Path.Combine(current, "package-lock.json")) ||
                    File.Exists(Path.Combine(current, "npm-shrinkwrap.json")))
                {
                    return "npm";
                }

                var fromPackageJson = ReadPackageManager(Path.Combine(current, "package.json"));
                if (fromPackageJson != null)
                    return fromPackageJson;

                if (string.Equals(current, workspaceRoot, comparison))
                    break;

                var parent = Directory.GetParent(current);
                if (parent == null)
                    break;
                current = parent.FullName;
            }

            return "npm";
        }

        private static string ReadPackageManager(string packageJson)
        {
            if (!File.Exists(packageJson))
                return null;

            try
            {
                using var stream = File.OpenRead(packageJson);
                using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
                if (!document.RootElement.TryGetProperty("packageManager", out var packageManager) ||
                    packageManager.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                var value = packageManager.GetString();
                if (value?.StartsWith("pnpm@", StringComparison.OrdinalIgnoreCase) == true)
                    return "pnpm";
                if (value?.StartsWith("yarn@", StringComparison.OrdinalIgnoreCase) == true)
                    return "yarn";
                if (value?.StartsWith("npm@", StringComparison.OrdinalIgnoreCase) == true)
                    return "npm";
            }
            catch
            {
            }

            return null;
        }

        private static string BuildAngularCommand(string packageManager, string projectName)
        {
            return packageManager switch
            {
                "pnpm" => $"pnpm exec ng serve {projectName}",
                "yarn" => $"yarn ng serve {projectName}",
                _ => $"npm exec -- ng serve {projectName}",
            };
        }

        private static string GetRelativeProfilePath(string workspace, string directory)
        {
            var relative = Path.GetRelativePath(workspace, directory);
            return relative == "." ? string.Empty : relative;
        }

        private static bool IsWithinWorkspace(string workspace, string path, StringComparison comparison)
        {
            if (string.Equals(workspace, path, comparison))
                return true;

            var prefix = workspace.EndsWith(Path.DirectorySeparatorChar)
                ? workspace
                : workspace + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, comparison);
        }
    }
}
