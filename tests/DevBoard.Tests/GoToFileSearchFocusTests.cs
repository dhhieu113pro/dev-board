using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class GoToFileSearchFocusTests
    {
        [AvaloniaFact]
        public void ActivatingSearchOverlayFocusesSearchBox()
        {
            var searchView = new DevBoard.Views.GoToFileSearch();
            var overlay = new Border
            {
                IsVisible = false,
                Child = searchView,
            };
            var outside = new TextBox();
            var host = new Grid();
            host.Children.Add(outside);
            host.Children.Add(overlay);

            var window = new Window { Content = host };
            window.Show();

            try
            {
                Dispatcher.UIThread.RunJobs();
                outside.Focus();
                Assert.True(outside.IsFocused);

                using var search = new DevBoard.ViewModels.GoToFileSearch(string.Empty, null!);
                searchView.DataContext = search;
                overlay.IsVisible = true;
                Dispatcher.UIThread.RunJobs();

                var searchBox = searchView.FindControl<TextBox>("SearchBox");
                Assert.NotNull(searchBox);
                Assert.True(searchBox.IsFocused);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
