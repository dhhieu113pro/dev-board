using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class RoslynUnusedCodeServiceTests
    {
        [Fact]
        public async Task InitializeAsync_AnalyzesUnusedCodeAfterWorkspaceLoads()
        {
            using var dir = new TempDirectory();
            File.WriteAllText(Path.Combine(dir.Path, "workspace.csproj"), "<Project />");
            var expected = new RoslynUnusedCodeItem(
                "Demo", RoslynUnusedCodeKind.Member, "UnusedMethod", "IDE0051",
                "Private member is unused", Path.Combine(dir.Path, "Demo.cs"), 12, 5);
            var loaded = new FakeLoadedWorkspace(expected);
            using var service = new RoslynDevSpaceService(dir.Path, new FakeLoader(loaded));

            await service.InitializeAsync();

            Assert.Equal(RoslynDevSpaceState.Available, service.State);
            Assert.Equal(1, service.UnusedCodeCount);
            Assert.Same(expected, Assert.Single(service.UnusedCode));
            Assert.Equal(1, loaded.AnalysisCallCount);
        }

        [Fact]
        public async Task RefreshUnusedCodeAsync_ReloadsWorkspaceBeforeAnalyzing()
        {
            using var dir = new TempDirectory();
            File.WriteAllText(Path.Combine(dir.Path, "workspace.csproj"), "<Project />");
            var firstItem = new RoslynUnusedCodeItem(
                "Demo", RoslynUnusedCodeKind.Member, "OldUnused", "IDE0051",
                "Old unused member", Path.Combine(dir.Path, "Demo.cs"), 5, 3);
            var refreshedItem = new RoslynUnusedCodeItem(
                "Demo", RoslynUnusedCodeKind.Variable, "newUnused", "CS0168",
                "New unused variable", Path.Combine(dir.Path, "Demo.cs"), 9, 7);
            var firstWorkspace = new FakeLoadedWorkspace(firstItem);
            var refreshedWorkspace = new FakeLoadedWorkspace(refreshedItem);
            var loader = new FakeLoader(firstWorkspace, refreshedWorkspace);
            using var service = new RoslynDevSpaceService(dir.Path, loader);

            await service.InitializeAsync();
            await service.RefreshUnusedCodeAsync();

            Assert.Equal(2, loader.CallCount);
            Assert.True(firstWorkspace.IsDisposed);
            Assert.Equal(1, firstWorkspace.AnalysisCallCount);
            Assert.Equal(1, refreshedWorkspace.AnalysisCallCount);
            Assert.Same(refreshedItem, Assert.Single(service.UnusedCode));
        }

        private sealed class FakeLoader : IRoslynWorkspaceLoader
        {
            private readonly Queue<IRoslynLoadedWorkspace> _workspaces = new();
            public int CallCount { get; private set; }

            public FakeLoader(params IRoslynLoadedWorkspace[] workspaces)
            {
                foreach (var workspace in workspaces)
                    _workspaces.Enqueue(workspace);
            }

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                CallCount++;
                if (_workspaces.Count == 0)
                    throw new InvalidOperationException("No fake Roslyn workspace remains.");
                return Task.FromResult(_workspaces.Dequeue());
            }
        }

        private sealed class FakeLoadedWorkspace : IRoslynLoadedWorkspace
        {
            private readonly IReadOnlyList<RoslynUnusedCodeItem> _items;
            public int ProjectCount => 1;
            public int AnalysisCallCount { get; private set; }
            public bool IsDisposed { get; private set; }

            public FakeLoadedWorkspace(params RoslynUnusedCodeItem[] items) => _items = items;

            public Task<IReadOnlyList<RoslynUnusedCodeItem>> FindUnusedCodeAsync(CancellationToken cancellationToken)
            {
                AnalysisCallCount++;
                return Task.FromResult(_items);
            }

            public void Dispose() => IsDisposed = true;
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"devboard-unused-service-{Guid.NewGuid():N}");

            public TempDirectory() => Directory.CreateDirectory(Path);

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }
    }
}
