using System;

using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class DevSpaceRoslynNavigationTests
    {
        [Fact]
        public void DevSpaces_ExposesDedicatedRoslynAnalyticsPageModel()
        {
            var property = typeof(DevSpaces).GetProperty("RoslynAnalytics");

            Assert.NotNull(property);
            Assert.Equal("DevSpaceRoslynAnalytics", property!.PropertyType.Name);
        }
    }
}
