using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    public partial class DevSpaceFiles : UserControl
    {
        public DevSpaceFiles()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, EventArgs e)
        {
            if (_files != null)
                _files.RevealRequested -= OnRevealRequested;

            _files = DataContext as ViewModels.DevSpaceFiles;
            if (_files != null)
                _files.RevealRequested += OnRevealRequested;
        }

        private void OnRevealRequested(ViewModels.DevSpaceFileNode node)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_files == null || !ReferenceEquals(_files.SelectedNode, node))
                    return;

                var tree = this.GetVisualDescendants()
                    .OfType<ListBox>()
                    .FirstOrDefault(list => ReferenceEquals(list.ItemsSource, _files.VisibleItems));
                tree?.ScrollIntoView(node);
            });
        }

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

        private ViewModels.DevSpaceFiles _files;
    }
}
