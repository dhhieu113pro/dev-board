using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Mcp.Services
{
    public sealed class McpSkillService
    {
        public McpSkillService(McpSkillStore store, McpSkillRouter router, McpRemoteSkillFetcher remote)
        {
            Store = store;
            Router = router;
            _remote = remote;
        }

        public McpSkillStore Store { get; }
        public McpSkillRouter Router { get; }

        public async Task<McpSkillDocument> InstallAsync(string source, bool enabled, string expectedName, CancellationToken ct)
        {
            var remote = await _remote.FetchAsync(source, ct).ConfigureAwait(false);
            var front = McpSkillStore.ReadFrontMatter(remote.Content);
            if (!string.IsNullOrWhiteSpace(expectedName) && !string.Equals(expectedName, front.Name, StringComparison.Ordinal)) throw new InvalidOperationException("Remote skill name does not match expected name.");
            return Store.InstallRemote(remote.Content, front, remote, enabled);
        }

        public async Task<object[]> CheckUpdatesAsync(string name, CancellationToken ct)
        {
            var skills = string.IsNullOrWhiteSpace(name) ? Store.List().Where(x => !x.BuiltIn && !string.IsNullOrWhiteSpace(x.SourceUrl)) : new[] { Store.List().Single(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) };
            var results = new System.Collections.Generic.List<object>();
            foreach (var skill in skills)
            {
                try
                {
                    var remote = await _remote.FetchAsync(skill.SourceUrl, ct).ConfigureAwait(false);
                    results.Add(new { name = skill.Name, source_url = skill.SourceUrl, installed_sha256 = skill.ContentSha256, remote_sha256 = remote.Sha256, status = remote.Sha256 == skill.ContentSha256 ? "up_to_date" : "update_available" });
                }
                catch (Exception ex) { results.Add(new { name = skill.Name, source_url = skill.SourceUrl, installed_sha256 = skill.ContentSha256, remote_sha256 = (string)null, status = "unavailable", message = ex.Message }); }
            }
            return results.ToArray();
        }

        public async Task<McpSkillDocument> UpdateFromSourceAsync(string name, CancellationToken ct)
        {
            var current = Store.Get(name);
            if (string.IsNullOrWhiteSpace(current.SourceUrl)) throw new InvalidOperationException("Skill has no recorded remote source.");
            var remote = await _remote.FetchAsync(current.SourceUrl, ct).ConfigureAwait(false);
            var front = McpSkillStore.ReadFrontMatter(remote.Content);
            return Store.ReplaceRemote(name, remote.Content, front, remote);
        }

        private readonly McpRemoteSkillFetcher _remote;
    }
}
