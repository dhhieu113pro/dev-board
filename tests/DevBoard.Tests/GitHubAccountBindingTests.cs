using System;

using DevBoard.Models;
using DevBoard.Services;
using Xunit;

namespace DevBoard.Tests;

public class GitHubAccountBindingTests
{
    [Theory]
    [InlineData("https://github.com/octocat/hello-world.git", "octocat")]
    [InlineData("git@github.com:octocat/hello-world.git", "octocat")]
    [InlineData("ssh://git@github.com/octocat/hello-world.git", "octocat")]
    public void ExtractGitHubOwnerSupportsCommonRemoteFormats(string remote, string expected)
    {
        Assert.Equal(expected, GitHubCredential.ExtractGitHubOwner(remote));
    }

    [Fact]
    public void HttpsRemoteSelectsMatchingPatAccount()
    {
        var pat = Account("octocat", GitHubAuthType.PersonalAccessToken, token: "token");
        var ssh = Account("octocat", GitHubAuthType.SSHKey, sshKey: "/tmp/id_ed25519");

        var selected = GitHubCredential.SelectAccountForRemotes(
            ["https://github.com/octocat/hello-world.git"], [ssh, pat]);

        Assert.Same(pat, selected);
    }

    [Fact]
    public void HttpsRemoteSelectsMatchingGitHubCliAccount()
    {
        var cli = Account("octocat", GitHubAuthType.GitHubCli);
        var ssh = Account("octocat", GitHubAuthType.SSHKey, sshKey: "/tmp/id_ed25519");

        var selected = GitHubCredential.SelectAccountForRemotes(
            ["https://github.com/octocat/hello-world.git"], [ssh, cli]);

        Assert.Same(cli, selected);
    }

    [Fact]
    public void GitHubCliTokenCommandTargetsExactAccountWithoutSwitchingGlobalAccount()
    {
        var startInfo = GitHubCliCredential.CreateTokenStartInfo("github.com", "work-user");

        Assert.Equal("gh", startInfo.FileName);
        Assert.Equal(
            ["auth", "token", "--hostname", "github.com", "--user", "work-user"],
            startInfo.ArgumentList);
        Assert.DoesNotContain(startInfo.ArgumentList, value =>
            string.Equals(value, "switch", StringComparison.OrdinalIgnoreCase));
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.Environment.ContainsKey("GH_TOKEN"));
        Assert.False(startInfo.Environment.ContainsKey("GITHUB_TOKEN"));
        Assert.False(startInfo.Environment.ContainsKey("GH_ENTERPRISE_TOKEN"));
        Assert.False(startInfo.Environment.ContainsKey("GITHUB_ENTERPRISE_TOKEN"));
    }

    [Fact]
    public void GitHubCliStatusJsonParsesAllAccountsAcrossHosts()
    {
        const string json = """
            {
              "hosts": {
                "github.com": [
                  { "login": "personal", "active": true },
                  { "login": "work", "active": false }
                ],
                "github.example.com": [
                  { "login": "employee", "active": true }
                ]
              }
            }
            """;

        var accounts = GitHubCliCredential.ParseAccounts(json);

        Assert.Collection(
            accounts,
            account =>
            {
                Assert.Equal("github.com", account.Host);
                Assert.Equal("personal", account.Username);
                Assert.True(account.IsActive);
            },
            account =>
            {
                Assert.Equal("github.com", account.Host);
                Assert.Equal("work", account.Username);
                Assert.False(account.IsActive);
            },
            account =>
            {
                Assert.Equal("github.example.com", account.Host);
                Assert.Equal("employee", account.Username);
                Assert.True(account.IsActive);
            });
    }

    [Fact]
    public void SshRemoteSelectsMatchingSshAccount()
    {
        var pat = Account("octocat", GitHubAuthType.PersonalAccessToken, token: "token");
        var ssh = Account("octocat", GitHubAuthType.SSHKey, sshKey: "/tmp/id_ed25519");

        var selected = GitHubCredential.SelectAccountForRemotes(
            ["git@github.com:octocat/hello-world.git"], [pat, ssh]);

        Assert.Same(ssh, selected);
    }

    [Fact]
    public void OrganizationRepoSelectsOnlyCompatibleAccount()
    {
        var pat = Account("personal-user", GitHubAuthType.PersonalAccessToken, token: "token");

        var selected = GitHubCredential.SelectAccountForRemotes(
            ["https://github.com/example-org/private-repo.git"], [pat]);

        Assert.Same(pat, selected);
    }

    [Fact]
    public void OrganizationRepoWithMultipleCompatibleAccountsIsAmbiguous()
    {
        var first = Account("first-user", GitHubAuthType.PersonalAccessToken, token: "one");
        var second = Account("second-user", GitHubAuthType.PersonalAccessToken, token: "two");

        var selected = GitHubCredential.SelectAccountForRemotes(
            ["https://github.com/example-org/private-repo.git"], [first, second]);

        Assert.Null(selected);
    }

    private static GitHubAccount Account(
        string username,
        GitHubAuthType authType,
        string token = "",
        string sshKey = "")
    {
        return new GitHubAccount
        {
            Username = username,
            AuthType = authType,
            Token = token,
            SSHKeyPath = sshKey,
        };
    }
}
