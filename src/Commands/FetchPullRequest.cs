using System.Threading.Tasks;

namespace DevBoard.Commands
{
    public sealed class FetchPullRequest : Command
    {
        public FetchPullRequest(string repo, string remote, string remoteRef, string localRef)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = $"fetch --progress --verbose {remote} +{remoteRef}:{localRef}";

            var account = FindBoundGitHubAccount();
            if (account?.AuthType == Models.GitHubAuthType.PersonalAccessToken)
                ApplyGitHubCredential(account);
        }

        public async Task<bool> RunAsync()
        {
            var configuredKey = await new Config(WorkingDirectory)
                .GetAsync($"remote.{_remote}.sshkey")
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(configuredKey))
                SSHKey = configuredKey;
            else if (string.IsNullOrEmpty(SSHKey))
                ApplyGitHubCredential(await Services.GitHubCredential.DetectForRepositoryAsync(WorkingDirectory).ConfigureAwait(false));

            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _remote;
    }
}
