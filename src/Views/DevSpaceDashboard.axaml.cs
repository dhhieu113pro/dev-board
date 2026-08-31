using System.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class DevSpaceDashboard : UserControl
    {
        public DevSpaceDashboard()
        {
            InitializeComponent();

            if (StatisticsRangeSwitcher.Items.Count > 2)
                StatisticsRangeSwitcher.Items.RemoveAt(2);
        }

        private ViewModels.DevSpaceDashboard Model => DataContext as ViewModels.DevSpaceDashboard;

        private void OnSessionPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { DataContext: ViewModels.DevSpaceDashboardSessionRow row })
                Model?.OpenSession(row.Terminal);
            e.Handled = true;
        }

        private void OnCloseSessionPointerPressed(object sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
        }

        private void OnCloseSession(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.DevSpaceDashboardSessionRow row })
                Model?.CloseSession(row.Terminal);
            e.Handled = true;
        }

        private void OnStartAgent(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string command })
            {
                var agent = DevBoard.DevSpaces.DevSpaceAgent.BuiltIn.FirstOrDefault(x => x.Command == command);
                if (agent != null)
                    Model?.StartAgent(agent);
            }
            e.Handled = true;
        }

        private void OnStartProfile(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: DevBoard.DevSpaces.DevSpaceTerminalProfile profile })
                Model?.StartProfile(profile);
            e.Handled = true;
        }

        private void OnStartTerminal(object sender, RoutedEventArgs e)
        {
            Model?.StartDefaultTerminal();
            e.Handled = true;
        }

        private async void OnInitializeRoslyn(object sender, RoutedEventArgs e)
        {
            var model = Model;
            if (model != null && model.CanInitializeRoslyn)
                await model.InitializeRoslynAsync();
            e.Handled = true;
        }

        private async void OnRefreshUnusedCode(object sender, RoutedEventArgs e)
        {
            if (Model != null)
                await Model.RefreshUnusedCodeAsync();
            e.Handled = true;
        }

        private void OnUnusedCodeFilter(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string filter })
                Model?.SetUnusedCodeFilter(filter);
            e.Handled = true;
        }

        private void OnUnusedCodePressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount >= 2 && sender is Border { DataContext: DevBoard.DevSpaces.RoslynUnusedCodeItem item })
                Model?.OpenUnusedCode(item);
            e.Handled = true;
        }

        private async void OnCopyPath(object sender, RoutedEventArgs e)
        {
            var model = Model;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (model != null && clipboard != null)
                await clipboard.SetTextAsync(model.WorkspacePath);
            e.Handled = true;
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            Model?.OpenWorkspaceFolder();
            e.Handled = true;
        }

        private void OnOpenFiles(object sender, RoutedEventArgs e)
        {
            Model?.OpenFiles();
            e.Handled = true;
        }

        private void OnOpenWorkingCopy(object sender, RoutedEventArgs e)
        {
            Model?.OpenWorkingCopy();
            e.Handled = true;
        }

        private void OnCloseAll(object sender, RoutedEventArgs e)
        {
            Model?.CloseAllSessions();
            e.Handled = true;
        }
    }
}
