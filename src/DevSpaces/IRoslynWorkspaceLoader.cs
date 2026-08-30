using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.DevSpaces
{
    public interface IRoslynLoadedWorkspace : IDisposable
    {
        int ProjectCount { get; }
        Task<IReadOnlyList<RoslynUnusedCodeItem>> FindUnusedCodeAsync(CancellationToken cancellationToken);
    }

    public interface IRoslynWorkspaceLoader
    {
        Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken);
    }
}
