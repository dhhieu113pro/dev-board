using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class DevSpacesNavigationTests
    {
        [AvaloniaFact]
        public void RepositoryDevSpacesToolsUseSiblingNativeNavigationItems()
        {
            var assembly = typeof(Views.Repository).Assembly;
            var integrationType = assembly.GetType("DevBoard.DevSpaces.DevSpacesBootstrap+RepositoryIntegration");
            Assert.NotNull(integrationType);

            var devSpacesFactory = integrationType.GetMethod("CreateNavigationItem", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(devSpacesFactory);

            var devSpacesArguments = new object[] { new Views.Repository(), null, null, null };
            var devSpacesItem = Assert.IsType<ListBoxItem>(devSpacesFactory.Invoke(null, devSpacesArguments));
            Assert.IsType<Grid>(devSpacesItem.Content);

            var toolFactory = integrationType.GetMethod("CreateToolNavigationItem", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(toolFactory);

            var filesItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.Folder", App.Text("DevSpaces.Files"), "Files"
            }));
            var aiRouterItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.AIAssist", "AI Router", "AIRouter"
            }));
            var roslynItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.Code", "Roslyn", "Roslyn"
            }));
            var terminalsItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.Terminal", App.Text("DevSpaces.Terminals"), "Terminals"
            }));

            var items = new[] { devSpacesItem, filesItem, aiRouterItem, roslynItem, terminalsItem };
            Assert.All(items, item => Assert.IsType<Grid>(item.Content));

            var labels = items
                .Select(item => Assert.IsType<Grid>(item.Content))
                .SelectMany(grid => grid.Children.OfType<TextBlock>())
                .Select(x => x.Text)
                .ToArray();

            Assert.Contains(App.Text("DevSpaces"), labels);
            Assert.Contains(App.Text("DevSpaces.Files"), labels);
            Assert.Contains("AI Router", labels);
            Assert.Contains("Roslyn", labels);
            Assert.Contains(App.Text("DevSpaces.Terminals"), labels);
        }

        [Fact]
        public void RepositoryNavigationReservesSeparateRoslynAndTerminalsIndexes()
        {
            var assembly = typeof(Views.Repository).Assembly;
            var integrationType = assembly.GetType("DevBoard.DevSpaces.DevSpacesBootstrap+RepositoryIntegration");
            Assert.NotNull(integrationType);

            var roslynIndex = integrationType.GetField("RoslynNavigationIndex", BindingFlags.Static | BindingFlags.NonPublic);
            var terminalsIndex = integrationType.GetField("TerminalsNavigationIndex", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(roslynIndex);
            Assert.NotNull(terminalsIndex);
            Assert.Equal(6, roslynIndex.GetRawConstantValue());
            Assert.Equal(7, terminalsIndex.GetRawConstantValue());

            var isDevNavigationIndex = integrationType.GetMethod("IsDevSpacesNavigationIndex", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(isDevNavigationIndex);
            Assert.True(Assert.IsType<bool>(isDevNavigationIndex.Invoke(null, new object[] { 6 })));
            Assert.True(Assert.IsType<bool>(isDevNavigationIndex.Invoke(null, new object[] { 7 })));
        }
    }
}
