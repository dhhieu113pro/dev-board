using System.Linq;
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
}
