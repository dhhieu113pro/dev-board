using System;
using System.IO;

using DevBoard.DevSpaces;
using DevBoard.Models;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceDashboardStatsLogsTests
{
    [Theory]
    [InlineData(0, StatisticsMode.ThisWeek)]
    [InlineData(1, StatisticsMode.ThisMonth)]
    [InlineData(2, StatisticsMode.All)]
    public void StatisticsModeSelectionUsesWeeklyMonthlyTotalOrder(int index, StatisticsMode expected)
    {
        Assert.Equal(expected, DevSpaceDashboard.GetStatisticsMode(index));
        Assert.Equal(index, DevSpaceDashboard.GetStatisticsModeIndex(expected));
    }

    [Fact]
    public void DashboardStatsDoesNotExposeBranchFilter()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DevBoard.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var xaml = File.ReadAllText(Path.Combine(directory!.FullName, "src", "Views", "DevSpaceDashboard.axaml"));

        Assert.DoesNotContain("<v:BranchSelector", xaml);
    }

    [Fact]
    public void DashboardWithoutRepositoryDoesNotExposeRepositoryStatsOrLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-stats-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

            Assert.Null(spaces.Dashboard.RepositoryStatistics);
            Assert.Null(spaces.Dashboard.RepositoryLogs);
            Assert.False(spaces.Dashboard.HasRepositoryInsights);
        }
        finally
        {
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
