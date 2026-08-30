using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class DevSpaceFiles : UserControl
    {
        public DevSpaceFiles() => InitializeComponent();

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.DevSpaceFiles files)
                await files.RefreshAsync();
            e.Handled = true;
        }

        private void OnTreeArrowClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.DevSpaceFiles files &&
                sender is Control { DataContext: ViewModels.DevSpaceFileNode node } &&
                node.IsDirectory)
            {
                files.ToggleExpanded(node);
                e.Handled = true;
            }
        }

        private void OnTreeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.DevSpaceFiles files ||
                e.Source is not Control { DataContext: ViewModels.DevSpaceFileNode node } ||
                !node.IsDirectory)
                return;

            files.ToggleExpanded(node);
            e.Handled = true;
        }

        private void OnTreeArrowDoubleTapped(object sender, TappedEventArgs e)
        {
            e.Handled = true;
        }

        private void OnFolderChildDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.DevSpaceFiles files &&
                sender is Control { DataContext: ViewModels.DevSpaceFileNode node })
            {
                files.OpenFolderChild(node);
                e.Handled = true;
            }
        }
    }
}
