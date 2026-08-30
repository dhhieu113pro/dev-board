using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DevBoard.DevSpaces;
using DevBoard.Views;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceDashboardProfileDisplayTests
{
    [AvaloniaFact]
    public void QuickStartProfileUsesIconAwareDisplayName()
    {
        var profile = new DevSpaceTerminalProfile
        {
            Name = "UpTime UI",
            Icon = "🦄",
        };
        var view = new DevSpaceDashboard
        {
            DataContext = new ProfileDisplayTestDataContext(profile),
        };
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = view,
            SystemDecorations = SystemDecorations.None,
        };
        window.Show();
        window.UpdateLayout();

        var profileButton = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(x => ReferenceEquals(x.DataContext, profile));

        Assert.Equal("🦄 UpTime UI", profileButton.Content);
        Assert.Equal(1.0, profileButton.Opacity);

        window.Close();
    }

    private sealed class ProfileDisplayTestDataContext
    {
        public DevSpaceTerminalProfile[] Profiles { get; }

        public ProfileDisplayTestDataContext(DevSpaceTerminalProfile profile)
        {
            Profiles = [profile];
        }
    }
}
