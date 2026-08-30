using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    public partial class Repository
    {
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Dispatcher.UIThread.Post(EnsurePullRequestPage);
        }

        private void EnsurePullRequestPage()
        {
            if (_pullRequestCherryPickPage != null || DataContext is not ViewModels.Repository repo)
                return;

            var navigation = this.GetVisualDescendants()
                .OfType<ListBox>()
                .FirstOrDefault(x => x.Items.Count == 3 &&
                                     x.Items[0] is ListBoxItem &&
                                     x.Items[1] is ListBoxItem &&
                                     x.Items[2] is ListBoxItem);
            if (navigation == null)
                return;

            var rightPages = this.GetVisualDescendants()
                .OfType<Grid>()
                .FirstOrDefault(x => x.Children.OfType<Border>().Any(b => b.Child is Histories) &&
                                     x.Children.OfType<Border>().Any(b => b.Child is WorkingCopy) &&
                                     x.Children.OfType<Border>().Any(b => b.Child is StashesPage));
            if (rightPages == null)
                return;

            var header = new TextBlock
            {
                Text = "🍒  Cherry Pick PR",
                Margin = new Thickness(6, 0),
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            header.Classes.Add("header");

            var item = new ListBoxItem
            {
                Content = header,
            };
            navigation.Items.Add(item);

            var vm = new ViewModels.PullRequestCherryPickPage(repo);
            _pullRequestCherryPickPage = new PullRequestCherryPickPage
            {
                DataContext = vm,
                IsVisible = false,
            };
            rightPages.Children.Add(_pullRequestCherryPickPage);

            navigation.SelectionChanged += (_, _) =>
            {
                var selected = navigation.SelectedIndex == 3;
                _pullRequestCherryPickPage.IsVisible = selected;
                if (selected)
                    vm.RefreshRemotes();
            };
        }

        private PullRequestCherryPickPage _pullRequestCherryPickPage;
    }
}
