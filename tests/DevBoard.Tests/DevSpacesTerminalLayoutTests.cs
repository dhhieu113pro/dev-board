using System.IO;

using DevBoard.DevSpaces;
using DevBoard.Models;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class DevSpacesTerminalLayoutTests
    {
        [Fact]
        public void AutoLayoutPlacesSessionsInGrid()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Layout = DevSpaceLayout.Auto;
                spaces.CreateTerminal();
                spaces.CreateTerminal();
                spaces.CreateTerminal();

                Assert.Equal(2, spaces.GridRows);
                Assert.Equal(2, spaces.GridColumns);
                Assert.Equal(4, spaces.VisibleSlots.Count);
                Assert.Equal(3, spaces.VisibleSlots.Count(slot => slot.Terminal != null));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void SelectedGridLayoutKeepsSessions()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Layout = DevSpaceLayout.TwoByTwo;
                var first = spaces.CreateTerminal();
                var second = spaces.CreateTerminal();

                Assert.Equal(DevSpaceLayout.TwoByTwo, spaces.Layout);
                Assert.Equal(2, spaces.GridRows);
                Assert.Equal(2, spaces.GridColumns);
                Assert.Equal(2, spaces.Sessions.Count);
                Assert.Same(first, spaces.Sessions[0]);
                Assert.Same(second, spaces.Sessions[1]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-terminal-layout-{System.Guid.NewGuid():N}");
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
