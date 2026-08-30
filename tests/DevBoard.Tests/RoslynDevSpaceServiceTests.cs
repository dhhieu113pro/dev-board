using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class RoslynDevSpaceServiceTests
    {
        [Fact]
        public void StartsUnavailable()
        {
            using var dir = new TempDirectory();
            using var service = new RoslynDevSpaceService(dir.Path, new FakeLoader());

            Assert.Equal(RoslynDevSpaceState.Unavailable, service.State);
            Assert.Equal(string.Empty, service.FailureReason);
        }

        [Fact]
        public async Task InitializeAsync_Success_TransitionsToAvailable()
        {
            using var dir = CreateWorkspaceDirectory();
            var loader = new FakeLoader(new FakeLoadedWorkspace(1));
            using var service = new RoslynDevSpaceService(dir.Path, loader);
            var states = new List<RoslynDevSpaceState>();
            service.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(service.State))
                    states.Add(service.State);
            };

            await service.InitializeAsync();

            Assert.Contains(RoslynDevSpaceState.Initializing, states);
            Assert.Equal(RoslynDevSpaceState.Available, service.State);
            Assert.Equal(1, loader.CallCount);
        }

        [Fact]
        public async Task InitializeAsync_LoaderFailure_TransitionsToFailedWithReason()
        {
            using var dir = CreateWorkspaceDirectory();
            var loader = new FakeLoader(new InvalidOperationException("MSBuild unavailable"));
            using var service = new RoslynDevSpaceService(dir.Path, loader);

            await service.InitializeAsync();

            Assert.Equal(RoslynDevSpaceState.Failed, service.State);
            Assert.Equal("MSBuild unavailable", service.FailureReason);
        }

        [Fact]
        public async Task InitializeAsync_ZeroProjects_TransitionsToFailed()
        {
            using var dir = CreateWorkspaceDirectory();
            using var service = new RoslynDevSpaceService(dir.Path, new FakeLoader(new FakeLoadedWorkspace(0)));

            await service.InitializeAsync();

            Assert.Equal(RoslynDevSpaceState.Failed, service.State);
            Assert.Contains("no projects", service.FailureReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Retry_AfterFailure_CanBecomeAvailable()
        {
            using var dir = CreateWorkspaceDirectory();
            var loader = new FakeLoader(
                new InvalidOperationException("first failure"),
                new FakeLoadedWorkspace(1));
            using var service = new RoslynDevSpaceService(dir.Path, loader);

            await service.InitializeAsync();
            Assert.Equal(RoslynDevSpaceState.Failed, service.State);

            await service.InitializeAsync();

            Assert.Equal(RoslynDevSpaceState.Available, service.State);
            Assert.Equal(string.Empty, service.FailureReason);
            Assert.Equal(2, loader.CallCount);
        }

        [Fact]
        public async Task ConcurrentInitialization_UsesSingleInFlightLoad()
        {
            using var dir = CreateWorkspaceDirectory();
            var loader = new BlockingFakeLoader();
            using var service = new RoslynDevSpaceService(dir.Path, loader);

            var first = service.InitializeAsync();
            var second = service.InitializeAsync();

            Assert.Same(first, second);
            Assert.Equal(1, loader.CallCount);
            loader.Complete(new FakeLoadedWorkspace(1));
            await Task.WhenAll(first, second);
            Assert.Equal(RoslynDevSpaceState.Available, service.State);
        }

        [Fact]
        public async Task InitializeAsync_NoWorkspace_TransitionsToFailedWithoutCallingLoader()
        {
            using var dir = new TempDirectory();
            var loader = new FakeLoader(new FakeLoadedWorkspace(1));
            using var service = new RoslynDevSpaceService(dir.Path, loader);

            await service.InitializeAsync();

            Assert.Equal(RoslynDevSpaceState.Failed, service.State);
            Assert.Contains("No .slnx, .sln, or .csproj", service.FailureReason, StringComparison.Ordinal);
            Assert.Equal(0, loader.CallCount);
        }

        private static TempDirectory CreateWorkspaceDirectory()
        {
            var dir = new TempDirectory();
            File.WriteAllText(Path.Combine(dir.Path, "workspace.csproj"), "<Project />");
            return dir;
        }

        private sealed class FakeLoader : IRoslynWorkspaceLoader
        {
            private readonly Queue<object> _results = new();
            public int CallCount { get; private set; }

            public FakeLoader(params object[] results)
            {
                foreach (var result in results)
                    _results.Enqueue(result);
            }

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                CallCount++;
                if (_results.Count == 0)
                    return Task.FromResult<IRoslynLoadedWorkspace>(new FakeLoadedWorkspace(1));

                var result = _results.Dequeue();
                if (result is Exception exception)
                    return Task.FromException<IRoslynLoadedWorkspace>(exception);
                return Task.FromResult((IRoslynLoadedWorkspace)result);
            }
        }

        private sealed class BlockingFakeLoader : IRoslynWorkspaceLoader
        {
            private readonly TaskCompletionSource<IRoslynLoadedWorkspace> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int CallCount { get; private set; }

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                CallCount++;
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

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devboard-roslyn-service-{Guid.NewGuid():N}");
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }
    }
}
