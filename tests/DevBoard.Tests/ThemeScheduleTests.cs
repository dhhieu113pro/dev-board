using DevBoard.Models;
using Xunit;

namespace DevBoard.Tests;

public class ThemeScheduleTests
{
    [Theory]
    [InlineData("System", "System")]
    [InlineData("Default", "System")]
    [InlineData("Light", "Light")]
    [InlineData("Dark", "Dark")]
    public void Resolve_PreservesManualAndSystemModes(string mode, string expected)
    {
        var actual = ThemeSchedule.Resolve(
            mode,
            new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Local),
            TimeSpan.FromHours(7),
            TimeSpan.FromHours(18),
            null,
            null);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(6, 59, "Dark")]
    [InlineData(7, 0, "Light")]
    [InlineData(17, 59, "Light")]
    [InlineData(18, 0, "Dark")]
    [InlineData(23, 30, "Dark")]
    public void Resolve_CustomTime_UsesLightBetweenConfiguredBoundaries(int hour, int minute, string expected)
    {
        var actual = ThemeSchedule.Resolve(
            "Custom",
            new DateTime(2026, 8, 30, hour, minute, 0, DateTimeKind.Local),
            TimeSpan.FromHours(7),
            TimeSpan.FromHours(18),
            null,
            null);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(21, 59, "Dark")]
    [InlineData(22, 0, "Light")]
    [InlineData(2, 0, "Light")]
    [InlineData(5, 59, "Light")]
    [InlineData(6, 0, "Dark")]
    public void Resolve_CustomTime_SupportsOvernightLightRange(int hour, int minute, string expected)
    {
        var actual = ThemeSchedule.Resolve(
            "Custom",
            new DateTime(2026, 8, 30, hour, minute, 0, DateTimeKind.Local),
            TimeSpan.FromHours(22),
            TimeSpan.FromHours(6),
            null,
            null);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Resolve_SunsetWithoutCoordinates_FallsBackToSystem()
    {
        var actual = ThemeSchedule.Resolve(
            "Sunset",
            new DateTime(2026, 8, 30, 20, 0, 0, DateTimeKind.Local),
            TimeSpan.FromHours(7),
            TimeSpan.FromHours(18),
            null,
            null);

        Assert.Equal("System", actual);
    }

    [Fact]
    public void Resolve_Sunset_UsesCalculatedSolarBoundaries()
    {
        var date = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Local);
        var solar = ThemeSchedule.GetSunriseSunset(date, 0, 0, TimeSpan.Zero);

        Assert.NotNull(solar);
        Assert.InRange(solar.Value.Sunrise.TimeOfDay, TimeSpan.FromHours(5), TimeSpan.FromHours(7));
        Assert.InRange(solar.Value.Sunset.TimeOfDay, TimeSpan.FromHours(17), TimeSpan.FromHours(19));
        Assert.Equal("Light", ThemeSchedule.Resolve("Sunset", date, TimeSpan.Zero, TimeSpan.Zero, 0, 0, TimeSpan.Zero));
        Assert.Equal("Dark", ThemeSchedule.Resolve("Sunset", date.AddHours(8), TimeSpan.Zero, TimeSpan.Zero, 0, 0, TimeSpan.Zero));
    }
}
