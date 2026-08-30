using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;
using DevBoard.Models;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
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
                spaces.ActivateTerminals();
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
                spaces.ActivateRoslyn();
                Assert.Equal(DevSpacePage.Roslyn, spaces.ActivePage);
                spaces.ActivateDashboard();
                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void AutomaticFirstSessionKeepsDashboardAsLandingPage()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

                spaces.EnsureFirstSession();

                Assert.Single(spaces.Sessions);
                Assert.NotNull(spaces.ActiveTerminal);
                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void GitSummaryCanNavigateToWorkingCopy()
        {
            var root = CreateTempDirectory();
            var gitDir = Path.Combine(root, ".git");
            Directory.CreateDirectory(gitDir);
            try
            {
                var repository = new Repository(false, root, gitDir);
                using var spaces = new ViewModels.DevSpaces(repository, root, new FakeLauncher());

                spaces.Dashboard.OpenWorkingCopy();

                Assert.Equal(1, repository.SelectedViewIndex);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task OpenFileSelectsFilesWithoutChangingSessions()
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
                await spaces.Files.InitialRefreshTask;
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void QuickStartAndSessionSelectionReuseExistingSessionObjects()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var created = spaces.Dashboard.StartDefaultTerminal();

                Assert.Single(spaces.Sessions);
                Assert.Same(created, spaces.ActiveTerminal);
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);

                spaces.ActivateDashboard();
                spaces.Dashboard.OpenSession(created);

                Assert.Single(spaces.Sessions);
                Assert.Same(created, spaces.ActiveTerminal);
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Theory]
        [InlineData("Codex", "codex")]
        [InlineData("Antigravity", "agy")]
        public void AgentQuickStartUsesBuiltInCommandMapping(string name, string command)
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var agent = DevSpaceAgent.BuiltIn.Single(x => x.Name == name);

                var created = spaces.Dashboard.StartAgent(agent);

                Assert.Equal(command, created.StartupCommand);
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void DashboardCanCloseSingleSessionThroughExistingLifecycle()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var first = spaces.Dashboard.StartDefaultTerminal();
                var second = spaces.Dashboard.StartDefaultTerminal();

                spaces.Dashboard.CloseSession(first);

                Assert.Single(spaces.Sessions);
                Assert.DoesNotContain(first, spaces.Sessions);
                Assert.Same(second, spaces.ActiveTerminal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void CloseAllDelegatesToExistingSessionLifecycle()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Dashboard.StartDefaultTerminal();
                spaces.Dashboard.StartDefaultTerminal();

                spaces.Dashboard.CloseAllSessions();

                Assert.Empty(spaces.Sessions);
                Assert.Null(spaces.ActiveTerminal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void DashboardActivityIsIsolatedByWorkspaceInstance()
        {
            var firstRoot = CreateTempDirectory();
            var secondRoot = CreateTempDirectory();
            try
            {
                using var first = new ViewModels.DevSpaces(firstRoot, new FakeLauncher());
                using var second = new ViewModels.DevSpaces(secondRoot, new FakeLauncher());

                first.Dashboard.AddActivity(DevSpaceActivityKind.FileOpened, "first.cs");

                Assert.Single(first.Dashboard.Activity);
                Assert.Empty(second.Dashboard.Activity);
            }
            finally
            {
                Directory.Delete(firstRoot, true);
                Directory.Delete(secondRoot, true);
            }
        }

        [Fact]
        public void ToolHealthFindsCommandsFromProvidedPathAndReturnsUnavailableOtherwise()
        {
            var root = CreateTempDirectory();
            try
            {
                var command = System.OperatingSystem.IsWindows() ? "dashboard-tool.cmd" : "dashboard-tool";
                File.WriteAllText(Path.Combine(root, command), string.Empty);

                Assert.Equal(DevSpaceCapabilityState.Available, DevSpaceToolHealth.CheckCommand("dashboard-tool", root));
                Assert.Equal(DevSpaceCapabilityState.Unavailable, DevSpaceToolHealth.CheckCommand("missing-dashboard-tool", root));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void RoslynDashboardStartsWithInitializeAction()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                using var dashboard = new DevSpaceDashboard(spaces, root, null, new FakeRoslynLoader(new FakeLoadedWorkspace(1)));

                Assert.Equal(RoslynDevSpaceState.Unavailable, dashboard.RoslynState);
                Assert.Equal("Unavailable", dashboard.RoslynStatusText);
                Assert.True(dashboard.CanInitializeRoslyn);
                Assert.False(dashboard.IsRoslynInitializing);
                Assert.Equal("Initialize", dashboard.RoslynActionText);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task RoslynDashboardFailedStateShowsRetryAndReason()
        {
            var root = CreateTempDirectory();
            File.WriteAllText(Path.Combine(root, "workspace.csproj"), "<Project />");
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var loader = new FakeRoslynLoader(new InvalidOperationException("MSBuild unavailable"), new FakeLoadedWorkspace(1));
                using var dashboard = new DevSpaceDashboard(spaces, root, null, loader);

                await dashboard.InitializeRoslynAsync();

                Assert.Equal(RoslynDevSpaceState.Failed, dashboard.RoslynState);
                Assert.Equal("Failed", dashboard.RoslynStatusText);
                Assert.Equal("MSBuild unavailable", dashboard.RoslynFailureReason);
                Assert.True(dashboard.CanInitializeRoslyn);
                Assert.Equal("Retry", dashboard.RoslynActionText);

                await dashboard.InitializeRoslynAsync();

                Assert.Equal(RoslynDevSpaceState.Available, dashboard.RoslynState);
                Assert.Equal("Available", dashboard.RoslynStatusText);
                Assert.False(dashboard.CanInitializeRoslyn);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task RoslynDashboardInitializingStateDisablesAction()
        {
            var root = CreateTempDirectory();
            File.WriteAllText(Path.Combine(root, "workspace.csproj"), "<Project />");
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var loader = new BlockingRoslynLoader();
                using var dashboard = new DevSpaceDashboard(spaces, root, null, loader);

                var initialization = dashboard.InitializeRoslynAsync();

                Assert.Equal(RoslynDevSpaceState.Initializing, dashboard.RoslynState);
                Assert.Equal("Initializing…", dashboard.RoslynStatusText);
                Assert.True(dashboard.IsRoslynInitializing);
                Assert.False(dashboard.CanInitializeRoslyn);

                loader.Complete(new FakeLoadedWorkspace(1));
                await initialization;
                Assert.Equal(RoslynDevSpaceState.Available, dashboard.RoslynState);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-dashboard-{Guid.NewGuid():N}");
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

        private sealed class FakeRoslynLoader : IRoslynWorkspaceLoader
        {
            private readonly Queue<object> _results = new();

            public FakeRoslynLoader(params object[] results)
            {
                foreach (var result in results)
                    _results.Enqueue(result);
            }

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                var result = _results.Dequeue();
                if (result is Exception exception)
                    return Task.FromException<IRoslynLoadedWorkspace>(exception);
                return Task.FromResult((IRoslynLoadedWorkspace)result);
            }
        }

        private sealed class BlockingRoslynLoader : IRoslynWorkspaceLoader
        {
            private readonly TaskCompletionSource<IRoslynLoadedWorkspace> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken) => _completion.Task;
            public void Complete(IRoslynLoadedWorkspace workspace) => _completion.TrySetResult(workspace);
        }

        private sealed class FakeLoadedWorkspace : IRoslynLoadedWorkspace
        {
            public int ProjectCount { get; }

            public FakeLoadedWorkspace(int projectCount)
            {
                ProjectCount = projectCount;
            }

            public void Dispose()
            {
            }
        }
    }
}
