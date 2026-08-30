using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class RoslynManualInitializationTests
    {
        [Fact]
        public async Task Dashboard_DoesNotLoadRoslynUntilInitializeIsRequested()
        {
            var ownerRoot = CreateTempDirectory();
            var workspaceRoot = CreateTempDirectory();
            File.WriteAllText(Path.Combine(workspaceRoot, "workspace.csproj"), "<Project />");

            try
            {
                using var spaces = new ViewModels.DevSpaces(ownerRoot, new FakeLauncher());
                var loader = new CountingRoslynLoader();
                using var dashboard = new DevSpaceDashboard(spaces, workspaceRoot, null, loader);

                var completed = await Task.WhenAny(loader.Started, Task.Delay(TimeSpan.FromSeconds(1)));

                Assert.NotSame(loader.Started, completed);
                Assert.Equal(0, loader.CallCount);
                Assert.Equal(RoslynDevSpaceState.Unavailable, dashboard.RoslynState);
                Assert.Contains("Click Initialize", dashboard.RoslynSummaryText, StringComparison.Ordinal);

                await dashboard.InitializeRoslynAsync();

                Assert.Equal(1, loader.CallCount);
                Assert.Equal(RoslynDevSpaceState.Available, dashboard.RoslynState);
            }
            finally
            {
                Directory.Delete(ownerRoot, true);
                Directory.Delete(workspaceRoot, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-roslyn-manual-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeLauncher : IDevSpaceSessionLauncher
        {
            public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null) =>
                new(terminal ?? string.Empty, [], workingDirectory);
        }

        private sealed class CountingRoslynLoader : IRoslynWorkspaceLoader
        {
            private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int CallCount { get; private set; }
            public Task Started => _started.Task;

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                CallCount++;
                _started.TrySetResult();
                return Task.FromResult<IRoslynLoadedWorkspace>(new FakeLoadedWorkspace());
            }
        }

        private sealed class FakeLoadedWorkspace : IRoslynLoadedWorkspace
        {
            public int ProjectCount => 1;

            public Task<IReadOnlyList<RoslynUnusedCodeItem>> FindUnusedCodeAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<RoslynUnusedCodeItem>>(Array.Empty<RoslynUnusedCodeItem>());

            public void Dispose()
            {
            }
        }
    }
}
