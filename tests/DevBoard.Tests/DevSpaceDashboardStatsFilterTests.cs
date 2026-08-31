using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceDashboardProfileDisplayTestsStatsFilter
{
    [AvaloniaFact]
    public void StatisticsRangeSwitcherShowsOnlyWeeklyAndMonthly()
    {
        var view = new Views.DevSpaceDashboard();
        var switcher = view.FindControl<ListBox>("StatisticsRangeSwitcher");
        Assert.NotNull(switcher);

        var visibleItems = switcher.Items
            .OfType<ListBoxItem>()
            .Where(item => item.IsVisible)
            .ToArray();

        Assert.Equal(2, visibleItems.Length);
        Assert.Equal("Weekly", GetLabel(visibleItems[0]));
        Assert.Equal("Monthly", GetLabel(visibleItems[1]));
    }

    [Fact]
    public void StatisticsRangeContainsOnlyWeeklyAndMonthly()
    {
        Assert.Equal(
            new[] { DevSpaceStatisticsRange.Weekly, DevSpaceStatisticsRange.Monthly },
            Enum.GetValues<DevSpaceStatisticsRange>());
    }

    private static string GetLabel(ListBoxItem item)
    {
        var border = Assert.IsType<Border>(item.Content);
        return Assert.IsType<TextBlock>(border.Child).Text;
    }
}
