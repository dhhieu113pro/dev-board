using System;
using System.IO;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DevBoard.DevSpaces;
using DevBoard.ViewModels;
using DevBoard.Views;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceDashboardProfileDisplayTests
{
    [AvaloniaFact]
    public void QuickStartProfileUsesIconAwareDisplayName()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-profile-display-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var profile = new DevSpaceTerminalProfile
        {
            Name = "UpTime UI",
            Icon = "🦄",
        };
        var profiles = DevSpaceProfileSettings.Instance.Profiles;
        profiles.Add(profile);

        try
        {
            using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
            var view = new Views.DevSpaceDashboard
            {
                DataContext = spaces.Dashboard,
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

            window.Close();
        }
        finally
        {
            profiles.Remove(profile);
            Directory.Delete(root, true);
        }
    }

    private sealed class FakeLauncher : IDevSpaceSessionLauncher
    {
        public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null)
        {
            return new DevSpaceLaunchSpec(terminal ?? string.Empty, [], workingDirectory);
        }
    }
}
