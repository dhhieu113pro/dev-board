using System;

using DevBoard.Services;
using Xunit;

namespace DevBoard.Tests;

public class GitHubCliCredentialTests
{
    [Fact]
    public void TokenCommandTargetsExactAccountWithoutSwitchingGlobalAccount()
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
    public void StatusJsonParsesAllAccountsAcrossHosts()
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
}
