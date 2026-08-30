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

            var items = new[] { devSpacesItem, filesItem, aiRouterItem };
            Assert.All(items, item => Assert.IsType<Grid>(item.Content));

            var labels = items
                .Select(item => Assert.IsType<Grid>(item.Content))
                .SelectMany(grid => grid.Children.OfType<TextBlock>())
                .Select(x => x.Text)
                .ToArray();

            Assert.Contains(App.Text("DevSpaces"), labels);
            Assert.Contains(App.Text("DevSpaces.Files"), labels);
            Assert.Contains("AI Router", labels);
        }
    }
}
