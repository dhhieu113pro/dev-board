using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

using Xunit;

namespace DevBoard.Tests;

[Trait("Category", "UIIntegration")]
public sealed class DevSpacesPreferencesTests
{
    [AvaloniaFact]
    public void OpeningPreferencesAddsExactlyOneDevSpacesTab()
    {
        var view = new Views.Preferences();
        try
        {
            view.Show();

            var tabs = Assert.IsType<TabControl>(view.FindDescendantOfType<TabControl>());
            var devSpacesTabs = tabs.Items
                .OfType<TabItem>()
                .Where(x => x.Header as string == App.Text("DevSpaces"));
            Assert.Single(devSpacesTabs);
        }
        finally
        {
            view.Close();
        }
    }
}
