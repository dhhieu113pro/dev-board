using System.Collections.Generic;
using DevBoard.Models;
using Xunit;

namespace DevBoard.Tests;

public class PullRequestCommitRangeTests
{
    [Fact]
    public void FromMergeRef_UsesSyntheticMergeParentsAndReverseOrder()
    {
        var limits = PullRequestCommitRange.FromMergeRef("refs/devboard/pull-requests/origin/42/merge");

        Assert.Equal("--reverse refs/devboard/pull-requests/origin/42/merge^1..refs/devboard/pull-requests/origin/42/merge^2", limits);
    }

    [Fact]
    public void FromHeadFallback_UsesMergeBaseAndReverseOrder()
    {
        var limits = PullRequestCommitRange.FromHeadFallback(
            "0123456789abcdef",
            "refs/devboard/pull-requests/origin/42/head");

        Assert.Equal("--reverse 0123456789abcdef..refs/devboard/pull-requests/origin/42/head", limits);
    }

    [Fact]
    public void ContainsMergeCommit_ReturnsTrueWhenAnyCommitHasMultipleParents()
    {
        var commits = new List<Commit>
        {
            CommitWithParents("a"),
            CommitWithParents("b", "c")
        };

        Assert.True(PullRequestCommitRange.ContainsMergeCommit(commits));
    }

    [Fact]
    public void ContainsMergeCommit_ReturnsFalseForLinearHistory()
    {
        var commits = new List<Commit>
        {
            CommitWithParents("a"),
            CommitWithParents("b")
        };

        Assert.False(PullRequestCommitRange.ContainsMergeCommit(commits));
    }

    private static Commit CommitWithParents(params string[] parents)
    {
        var commit = new Commit();
        commit.ParseParents(string.Join(' ', parents));
        return commit;
    }
}
