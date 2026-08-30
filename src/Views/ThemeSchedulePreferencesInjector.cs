using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    internal static class ThemeSchedulePreferencesInjector
    {
        public static void EnsureInjected()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            foreach (var window in desktop.Windows.OfType<Preferences>())
                Inject(window);
        }

        private static void Inject(Preferences window)
        {
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabControl == null)
                return;

            var tabs = tabControl.Items.OfType<TabItem>().ToArray();
            if (tabs.Length < 2 || tabs[1].Content is not Grid appearanceGrid)
                return;

            if (appearanceGrid.Children.OfType<ThemeSchedulePreferences>().Any())
                return;

            var oldThemePicker = appearanceGrid.Children
                .OfType<ComboBox>()
                .FirstOrDefault(control => Grid.GetRow(control) == 0 && Grid.GetColumn(control) == 1);
            if (oldThemePicker == null)
                return;

            oldThemePicker.IsVisible = false;
            if (appearanceGrid.RowDefinitions.Count > 0)
                appearanceGrid.RowDefinitions[0].Height = GridLength.Auto;

            var themeLabel = appearanceGrid.Children
                .OfType<TextBlock>()
                .FirstOrDefault(control => Grid.GetRow(control) == 0 && Grid.GetColumn(control) == 0);
            if (themeLabel != null)
            {
                themeLabel.VerticalAlignment = VerticalAlignment.Top;
                themeLabel.Margin = new Thickness(0, 6, 16, 0);
            }

            var scheduler = new ThemeSchedulePreferences
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            Grid.SetRow(scheduler, 0);
            Grid.SetColumn(scheduler, 1);
            appearanceGrid.Children.Add(scheduler);
        }
    }
}
