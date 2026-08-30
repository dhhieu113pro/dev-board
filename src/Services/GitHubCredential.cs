using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevBoard.Services
{
    public static partial class GitHubCredential
    {
        public static Models.GitHubAccount FindForRepository(string repoPath)
        {
            if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
                return null;

            try
            {
                var gitDir = new Commands.QueryGitDir(repoPath).GetResult();
                if (string.IsNullOrWhiteSpace(gitDir))
                    return null;

                var settings = Models.RepositorySettings.Get(gitDir);
                if (settings.GitHubAccountId == Guid.Empty)
                    return null;

                var account = GitHubAccountStore.Instance.Get(settings.GitHubAccountId);
                if (account != null)
                    return account;

                // Do not leave a repository permanently pointing at a removed account.
                settings.GitHubAccountId = Guid.Empty;
                settings.Save();
            }
            catch
            {
            }

            return null;
        }

        public static bool BindRepository(string repoPath, Models.GitHubAccount account)
        {
            if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
                return false;

            try
            {
                var gitDir = new Commands.QueryGitDir(repoPath).GetResult();
                if (string.IsNullOrWhiteSpace(gitDir))
                    return false;

                var settings = Models.RepositorySettings.Get(gitDir);
                var accountId = account?.Id ?? Guid.Empty;
                if (settings.GitHubAccountId == accountId)
                    return true;

                settings.GitHubAccountId = accountId;
                settings.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<Models.GitHubAccount> DetectForRepositoryAsync(string repoPath)
        {
            var accounts = GitHubAccountStore.Instance.Accounts;
            if (accounts.Count == 0 || string.IsNullOrWhiteSpace(repoPath))
                return null;

            var bound = FindForRepository(repoPath);
            if (bound != null)
                return bound;

            try
            {
                var remotes = await new Commands.QueryRemotes(repoPath).GetResultAsync().ConfigureAwait(false);
                var selected = SelectAccountForRemotes(remotes.Select(x => x.URL), accounts);
                if (selected != null)
                {
                    BindRepository(repoPath, selected);
                    return selected;
                }

                // SSH aliases may not expose github.com in the URL. If an SSH remote exists
                // and exactly one valid SSH account is configured, the choice is deterministic.
                if (remotes.Any(x => IsSshRemote(x.URL)))
                {
                    var sshAccounts = accounts
                        .Where(x => x.HasValidCredentials && x.AuthType == Models.GitHubAuthType.SSHKey)
                        .Take(2)
                        .ToList();
                    if (sshAccounts.Count == 1)
                    {
                        BindRepository(repoPath, sshAccounts[0]);
                        return sshAccounts[0];
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Backfills deterministic bindings from local repository metadata/remotes only.
        /// It intentionally does not scan credential stores or call GitHub.
        /// </summary>
        public static async Task WarmupRepositoryBindingsAsync(IEnumerable<ViewModels.RepositoryNode> nodes)
        {
            if (nodes == null || GitHubAccountStore.Instance.Accounts.Count == 0)
                return;

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                if (node.IsRepository)
                {
                    if (Directory.Exists(node.Id))
                        await DetectForRepositoryAsync(node.Id).ConfigureAwait(false);
                }
                else if (node.SubNodes.Count > 0)
                {
                    await WarmupRepositoryBindingsAsync(node.SubNodes).ConfigureAwait(false);
                }
            }
        }

        public static string ExtractGitHubOwner(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;
            var match = GitHubOwnerRegex().Match(url.Trim());
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public static bool IsSshRemote(string url)
            => !string.IsNullOrWhiteSpace(url) &&
               (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase));

        public static bool IsAccountCompatible(Models.GitHubAccount account, bool sshRemote)
            => sshRemote
                ? account?.AuthType == Models.GitHubAuthType.SSHKey
                : account?.AuthType is Models.GitHubAuthType.PersonalAccessToken or Models.GitHubAuthType.GitHubCli;

        public static Models.GitHubAccount SelectAccountForRemotes(
            IEnumerable<string> remoteUrls,
            IEnumerable<Models.GitHubAccount> accounts)
        {
            var remotes = remoteUrls?.Where(IsGitHubRemote).ToList() ?? [];
            var configured = accounts?.Where(x => x != null).ToList() ?? [];
            if (remotes.Count == 0 || configured.Count == 0)
                return null;

            Models.GitHubAccount matched = null;
            foreach (var remote in remotes)
            {
                var owner = ExtractGitHubOwner(remote);
                if (string.IsNullOrEmpty(owner))
                    continue;

                foreach (var account in configured)
                {
                    if (!account.HasValidCredentials ||
                        !IsAccountCompatible(account, IsSshRemote(remote)) ||
                        !string.Equals(account.Username, owner, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (matched != null && matched.Id != account.Id)
                        return null;
                    matched = account;
                }
            }

            if (matched != null)
                return matched;

            var allSsh = remotes.All(IsSshRemote);
            var allHttps = remotes.All(x => !IsSshRemote(x));
            if (!allSsh && !allHttps)
                return null;

            var compatible = configured
                .Where(x => x.HasValidCredentials && IsAccountCompatible(x, allSsh))
                .Take(2)
                .ToList();
            return compatible.Count == 1 ? compatible[0] : null;
        }

        private static bool IsGitHubRemote(string url)
            => !string.IsNullOrEmpty(ExtractGitHubOwner(url));

        [GeneratedRegex(@"^(?:https?://|ssh://)?(?:[^@/\s]+@)?github\.com[:/]([\w.\-]+)/", RegexOptions.IgnoreCase)]
        private static partial Regex GitHubOwnerRegex();
    }
}
