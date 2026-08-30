using System.Linq;

using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    public partial class Preferences
    {
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            EnsureGitHubAccountsTab();
        }

        private void EnsureGitHubAccountsTab()
        {
            var tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabs == null || tabs.Items.OfType<TabItem>().Any(x => x.Tag as string == GitHubAccountsTabTag))
                return;

            tabs.Items.Add(new TabItem
            {
                Tag = GitHubAccountsTabTag,
                Header = new TextBlock
                {
                    Classes = { "tab_header" },
                    Text = "GitHub Accounts",
                },
                Content = new GitHubAccounts
                {
                    DataContext = new ViewModels.GitHubAccountsViewModel(),
                },
            });
        }

        private const string GitHubAccountsTabTag = "devboard.github-accounts";
    }
}
