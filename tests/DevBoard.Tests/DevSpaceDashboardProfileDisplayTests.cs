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
            var window = Show(view);

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

    [AvaloniaFact]
    public void DashboardUsesActiveTerminalsHeading()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-active-terminals-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
            var view = new Views.DevSpaceDashboard
            {
                DataContext = spaces.Dashboard,
            };
            var window = Show(view);

            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Active terminals");
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Active Spaces");

            window.Close();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [AvaloniaFact]
    public void DashboardDoesNotShowRoslynSummaryInQuickStart()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-no-roslyn-summary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
            var view = new Views.DevSpaceDashboard
            {
                DataContext = spaces.Dashboard,
            };
            var window = Show(view);

            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<TextBlock>(),
                x => x.Text == "Roslyn is not running. Click Initialize to analyze this workspace.");

            window.Close();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [AvaloniaFact]
    public void ActiveTerminalProfileIconUsesEmojiFontAndSeparateTitle()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-active-terminal-icon-{Guid.NewGuid():N}");
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
            spaces.CreateProfileTerminalAt(-1, profile);
            var view = new Views.DevSpaceDashboard
            {
                DataContext = spaces.Dashboard,
            };
            var window = Show(view);

            var icon = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(x => x.Text == "🦄");

            Assert.Contains("Segoe UI Emoji", icon.FontFamily.ToString());
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "UpTime UI 1");

            window.Close();
        }
        finally
        {
            profiles.Remove(profile);
            Directory.Delete(root, true);
        }
    }

    [AvaloniaFact]
    public void DashboardShowsSourceGitStatisticsAndLogsWithWeeklyDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-stats-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
            var view = new Views.DevSpaceDashboard
            {
                DataContext = spaces.Dashboard,
            };
            var window = Show(view);

            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Stats");
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Logs");
            Assert.Single(view.GetVisualDescendants().OfType<Chart>());
            Assert.Single(view.GetVisualDescendants().OfType<CommandLogContentPresenter>());

            var rangeSwitcher = view.GetVisualDescendants()
                .OfType<ListBox>()
                .Single(x => x.Name == "StatisticsRangeSwitcher");
            Assert.Equal(0, rangeSwitcher.SelectedIndex);

            var rangeLabels = rangeSwitcher.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(x => x.Text)
                .ToArray();
            Assert.Equal(new[] { "Weekly", "Monthly", "Total" }, rangeLabels);

            Assert.Equal(Models.StatisticsMode.ThisWeek, spaces.Dashboard.StatisticsViewMode);
            spaces.Dashboard.StatisticsRange = DevSpaceStatisticsRange.Monthly;
            Assert.Equal(Models.StatisticsMode.ThisMonth, spaces.Dashboard.StatisticsViewMode);
            spaces.Dashboard.StatisticsRange = DevSpaceStatisticsRange.Total;
            Assert.Equal(Models.StatisticsMode.All, spaces.Dashboard.StatisticsViewMode);

            window.Close();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Window Show(Control view)
    {
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = view,
            SystemDecorations = SystemDecorations.None,
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private sealed class FakeLauncher : IDevSpaceSessionLauncher
    {
        public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null)
        {
            return new DevSpaceLaunchSpec(terminal ?? string.Empty, [], workingDirectory);
        }
    }
}
