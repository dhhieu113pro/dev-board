using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public class AddWorktreeTests
{
    [Theory]
    [InlineData("feat/sprint246/53576-email-tech-change-the-email-queue-to-use-postmarkapp", "53576-email-tech-change-the-email-queue-to-use-postmarkapp")]
    [InlineData("fix/12345-something", "12345-something")]
    [InlineData("plain-worktree", "plain-worktree")]
    [InlineData("feature/nested/name/", "name")]
    public void DeriveWorktreeName_UsesLastBranchSegment(string title, string expected)
    {
        Assert.Equal(expected, AddWorktree.DeriveWorktreeName(title));
    }
}
