using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class PullRequestCherryPickPage : UserControl
    {
        public PullRequestCherryPickPage()
        {
            InitializeComponent();
        }

        private async void OnLoadPullRequest(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestCherryPickPage vm)
                await vm.LoadAsync();

            e.Handled = true;
        }

        private async void OnCherryPickAll(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestCherryPickPage vm)
                await vm.CherryPickAllAsync();

            e.Handled = true;
        }

        private async void OnCherryPickCommit(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PullRequestCherryPickPage vm &&
                sender is Button { Tag: Models.Commit commit })
            {
                await vm.CherryPickAsync(commit);
            }

            e.Handled = true;
        }
    }
}
