using DevBoard.Models;
using Xunit;

namespace DevBoard.Tests;

public class PullRequestRemoteTests
{
    [Theory]
    [InlineData("https://github.com/acme/widgets.git")]
    [InlineData("git@github.com:acme/widgets.git")]
    public void TryCreate_GitHubRemote_MapsPullRefs(string url)
    {
        var remote = new Remote { Name = "origin", URL = url };

        var ok = PullRequestRemote.TryCreate(remote, 42, out var descriptor);

        Assert.True(ok);
        Assert.Equal(PullRequestProvider.GitHub, descriptor.Provider);
        Assert.Equal("origin", descriptor.RemoteName);
        Assert.Equal(42, descriptor.Number);
        Assert.Equal("refs/pull/42/merge", descriptor.MergeRemoteRef);
        Assert.Equal("refs/pull/42/head", descriptor.HeadRemoteRef);
        Assert.Equal("refs/devboard/pull-requests/origin/42/merge", descriptor.MergeLocalRef);
        Assert.Equal("refs/devboard/pull-requests/origin/42/head", descriptor.HeadLocalRef);
    }

    [Theory]
    [InlineData("https://dev.azure.com/acme/widgets/_git/app")]
    [InlineData("https://acme.visualstudio.com/widgets/_git/app")]
    [InlineData("git@ssh.dev.azure.com:v3/acme/widgets/app")]
    public void TryCreate_AzureDevOpsRemote_MapsMergeRef(string url)
    {
        var remote = new Remote { Name = "azure", URL = url };

        var ok = PullRequestRemote.TryCreate(remote, 53576, out var descriptor);

        Assert.True(ok);
        Assert.Equal(PullRequestProvider.AzureDevOps, descriptor.Provider);
        Assert.Equal("refs/pull/53576/merge", descriptor.MergeRemoteRef);
        Assert.Null(descriptor.HeadRemoteRef);
        Assert.Equal("refs/devboard/pull-requests/azure/53576/merge", descriptor.MergeLocalRef);
        Assert.Null(descriptor.HeadLocalRef);
    }

    [Fact]
    public void TryCreate_UnsupportedRemote_ReturnsFalse()
    {
        var remote = new Remote { Name = "origin", URL = "https://gitlab.com/acme/widgets.git" };

        var ok = PullRequestRemote.TryCreate(remote, 42, out var descriptor);

        Assert.False(ok);
        Assert.Null(descriptor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryCreate_InvalidPullRequestNumber_ReturnsFalse(int number)
    {
        var remote = new Remote { Name = "origin", URL = "https://github.com/acme/widgets.git" };

        var ok = PullRequestRemote.TryCreate(remote, number, out var descriptor);

        Assert.False(ok);
        Assert.Null(descriptor);
    }
}
