using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.DevSpaces
{
    public interface IRoslynLoadedWorkspace : IDisposable
    {
        int ProjectCount { get; }
    }

    public interface IRoslynWorkspaceLoader
    {
        Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken);
    }
}
