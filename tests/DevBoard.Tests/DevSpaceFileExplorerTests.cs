using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceFileExplorerTests
{
    [Fact]
    public void SelectFolder_exposes_direct_children_folders_first()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);
        folder.Children.Add(new DevSpaceFileNode("zeta.cs", "src/zeta.cs", false, 1));
        folder.Children.Add(new DevSpaceFileNode("Components", "src/Components", true, 1));
        folder.Children.Add(new DevSpaceFileNode("alpha.json", "src/alpha.json", false, 1));
        folder.Children.Add(new DevSpaceFileNode("Assets", "src/Assets", true, 1));

        var children = DevSpaceFiles.GetFolderChildren(folder).ToArray();

        Assert.Equal(new[] { "Assets", "Components", "alpha.json", "zeta.cs" }, children.Select(x => x.Name));
    }

    [Fact]
    public void ToggleFolderSelection_toggles_folder_state()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);

        DevSpaceFiles.ToggleFolderSelection(folder);
        Assert.True(folder.IsExpanded);

        DevSpaceFiles.ToggleFolderSelection(folder);
        Assert.False(folder.IsExpanded);
    }

    [Fact]
    public void ExpansionGlyph_reflects_folder_state()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);

        Assert.Equal("›", folder.ExpansionGlyph);

        folder.IsExpanded = true;

        Assert.Equal("⌄", folder.ExpansionGlyph);
    }

    [Fact]
    public async Task OpenFile_requests_reveal_every_time_and_expands_parent_folders()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"devboard-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "nested"));
        File.WriteAllText(Path.Combine(repositoryPath, "src", "nested", "Demo.cs"), "class Demo { }");

        try
        {
            RunGit(repositoryPath, "init");
            RunGit(repositoryPath, "add .");
            RunGit(repositoryPath, "-c user.name=DevBoardTests -c user.email=devboard@example.invalid commit -m initial");

            var files = new DevSpaceFiles(repositoryPath);
            await files.RefreshAsync();
            var revealPaths = new List<string>();
            files.RevealRequested += node => revealPaths.Add(node.RelativePath);

            Assert.True(files.OpenFile("src/nested/Demo.cs"));
            Assert.Equal("src/nested/Demo.cs", files.SelectedNode.RelativePath);
            Assert.Equal(new[] { "src", "src/nested", "src/nested/Demo.cs" }, files.VisibleItems.Select(x => x.RelativePath));

            Assert.True(files.OpenFile("src/nested/Demo.cs"));
            Assert.Equal(new[] { "src/nested/Demo.cs", "src/nested/Demo.cs" }, revealPaths);
        }
        finally
        {
            try { Directory.Delete(repositoryPath, true); } catch { }
        }
    }

    private static void RunGit(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        Assert.NotNull(process);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
