using System.Text;
using System.Threading.Tasks;

namespace DevBoard.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool noTags, bool force)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);
            builder.Append("fetch --progress --verbose ");
            builder.Append(noTags ? "--no-tags " : "--tags ");
            if (force)
                builder.Append("--force ");
            builder.Append(remote);

            Args = builder.ToString();
            ResolveBoundCredential();
        }

        public Fetch(string repo, string remote)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            NonInteractiveAuthentication = true;

            Args = $"fetch --progress --verbose {remote}";
            ResolveBoundCredential();
        }

        public Fetch(string repo, Models.Branch local, Models.Branch remote)
        {
            _remote = remote.Remote;

            WorkingDirectory = repo;
            Context = repo;
            Args = $"fetch --progress --verbose {remote.Remote} {remote.Name}:{local.Name}";
            ResolveBoundCredential();
        }

        public async Task<bool> RunAsync()
        {
            var configuredKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(configuredKey))
                SSHKey = configuredKey;

            if (!await PrepareRepositoryCredentialAsync().ConfigureAwait(false))
                return false;

            return await ExecAsync().ConfigureAwait(false);
        }

        private void ResolveBoundCredential()
        {
            // Keep the existing eager PAT path for callers that inspect/execute Fetch directly.
            // GitHub CLI credentials are intentionally resolved only immediately before RunAsync.
            var account = FindBoundGitHubAccount();
            if (account?.AuthType == Models.GitHubAuthType.PersonalAccessToken)
                ApplyGitHubCredential(account);
        }

        private readonly string _remote;
    }
}
