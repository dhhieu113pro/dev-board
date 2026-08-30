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

        private ViewModels.DevSpaceRoslynAnalytics Model => DataContext as ViewModels.DevSpaceRoslynAnalytics;

        private async void OnRefreshUnusedCode(object sender, RoutedEventArgs e)
        {
            if (Model?.Dashboard != null)
                await Model.Dashboard.RefreshUnusedCodeAsync();
            e.Handled = true;
        }

        private void OnUnusedCodeFilter(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string filter })
                Model?.Dashboard.SetUnusedCodeFilter(filter);
            e.Handled = true;
        }

        private void OnUnusedCodePressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount >= 2 && sender is Border { DataContext: DevBoard.DevSpaces.RoslynUnusedCodeItem item })
                Model?.Dashboard.OpenUnusedCode(item);
            e.Handled = true;
        }
    }
}
