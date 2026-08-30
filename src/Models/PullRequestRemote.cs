using System;

namespace DevBoard.Models
{
    public enum PullRequestProvider
    {
        GitHub,
        AzureDevOps,
    }

    public sealed class PullRequestRemote
    {
        public PullRequestProvider Provider { get; }
        public string RemoteName { get; }
        public int Number { get; }
        public string MergeRemoteRef { get; }
        public string MergeLocalRef { get; }
        public string HeadRemoteRef { get; }
        public string HeadLocalRef { get; }

        private PullRequestRemote(PullRequestProvider provider, string remoteName, int number, bool hasHeadRef)
        {
            Provider = provider;
            RemoteName = remoteName;
            Number = number;
            MergeRemoteRef = $"refs/pull/{number}/merge";
            MergeLocalRef = $"refs/devboard/pull-requests/{remoteName}/{number}/merge";
            HeadRemoteRef = hasHeadRef ? $"refs/pull/{number}/head" : null;
            HeadLocalRef = hasHeadRef ? $"refs/devboard/pull-requests/{remoteName}/{number}/head" : null;
        }

        public static bool TryCreate(Remote remote, int pullRequestNumber, out PullRequestRemote descriptor)
        {
            descriptor = null;
            if (remote == null || pullRequestNumber <= 0 || string.IsNullOrWhiteSpace(remote.Name))
                return false;

            if (!TryGetHost(remote.URL, out var host))
                return false;

            if (host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                descriptor = new PullRequestRemote(PullRequestProvider.GitHub, remote.Name, pullRequestNumber, true);
                return true;
            }

            if (host.Contains("azure.com", StringComparison.OrdinalIgnoreCase) ||
                host.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                descriptor = new PullRequestRemote(PullRequestProvider.AzureDevOps, remote.Name, pullRequestNumber, false);
                return true;
            }

            return false;
        }

        private static bool TryGetHost(string url, out string host)
        {
            host = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                host = uri.Host;
                return true;
            }

            var at = url.IndexOf('@');
            var colon = at >= 0 ? url.IndexOf(':', at + 1) : -1;
            if (at >= 0 && colon > at + 1)
            {
                host = url.Substring(at + 1, colon - at - 1);
                return true;
            }

            return false;
        }
    }
}
