using System.Threading.Tasks;

namespace DevBoard.Commands
{
    public partial class Command
    {
        protected Models.GitHubAccount FindBoundGitHubAccount()
        {
            return Services.GitHubCredential.FindForRepository(WorkingDirectory);
        }

        protected void ApplyGitHubCredential(Models.GitHubAccount account)
        {
            if (account == null)
                return;

            if (account.AuthType == Models.GitHubAuthType.PersonalAccessToken)
            {
                var token = Services.CredentialManager.GetToken(account.Id);
                if (!string.IsNullOrEmpty(token))
                {
                    GitHubUsername = account.Username;
                    GitHubToken = token;
                }
            }
            else if (account.AuthType == Models.GitHubAuthType.SSHKey && string.IsNullOrEmpty(SSHKey))
            {
                SSHKey = account.SSHKeyPath;
            }
        }

        protected async Task ApplyGitHubCredentialAsync(Models.GitHubAccount account)
        {
            if (account == null)
                return;

            if (account.AuthType != Models.GitHubAuthType.GitHubCli)
            {
                ApplyGitHubCredential(account);
                return;
            }

            var token = await Services.GitHubCliCredential
                .GetTokenAsync("github.com", account.Username, CancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                GitHubUsername = account.Username;
                GitHubToken = token;
            }
        }

        protected async Task PrepareRepositoryAuthenticationAsync(string remote)
        {
            var configuredKey = await new Config(WorkingDirectory)
                .GetAsync($"remote.{remote}.sshkey")
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(configuredKey))
            {
                SSHKey = configuredKey;
                return;
            }

            var account = FindBoundGitHubAccount() ??
                await Services.GitHubCredential
                    .DetectForRepositoryAsync(WorkingDirectory)
                    .ConfigureAwait(false);
            await ApplyGitHubCredentialAsync(account).ConfigureAwait(false);
        }
    }
}
