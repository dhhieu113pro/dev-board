using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SourceGit.Mcp.Services
{
    public sealed record McpSkillInfo(string Name, string Path, DateTimeOffset ModifiedAt, bool Enabled, bool BuiltIn, string SourceUrl, string ResolvedSourceUrl, string ContentSha256, string License);
    public sealed record McpSkillDocument(string Name, string Path, string Content, DateTimeOffset ModifiedAt, bool Enabled, bool BuiltIn, string SourceUrl, string ResolvedSourceUrl, string ContentSha256, string License);
    internal sealed record McpSkillMetadata(bool Enabled, bool BuiltIn, string SourceUrl, string ResolvedSourceUrl, string ContentSha256, string License);

    public sealed class McpSkillStore
    {
        public McpSkillStore(string skillsDirectory)
        {
            _root = Path.GetFullPath(skillsDirectory ?? throw new ArgumentNullException(nameof(skillsDirectory)));
            Directory.CreateDirectory(_root);
            SeedBuiltIns();
        }

        public IReadOnlyList<McpSkillInfo> List() => Directory.EnumerateDirectories(_root)
            .Where(x => File.Exists(Path.Combine(x, "SKILL.md")))
            .Select(x => ToInfo(Path.GetFileName(x), x))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        public IReadOnlyList<McpSkillDocument> ListEnabled() => List().Where(x => x.Enabled).Select(x => Get(x.Name)).ToArray();

        public McpSkillDocument Get(string name)
        {
            var dir = GetDirectory(name);
            var path = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(path)) throw new KeyNotFoundException($"Skill '{name}' was not found.");
            var meta = ReadMetadata(dir);
            return new McpSkillDocument(name, path, File.ReadAllText(path), File.GetLastWriteTimeUtc(path), meta.Enabled, meta.BuiltIn, meta.SourceUrl, meta.ResolvedSourceUrl, meta.ContentSha256, meta.License);
        }

        public McpSkillDocument Create(string name, string content)
        {
            var dir = GetDirectory(name);
            if (Directory.Exists(dir)) throw new InvalidOperationException($"Skill '{name}' already exists.");
            Directory.CreateDirectory(dir);
            WriteAtomic(dir, content, new McpSkillMetadata(true, false, null, null, null, ReadFrontMatter(content).License));
            return Get(name);
        }

        public McpSkillDocument Update(string name, string content)
        {
            var current = Get(name);
            var dir = GetDirectory(name);
            var meta = ReadMetadata(dir);
            WriteAtomic(dir, content, meta with { License = ReadFrontMatter(content).License });
            return Get(current.Name);
        }

        public McpSkillDocument InstallRemote(string content, McpSkillFrontMatter frontMatter, McpRemoteSkillDocument source, bool enabled)
        {
            var dir = GetDirectory(frontMatter.Name);
            if (Directory.Exists(dir)) throw new InvalidOperationException($"Skill '{frontMatter.Name}' already exists.");
            Directory.CreateDirectory(dir);
            WriteAtomic(dir, content, new McpSkillMetadata(enabled, false, source.SourceUrl, source.ResolvedUrl, source.Sha256, frontMatter.License));
            return Get(frontMatter.Name);
        }

        public McpSkillDocument ReplaceRemote(string name, string content, McpSkillFrontMatter frontMatter, McpRemoteSkillDocument source)
        {
            if (!string.Equals(name, frontMatter.Name, StringComparison.Ordinal)) throw new InvalidDataException("Upstream skill name does not match installed name.");
            var existing = Get(name);
            if (existing.BuiltIn) throw new InvalidOperationException("Built-in skills cannot be remotely updated.");
            var dir = GetDirectory(name);
            var meta = ReadMetadata(dir);
            WriteAtomic(dir, content, meta with { SourceUrl = source.SourceUrl, ResolvedSourceUrl = source.ResolvedUrl, ContentSha256 = source.Sha256, License = frontMatter.License });
            return Get(name);
        }

        public McpSkillDocument SetEnabled(string name, bool enabled)
        {
            var dir = GetDirectory(name);
            Get(name);
            WriteMetadata(dir, ReadMetadata(dir) with { Enabled = enabled });
            return Get(name);
        }

        public bool Delete(string name)
        {
            var doc = Get(name);
            if (doc.BuiltIn) throw new InvalidOperationException($"Built-in skill '{name}' cannot be deleted. Disable it instead.");
            Directory.Delete(GetDirectory(name), true);
            return true;
        }

        public static McpSkillFrontMatter ReadFrontMatter(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("Skill document is empty.");
            string name = null, description = null, license = null;
            foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim().Trim('"', '\'');
                if (key.Equals("name", StringComparison.OrdinalIgnoreCase)) name = value;
                else if (key.Equals("description", StringComparison.OrdinalIgnoreCase)) description = value;
                else if (key.Equals("license", StringComparison.OrdinalIgnoreCase)) license = value;
            }
            ValidateName(name);
            if (string.IsNullOrWhiteSpace(description)) throw new InvalidDataException("Skill front matter requires description.");
            return new McpSkillFrontMatter(name, description, license);
        }

        private void SeedBuiltIns()
        {
            foreach (var skill in McpBuiltInSkillCatalog.All)
            {
                var dir = GetDirectory(skill.Name);
                if (File.Exists(Path.Combine(dir, "SKILL.md"))) continue;
                Directory.CreateDirectory(dir);
                WriteAtomic(dir, skill.Content, new McpSkillMetadata(false, true, skill.SourceUrl, skill.SourceUrl, null, skill.License));
            }
        }

        private McpSkillInfo ToInfo(string name, string dir)
        {
            var path = Path.Combine(dir, "SKILL.md");
            var meta = ReadMetadata(dir);
            return new McpSkillInfo(name, path, File.GetLastWriteTimeUtc(path), meta.Enabled, meta.BuiltIn, meta.SourceUrl, meta.ResolvedSourceUrl, meta.ContentSha256, meta.License);
        }

        private string GetDirectory(string name)
        {
            ValidateName(name);
            return Path.Combine(_root, name);
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant))
                throw new ArgumentException("Skill name must be 1-64 characters using letters, numbers, '.', '_' or '-'.", nameof(name));
        }

        private static McpSkillMetadata ReadMetadata(string dir)
        {
            var path = Path.Combine(dir, "metadata.json");
            if (!File.Exists(path)) return new McpSkillMetadata(true, false, null, null, null, null);
            return JsonSerializer.Deserialize<McpSkillMetadata>(File.ReadAllText(path)) ?? new McpSkillMetadata(true, false, null, null, null, null);
        }

        private static void WriteMetadata(string dir, McpSkillMetadata metadata) => File.WriteAllText(Path.Combine(dir, "metadata.json"), JsonSerializer.Serialize(metadata));

        private static void WriteAtomic(string dir, string content, McpSkillMetadata metadata)
        {
            var skillTemp = Path.Combine(dir, $".{Guid.NewGuid():N}.skill.tmp");
            var metaTemp = Path.Combine(dir, $".{Guid.NewGuid():N}.meta.tmp");
            try
            {
                File.WriteAllText(skillTemp, content ?? string.Empty);
                File.WriteAllText(metaTemp, JsonSerializer.Serialize(metadata));
                File.Move(skillTemp, Path.Combine(dir, "SKILL.md"), true);
                File.Move(metaTemp, Path.Combine(dir, "metadata.json"), true);
            }
            finally
            {
                if (File.Exists(skillTemp)) File.Delete(skillTemp);
                if (File.Exists(metaTemp)) File.Delete(metaTemp);
            }
        }

        private readonly string _root;
    }

    public sealed record McpSkillFrontMatter(string Name, string Description, string License);
}
