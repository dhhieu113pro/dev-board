using System.IO;

using SourceGit.DevSpaces;
using SourceGit.Models;
using SourceGit.ViewModels;

using Xunit;

namespace SourceGit.Tests
{
    public sealed class DevSpacesDashboardTests
    {
        [Fact]
        public void DashboardIsDefaultAndNavigationUsesSinglePageState()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
                Assert.True(spaces.IsDashboardActive);
                Assert.False(spaces.IsFilesActive);
                Assert.False(spaces.IsTerminalsActive);
                Assert.False(spaces.IsRoslynActive);

                spaces.ActivateFiles();
                Assert.Equal(DevSpacePage.Files, spaces.ActivePage);
                Assert.True(spaces.IsFilesActive);

                spaces.ActivateTerminals();
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
                Assert.True(spaces.IsTerminalsActive);

                spaces.ActivateRoslyn();
                Assert.Equal(DevSpacePage.Roslyn, spaces.ActivePage);
                Assert.True(spaces.IsRoslynActive);

                spaces.ActivateDashboard();
                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void OpenFileSelectsFilesWithoutChangingSessions()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var before = spaces.Sessions.Count;

                var opened = spaces.OpenFile("missing-file.cs");

                Assert.False(opened);
                Assert.Equal(DevSpacePage.Files, spaces.ActivePage);
                Assert.Equal(before, spaces.Sessions.Count);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"sourcegit-dashboard-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeLauncher : IDevSpaceSessionLauncher
        {
            public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null)
            {
                return new DevSpaceLaunchSpec(terminal ?? string.Empty, [], workingDirectory);
            }
        }
    }
}
