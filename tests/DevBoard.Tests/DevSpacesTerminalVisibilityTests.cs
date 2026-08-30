using System;
using System.IO;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class DevSpacesTerminalVisibilityTests
    {
        [Fact]
        public void ActivatingDevSpacesDoesNotCreateImplicitTerminal()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

                spaces.EnsureFirstSession();

                Assert.Empty(spaces.Sessions);
                Assert.Null(spaces.ActiveTerminal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-terminal-visibility-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeLauncher : IDevSpaceSessionLauncher
        {
            public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null) =>
                new(terminal ?? string.Empty, [], workingDirectory);
        }
    }
}
