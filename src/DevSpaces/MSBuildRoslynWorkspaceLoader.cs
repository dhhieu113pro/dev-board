using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevBoard.DevSpaces
{
    public sealed class MSBuildRoslynWorkspaceLoader : IRoslynWorkspaceLoader
    {
        public async Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                throw new ArgumentException("A Roslyn workspace path is required.", nameof(workspacePath));
            if (!File.Exists(workspacePath))
                throw new FileNotFoundException("The selected Roslyn workspace does not exist.", workspacePath);

            EnsureMSBuildRegistered();

            var workspace = MSBuildWorkspace.Create();
            try
            {
                Solution solution;
                var extension = Path.GetExtension(workspacePath);
                if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    var project = await workspace.OpenProjectAsync(workspacePath, cancellationToken: cancellationToken);
                    solution = project.Solution;
                }
                else if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    solution = await workspace.OpenSolutionAsync(workspacePath, cancellationToken: cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported Roslyn workspace type '{extension}'.");
                }

                return new LoadedWorkspace(workspace, solution);
            }
            catch (OperationCanceledException)
            {
                workspace.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                workspace.Dispose();
                throw new InvalidOperationException($"Roslyn could not load '{Path.GetFileName(workspacePath)}': {ex.Message}", ex);
            }
        }

        private static void EnsureMSBuildRegistered()
        {
            lock (RegistrationGate)
            {
                if (MSBuildLocator.IsRegistered)
                    return;

                var instance = MSBuildLocator.QueryVisualStudioInstances()
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault();
                if (instance == null)
                    throw new InvalidOperationException("No compatible .NET SDK/MSBuild installation was found.");

                MSBuildLocator.RegisterInstance(instance);
            }
        }

        public sealed class LoadedWorkspace : IRoslynLoadedWorkspace
        {
            public MSBuildWorkspace Workspace { get; }
            public Solution Solution { get; }
            public int ProjectCount => Solution.ProjectIds.Count;

            internal LoadedWorkspace(MSBuildWorkspace workspace, Solution solution)
            {
                Workspace = workspace;
                Solution = solution;
            }

            public void Dispose() => Workspace.Dispose();
        }

        private static readonly object RegistrationGate = new();
    }
}
