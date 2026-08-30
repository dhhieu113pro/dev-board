using System;
using System.IO;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

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

        [Fact]
        public void TerminalCountTracksOpenCloseAndCloseAll()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                Assert.Equal(0, spaces.TerminalCount);

                var first = spaces.CreateTerminal();
                var second = spaces.CreateTerminal();
                Assert.Equal(2, spaces.TerminalCount);

                spaces.CloseTerminal(first);
                Assert.Equal(1, spaces.TerminalCount);

                spaces.StopAll();
                Assert.Equal(0, spaces.TerminalCount);
                Assert.DoesNotContain(second, spaces.Sessions);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [AvaloniaFact]
        public void OpenTerminalTabsRemainVisibleFromDashboard()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.CreateTerminal();
                spaces.ActivateDashboard();

                using var view = new Views.DevSpaces { DataContext = spaces };
                var sessionTabs = view.GetVisualDescendants()
                    .OfType<ItemsControl>()
                    .Single(x => ReferenceEquals(x.ItemsSource, spaces.Sessions));

                Assert.True(sessionTabs.IsVisible);
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
