using SourceGit.DevSpaces.Roslyn;

namespace SourceGit.Tests;

public sealed class RoslynWorkspaceDiscoveryTests
{
    [Fact]
    public void FindCandidates_prefers_root_slnx_over_sln_and_csproj()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.CreateFile("App.slnx");
        workspace.CreateFile("App.sln");
        workspace.CreateFile("App.csproj");

        var candidates = RoslynWorkspaceDiscovery.FindCandidates(workspace.Path);

        Assert.Equal("App.slnx", System.IO.Path.GetFileName(candidates[0]));
    }

    [Fact]
    public void FindCandidates_ignores_generated_directories()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.CreateFile("src/App/App.csproj");
        workspace.CreateFile("bin/Generated.csproj");
        workspace.CreateFile("obj/Generated.csproj");
        workspace.CreateFile("node_modules/Generated.csproj");
        workspace.CreateFile(".git/Generated.csproj");

        var candidates = RoslynWorkspaceDiscovery.FindCandidates(workspace.Path);

        var candidate = Assert.Single(candidates);
        Assert.EndsWith(System.IO.Path.Combine("src", "App", "App.csproj"), candidate);
    }

    [Fact]
    public void FindCandidates_orders_solution_before_projects()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.CreateFile("src/Lib/Lib.csproj");
        workspace.CreateFile("Workspace.sln");

        var candidates = RoslynWorkspaceDiscovery.FindCandidates(workspace.Path);

        Assert.Equal("Workspace.sln", System.IO.Path.GetFileName(candidates[0]));
        Assert.Equal("Lib.csproj", System.IO.Path.GetFileName(candidates[1]));
    }

    [Fact]
    public void FindCandidates_returns_empty_for_missing_workspace_root()
    {
        var missingPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"missing-sourcegit-roslyn-{Guid.NewGuid():N}");

        var candidates = RoslynWorkspaceDiscovery.FindCandidates(missingPath);

        Assert.Empty(candidates);
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sourcegit-roslyn-{Guid.NewGuid():N}");

        public TemporaryWorkspace()
        {
            Directory.CreateDirectory(Path);
        }

        public void CreateFile(string relativePath)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, string.Empty);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
