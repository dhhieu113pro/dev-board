using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class RoslynUiResponsivenessTests
    {
        [Fact]
        public async Task InitializeAsync_RoslynWorkDoesNotRunOnCallerSynchronizationContext()
        {
            using var dir = CreateWorkspaceDirectory();
            var loaded = new ContextCapturingWorkspace();
            var loader = new ContextCapturingLoader(loaded);
            using var service = new RoslynDevSpaceService(dir.Path, loader);
            var callerContext = new InlineSynchronizationContext();
            var previous = SynchronizationContext.Current;

            SynchronizationContext.SetSynchronizationContext(callerContext);
            try
            {
                await service.InitializeAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            Assert.NotSame(callerContext, loader.ObservedContext);
            Assert.NotSame(callerContext, loaded.ObservedContext);
        }

        [Fact]
        public async Task RefreshUnusedCodeAsync_RoslynWorkDoesNotRunOnCallerSynchronizationContext()
        {
            using var dir = CreateWorkspaceDirectory();
            var initial = new ContextCapturingWorkspace();
            var refreshed = new ContextCapturingWorkspace();
            var loader = new ContextCapturingLoader(initial, refreshed);
            using var service = new RoslynDevSpaceService(dir.Path, loader);

            await service.InitializeAsync();
            loader.ResetObservedContext();
            refreshed.ResetObservedContext();

            var callerContext = new InlineSynchronizationContext();
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(callerContext);
            try
            {
                await service.RefreshUnusedCodeAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            Assert.NotSame(callerContext, loader.ObservedContext);
            Assert.NotSame(callerContext, refreshed.ObservedContext);
        }

        private static TempDirectory CreateWorkspaceDirectory()
        {
            var dir = new TempDirectory();
            File.WriteAllText(Path.Combine(dir.Path, "workspace.csproj"), "<Project />");
            return dir;
        }

        private sealed class InlineSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    d(state);
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            }
        }

        private sealed class ContextCapturingLoader : IRoslynWorkspaceLoader
        {
            private readonly Queue<IRoslynLoadedWorkspace> _workspaces = new();

            public SynchronizationContext ObservedContext { get; private set; }

            public ContextCapturingLoader(params IRoslynLoadedWorkspace[] workspaces)
            {
                foreach (var workspace in workspaces)
                    _workspaces.Enqueue(workspace);
            }

            public Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
            {
                ObservedContext = SynchronizationContext.Current;
                return Task.FromResult(_workspaces.Dequeue());
            }

            public void ResetObservedContext() => ObservedContext = null;
        }

        private sealed class ContextCapturingWorkspace : IRoslynLoadedWorkspace
        {
            public int ProjectCount => 1;
            public SynchronizationContext ObservedContext { get; private set; }

            public Task<IReadOnlyList<RoslynUnusedCodeItem>> FindUnusedCodeAsync(CancellationToken cancellationToken)
            {
                ObservedContext = SynchronizationContext.Current;
                return Task.FromResult<IReadOnlyList<RoslynUnusedCodeItem>>(Array.Empty<RoslynUnusedCodeItem>());
            }

            public void ResetObservedContext() => ObservedContext = null;

            public void Dispose()
            {
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devboard-roslyn-ui-{Guid.NewGuid():N}");
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
