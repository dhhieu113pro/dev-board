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
    }
}
