using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class DevSpaceRoslynAnalytics : UserControl
    {
        public DevSpaceRoslynAnalytics()
        {
            InitializeComponent();
        }

        private ViewModels.DevSpaces Owner => DataContext as ViewModels.DevSpaces;
        private ViewModels.DevSpaceDashboard Dashboard => Owner?.RoslynAnalytics?.Dashboard;

        private async void OnRefreshUnusedCode(object sender, RoutedEventArgs e)
        {
            if (Dashboard != null)
                await Dashboard.RefreshUnusedCodeAsync();
            e.Handled = true;
        }

        private void OnUnusedCodeFilter(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string filter })
                Dashboard?.SetUnusedCodeFilter(filter);
            e.Handled = true;
        }

        private void OnUnusedCodePressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount >= 2 && sender is Border { DataContext: DevBoard.DevSpaces.RoslynUnusedCodeItem item })
                Dashboard?.OpenUnusedCode(item);
            e.Handled = true;
        }
    }
}
