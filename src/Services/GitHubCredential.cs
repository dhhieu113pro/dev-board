using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevBoard.Services
{
    public static partial class GitHubCredential
    {
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
                : account?.AuthType == Models.GitHubAuthType.PersonalAccessToken;

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
