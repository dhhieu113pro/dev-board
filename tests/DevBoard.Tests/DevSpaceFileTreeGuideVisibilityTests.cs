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

    [Fact]
    public void Filtered_tree_stops_guide_at_last_visible_sibling()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);
        var alpha = new DevSpaceFileNode("Alpha.cs", "src/Alpha.cs", false, 1);
        var beta = new DevSpaceFileNode("Beta.cs", "src/Beta.cs", false, 1);
        folder.Children.Add(alpha);
        folder.Children.Add(beta);

        DevSpaceFiles.UpdateTreeGuideVisibility([folder, alpha]);

        var guide = Assert.Single(alpha.TreeGuideSegments);
        Assert.False(guide.ShowBottom);
    }

    [Fact]
    public void Filtered_tree_hides_expanded_parent_stem_when_no_child_is_visible()
    {
        var folder = new DevSpaceFileNode("src", "src", true, 0);
        folder.Children.Add(new DevSpaceFileNode("Demo.cs", "src/Demo.cs", false, 1));
        folder.IsExpanded = true;

        DevSpaceFiles.UpdateTreeGuideVisibility([folder]);

        Assert.False(folder.ShowChildGuideStem);
    }
}
