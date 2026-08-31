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

    [Fact]
    public void TreeGuides_preserve_ancestor_continuations_and_stop_at_last_sibling()
    {
        var src = new DevSpaceFileNode("src", "src", true, 0);
        var controllers = new DevSpaceFileNode("Controllers", "src/Controllers", true, 1);
        var services = new DevSpaceFileNode("Services", "src/Services", true, 1);
        var alpha = new DevSpaceFileNode("Alpha.cs", "src/Controllers/Alpha.cs", false, 2);
        var beta = new DevSpaceFileNode("Beta.cs", "src/Controllers/Beta.cs", false, 2);

        src.Children.Add(controllers);
        src.Children.Add(services);
        controllers.Children.Add(alpha);
        controllers.Children.Add(beta);

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

    [Fact]
    public void WorkspaceFile_cancel_edit_discards_buffer_changes()
    {
        var file = new DevSpaceWorkspaceFile("src/Demo.cs", "class Demo { }");

        file.BeginEdit();
        file.EditableContent = "class Demo { public int Value { get; set; } }";
        file.CancelEdit();

        Assert.False(file.IsEditing);
        Assert.Equal("class Demo { }", file.Content);
        Assert.Equal("class Demo { }", file.EditableContent);
    }

    [AvaloniaFact]
    public async Task SaveSelectedFile_writes_edit_buffer_and_keeps_file_selected()
    {
        var repositoryPath = CreateRepositoryWithFile("src/Demo.cs", "class Demo { }");

        try
        {
            var files = new DevSpaceFiles(repositoryPath);
            await files.RefreshAsync();
            Assert.True(files.OpenFile("src/Demo.cs"));
            var file = await WaitForWorkspaceFileAsync(files);

            file.BeginEdit();
            file.EditableContent = "class Demo { public int Value { get; set; } }";
            await files.SaveSelectedFileAsync();

            Assert.Equal("class Demo { public int Value { get; set; } }", File.ReadAllText(Path.Combine(repositoryPath, "src", "Demo.cs")));
            Assert.Equal("src/Demo.cs", files.SelectedNode?.RelativePath);
            Assert.Same(file, files.DetailContext);
            Assert.False(file.IsEditing);
            Assert.Equal("class Demo { public int Value { get; set; } }", file.Content);
        }
        finally
        {
            try { Directory.Delete(repositoryPath, true); } catch { }
        }
    }

    [AvaloniaFact]
    public async Task DeleteSelectedFile_removes_file_and_clears_viewer_selection()
    {
        var repositoryPath = CreateRepositoryWithFile("src/Demo.cs", "class Demo { }");

        try
        {
            var files = new DevSpaceFiles(repositoryPath);
            await files.RefreshAsync();
            Assert.True(files.OpenFile("src/Demo.cs"));
            await WaitForWorkspaceFileAsync(files);

            await files.DeleteSelectedFileAsync();

            Assert.False(File.Exists(Path.Combine(repositoryPath, "src", "Demo.cs")));
            Assert.Null(files.SelectedNode);
            Assert.Null(files.DetailContext);
            Assert.False(files.IsLoading);
        }
        finally
        {
            try { Directory.Delete(repositoryPath, true); } catch { }
        }
    }

    [AvaloniaFact]
    [Trait("Category", "UIIntegration")]
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

    private static string CreateRepositoryWithFile(string relativePath, string content)
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"devboard-files-{Guid.NewGuid():N}");
        var absolutePath = Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, content);
        RunGit(repositoryPath, "init");
        RunGit(repositoryPath, "add .");
        RunGit(repositoryPath, "-c user.name=DevBoardTests -c user.email=devboard@example.invalid commit -m initial");
        return repositoryPath;
    }

    private static async Task<DevSpaceWorkspaceFile> WaitForWorkspaceFileAsync(DevSpaceFiles files)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (files.DetailContext is DevSpaceWorkspaceFile file)
                return file;

            await Task.Delay(10);
        }

        throw new TimeoutException("Workspace file detail did not load in time.");
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
