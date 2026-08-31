using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;

using DevBoard.DevSpaces;
using DevBoard.DevSpaces.Terminal;

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
        public void TerminalTabsAreOnlyVisibleOnTerminalsPage()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.CreateTerminal();
                spaces.ActivateDashboard();

                using var view = new Views.DevSpaces { DataContext = spaces };
                var sessionTabs = view.GetLogicalDescendants()
                    .OfType<ItemsControl>()
                    .Single(x => ReferenceEquals(x.ItemsSource, spaces.Sessions));

                Assert.False(sessionTabs.IsVisible);

                spaces.ActivateTerminals();

                Assert.True(sessionTabs.IsVisible);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [AvaloniaFact]
        public void AgentTerminalTabsRenderOriginalLogos()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var sessions = new[]
                {
                    spaces.CreateTerminalAt(-1, "pwsh", "Copilot", root, "copilot"),
                    spaces.CreateTerminalAt(-1, "pwsh", "Codex", root, "codex"),
                    spaces.CreateTerminalAt(-1, "pwsh", "Antigravity", root, "agy"),
                };

                using var view = new Views.DevSpaces { DataContext = spaces };
                var sessionTabs = view.GetLogicalDescendants()
                    .OfType<ItemsControl>()
                    .Single(x => ReferenceEquals(x.ItemsSource, spaces.Sessions));
                var template = Assert.IsAssignableFrom<IDataTemplate>(sessionTabs.ItemTemplate);

                foreach (var session in sessions)
                {
                    var tab = template.Build(session);
                    Assert.NotNull(tab);
                    tab.DataContext = session;

                    var icon = Assert.Single(tab.GetLogicalDescendants().OfType<Image>());
                    Assert.NotNull(icon.Source);
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void NativeTerminalSurfaceExposesPersistentVerticalScrollbar()
        {
            var surfaceType = typeof(IDevSpaceSessionLauncher).Assembly
                .GetType("DevBoard.DevSpaces.WindowsTerminalDevSpaceSurface");
            Assert.NotNull(surfaceType);

            var constructor = Assert.Single(surfaceType.GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic));
            using var surface = Assert.IsAssignableFrom<IDisposable>(
                constructor.Invoke([new TerminalTranscriptStore()]));
            var viewProperty = surfaceType.GetProperty("View", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(viewProperty);
            var surfaceView = Assert.IsAssignableFrom<Control>(viewProperty.GetValue(surface));

            var scrollbar = Assert.Single(surfaceView.GetLogicalDescendants().OfType<ScrollBar>());
            Assert.Equal(Orientation.Vertical, scrollbar.Orientation);
            Assert.False(scrollbar.AllowAutoHide);
            Assert.True(scrollbar.IsVisible);
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
