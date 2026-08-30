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

        private sealed class FakeLoader : IRoslynWorkspaceLoader
        {
            private readonly IRoslynLoadedWorkspace _workspace;

            public FakeLoader(IRoslynLoadedWorkspace workspace) => _workspace = workspace;

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken) =>
                Task.FromResult(_workspace);
        }

        private sealed class FakeLoadedWorkspace : IRoslynLoadedWorkspace
        {
            private readonly IReadOnlyList<RoslynUnusedCodeItem> _items;
            public int ProjectCount => 1;
            public int AnalysisCallCount { get; private set; }

            public FakeLoadedWorkspace(params RoslynUnusedCodeItem[] items) => _items = items;

            public Task<IReadOnlyList<RoslynUnusedCodeItem>> FindUnusedCodeAsync(CancellationToken cancellationToken)
            {
                AnalysisCallCount++;
                return Task.FromResult(_items);
            }

            public void Dispose()
            {
            }
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
