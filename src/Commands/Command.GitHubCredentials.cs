using System;
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

        protected async Task<bool> PrepareRepositoryCredentialAsync()
        {
            var account = FindBoundGitHubAccount();
            if (account == null)
                account = await Services.GitHubCredential.DetectForRepositoryAsync(WorkingDirectory).ConfigureAwait(false);
            if (account == null)
                return true;

            if (!account.HasValidCredentials)
            {
                RaiseException($"GitHub account '{account.DisplayName}' does not have valid credentials.");
                return false;
            }

            try
            {
                if (account.AuthType == Models.GitHubAuthType.GitHubCli)
                {
                    var token = await Services.GitHubCliCredential.GetTokenAsync(
                        account.Host,
                        account.Username,
                        CancellationToken).ConfigureAwait(false);
                    GitHubUsername = account.Username;
                    GitHubToken = token;
                }
                else
                {
                    ApplyGitHubCredential(account);
                }
            }
            catch (Exception ex)
            {
                RaiseException($"Unable to use GitHub account '{account.DisplayName}': {ex.Message}");
                return false;
            }

            if (account.AuthType == Models.GitHubAuthType.PersonalAccessToken && string.IsNullOrEmpty(GitHubToken))
            {
                RaiseException($"GitHub account '{account.DisplayName}' has no saved token.");
                return false;
            }

            if (account.AuthType == Models.GitHubAuthType.SSHKey && string.IsNullOrEmpty(SSHKey))
            {
                RaiseException($"GitHub account '{account.DisplayName}' has no SSH key configured.");
                return false;
            }

            return true;
        }
    }
}
