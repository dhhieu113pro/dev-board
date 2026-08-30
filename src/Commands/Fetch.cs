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
            await PrepareRepositoryAuthenticationAsync(_remote).ConfigureAwait(false);
            return await ExecAsync().ConfigureAwait(false);
        }

        private void ResolveBoundCredential()
        {
            // PAT credentials can be loaded without starting another process. GitHub CLI
            // credentials are resolved at execution time for the exact bound account.
            var account = FindBoundGitHubAccount();
            if (account?.AuthType == Models.GitHubAuthType.PersonalAccessToken)
                ApplyGitHubCredential(account);
        }

        private readonly string _remote;
    }
}
