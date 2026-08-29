using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SourceGit.Mcp.Services
{
    public sealed record McpSkillRoute(string Name, string Description, string Reason, int Score);

    public sealed class McpSkillRouter
    {
        public McpSkillRouter(McpSkillStore store) => _store = store;

        public IReadOnlyList<McpSkillRoute> Route(string task)
        {
            if (string.IsNullOrWhiteSpace(task)) throw new ArgumentException("Task is required.", nameof(task));
            var taskTokens = Tokens(task);
            return _store.ListEnabled().Select(skill =>
            {
                var front = McpSkillStore.ReadFrontMatter(skill.Content);
                var matches = Tokens(skill.Name + " " + front.Description).Intersect(taskTokens, StringComparer.OrdinalIgnoreCase).ToArray();
                return new McpSkillRoute(skill.Name, front.Description, matches.Length == 0 ? "Matched task intent" : "Matched: " + string.Join(", ", matches), matches.Length * 2);
            }).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static HashSet<string> Tokens(string text) => Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+(?:-[a-z0-9]+)?", RegexOptions.CultureInvariant).Select(x => x.Value).Where(x => x.Length > 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        private readonly McpSkillStore _store;
    }
}
