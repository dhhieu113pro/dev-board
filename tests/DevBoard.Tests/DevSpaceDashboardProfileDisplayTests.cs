using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
        var view = new DevSpaceDashboard();

        var profileButton = FindProfileButton(view, profile);

        Assert.Equal("🦄 UpTime UI", profileButton.Content);
    }

    private static Button FindProfileButton(DevSpaceDashboard view, DevSpaceTerminalProfile profile)
    {
        view.DataContext = new ProfileDisplayTestDataContext(profile);
        view.ApplyTemplate();

        foreach (var control in view.GetVisualDescendants())
        {
            if (control is Button { DataContext: DevSpaceTerminalProfile })
                return (Button)control;
        }

        throw new Xunit.Sdk.XunitException("Dashboard profile Quick Start button was not rendered.");
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
