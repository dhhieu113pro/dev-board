using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
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
    public void TreeGuideChildStem_follows_expansion_state()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);
        folder.Children.Add(new DevSpaceFileNode("Demo.cs", "src/Demo.cs", false, 1));

        Assert.False(folder.ShowChildGuideStem);

        folder.IsExpanded = true;

        Assert.True(folder.ShowChildGuideStem);
    }

    [AvaloniaFact]
    public async Task TreeGuides_preserve_ancestor_continuations_and_stop_at_last_sibling()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"devboard-guides-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "Controllers"));
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src", "Services"));
        File.WriteAllText(Path.Combine(repositoryPath, "src", "Controllers", "Alpha.cs"), "class Alpha { }");
        File.WriteAllText(Path.Combine(repositoryPath, "src", "Controllers", "Beta.cs"), "class Beta { }");
        File.WriteAllText(Path.Combine(repositoryPath, "src", "Services", "Worker.cs"), "class Worker { }");

        try
        {
            RunGit(repositoryPath, "init");
            RunGit(repositoryPath, "add .");
            RunGit(repositoryPath, "-c user.name=DevBoardTests -c user.email=devboard@example.invalid commit -m initial");

            var files = new DevSpaceFiles(repositoryPath);
            await files.RefreshAsync();

            var src = Assert.Single(files.VisibleItems);
            var controllers = Assert.Single(src.Children, x => x.Name == "Controllers");
            var services = Assert.Single(src.Children, x => x.Name == "Services");
            var alpha = Assert.Single(controllers.Children, x => x.Name == "Alpha.cs");
            var beta = Assert.Single(controllers.Children, x => x.Name == "Beta.cs");

            Assert.Empty(src.TreeGuideSegments);

            var controllersGuide = Assert.Single(controllers.TreeGuideSegments);
            Assert.True(controllersGuide.ShowTop);
            Assert.True(controllersGuide.ShowBottom);
            Assert.True(controllersGuide.ShowHorizontal);

            var servicesGuide = Assert.Single(services.TreeGuideSegments);
            Assert.True(servicesGuide.ShowTop);
            Assert.False(servicesGuide.ShowBottom);
            Assert.True(servicesGuide.ShowHorizontal);

            Assert.Equal(2, alpha.TreeGuideSegments.Count);
            Assert.True(alpha.TreeGuideSegments[0].ShowTop);
            Assert.True(alpha.TreeGuideSegments[0].ShowBottom);
            Assert.False(alpha.TreeGuideSegments[0].ShowHorizontal);
            Assert.True(alpha.TreeGuideSegments[1].ShowBottom);
            Assert.True(alpha.TreeGuideSegments[1].ShowHorizontal);

            Assert.Equal(2, beta.TreeGuideSegments.Count);
            Assert.True(beta.TreeGuideSegments[0].ShowTop);
            Assert.True(beta.TreeGuideSegments[0].ShowBottom);
            Assert.False(beta.TreeGuideSegments[0].ShowHorizontal);
            Assert.False(beta.TreeGuideSegments[1].ShowBottom);
            Assert.True(beta.TreeGuideSegments[1].ShowHorizontal);
        }
        finally
        {
            try { Directory.Delete(repositoryPath, true); } catch { }
        }
    }

    [AvaloniaFact]
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
