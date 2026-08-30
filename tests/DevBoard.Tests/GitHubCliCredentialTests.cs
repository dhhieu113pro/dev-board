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
        Assert.Equal("auth token --hostname github.com --user work-user", startInfo.Arguments);
        Assert.DoesNotContain("switch", startInfo.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }
}
