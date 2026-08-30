using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class RoslynBackgroundInitializationTests
    {
        [Fact]
        public async Task DashboardStartsRoslynInitializationWithoutBlockingConstruction()
        {
            var root = CreateTempDirectory();
            try
            {
                // Create the owner before the .NET project so its own dashboard does not
                // participate in this focused fake-loader test.
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                File.WriteAllText(Path.Combine(root, "workspace.csproj"), "<Project />");

                var loader = new BlockingRoslynLoader();
                using var dashboard = new DevSpaceDashboard(spaces, root, null, loader);

                // Construction already returned even though the fake loader cannot finish.
                // Automatic initialization must reach that loader without a manual click.
                await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, loader.CallCount);
                Assert.Equal(RoslynDevSpaceState.Initializing, dashboard.RoslynState);
                Assert.False(dashboard.CanInitializeRoslyn);

                loader.Complete(new FakeLoadedWorkspace(1));
                await dashboard.InitializeRoslynAsync();

                Assert.Equal(RoslynDevSpaceState.Available, dashboard.RoslynState);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-roslyn-background-{Guid.NewGuid():N}");
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

        private sealed class BlockingRoslynLoader : IRoslynWorkspaceLoader
        {
            private readonly TaskCompletionSource<IRoslynLoadedWorkspace> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int CallCount { get; private set; }

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                CallCount++;
                Started.TrySetResult();
                return _completion.Task;
            }

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
