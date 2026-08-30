using System.Diagnostics;

using DevBoard.Commands;

namespace DevBoard.Tests;

public class GitAuthenticationTests
{
    [Fact]
    public void BackgroundAuthenticationDisablesInteractivePrompts()
    {
        var command = new TestCommand
        {
            NonInteractiveAuthentication = true,
            Args = "fetch origin",
        };

        var startInfo = command.GetStartInfo();

        Assert.Equal("0", startInfo.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.DoesNotContain("SOURCEGIT_LAUNCH_AS_ASKPASS", startInfo.Environment.Keys);
        Assert.Equal("ssh -o BatchMode=yes", startInfo.Environment["GIT_SSH_COMMAND"]);
        Assert.Contains("-c credential.helper= ", startInfo.Arguments);
    }

    [Fact]
    public void BackgroundAuthenticationWithSshKeyUsesBatchMode()
    {
        var command = new TestCommand
        {
            NonInteractiveAuthentication = true,
            SSHKey = @"C:\\Users\\test\\.ssh\\id_ed25519",
            Args = "fetch origin",
        };

        var startInfo = command.GetStartInfo();

        Assert.Contains("-o BatchMode=yes", startInfo.Environment["GIT_SSH_COMMAND"]);
        Assert.Contains("C:/Users/test/.ssh/id_ed25519", startInfo.Environment["GIT_SSH_COMMAND"]);
    }

    private sealed class TestCommand : Command
    {
        public ProcessStartInfo GetStartInfo() => CreateGitStartInfo(true);
    }
}
