using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceFileExplorerTestsFilteredGuides
{
    [Fact]
    public void Visible_child_keeps_collapsed_parent_guide_stem_connected()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);
        var child = new DevSpaceFileNode("Demo.cs", "src/Demo.cs", false, 1);
        folder.Children.Add(child);

        Assert.False(folder.IsExpanded);

        DevSpaceFiles.UpdateTreeGuideVisibility([folder, child]);

        Assert.True(folder.ShowChildGuideStem);
    }
}
