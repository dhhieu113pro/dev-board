using System;
using System.Linq;
using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    public partial class RepositoryConfigure
    {
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            AddGitHubBindingSelector();
        }

        private void AddGitHubBindingSelector()
        {
            if (_githubBindingAdded || Content is not Grid root)
                return;

            var tabs = root.Children.OfType<TabControl>().FirstOrDefault();
            if (tabs?.Items.Count == 0 || tabs.Items[0] is not TabItem { Content: Grid gitGrid })
                return;

            var repoPath = GetRepositoryPath();
            if (string.IsNullOrWhiteSpace(repoPath))
                return;

            _githubBindingAdded = true;
            gitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            gitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var accountRow = gitGrid.RowDefinitions.Count - 2;
            var statusRow = gitGrid.RowDefinitions.Count - 1;

            var label = new TextBlock
            {
                Text = "GitHub auth",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetRow(label, accountRow);
            Grid.SetColumn(label, 0);
            gitGrid.Children.Add(label);

            var controls = new Grid();
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(controls, accountRow);
            Grid.SetColumn(controls, 1);

            _githubAccountCombo = new ComboBox
            {
                Height = 28,
                Padding = new Thickness(8, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Unbound",
                ItemsSource = Services.GitHubAccountStore.Instance.Accounts,
            };
            _githubAccountCombo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Models.GitHubAccount.DisplayName));
            _githubAccountCombo.SelectedItem = Services.GitHubCredential.FindForRepository(repoPath);
            _githubAccountCombo.SelectionChanged += (_, _) =>
            {
                if (_changingGitHubSelection)
                    return;

                Services.GitHubCredential.BindRepository(repoPath, _githubAccountCombo.SelectedItem as Models.GitHubAccount);
                UpdateGitHubBindingStatus(repoPath, _githubAccountCombo.SelectedItem as Models.GitHubAccount, "manual");
            };
            Grid.SetColumn(_githubAccountCombo, 0);
            controls.Children.Add(_githubAccountCombo);

            var detect = new Button
            {
                Content = "Auto-detect",
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
            };
            detect.Click += async (_, args) =>
            {
                detect.IsEnabled = false;
                _githubBindingStatus.Text = "Detecting from repository remotes...";
                var detected = await Services.GitHubCredential.DetectForRepositoryAsync(repoPath);
                _changingGitHubSelection = true;
                _githubAccountCombo.ItemsSource = Services.GitHubAccountStore.Instance.Accounts;
                _githubAccountCombo.SelectedItem = detected;
                _changingGitHubSelection = false;
                UpdateGitHubBindingStatus(repoPath, detected, detected == null ? null : "auto-detected");
                detect.IsEnabled = true;
                args.Handled = true;
            };
            Grid.SetColumn(detect, 1);
            controls.Children.Add(detect);

            var manage = new Button
            {
                Content = "Manage...",
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
            };
            manage.Click += async (_, args) =>
            {
                await new Preferences().ShowDialog(this);
                _changingGitHubSelection = true;
                _githubAccountCombo.ItemsSource = null;
                _githubAccountCombo.ItemsSource = Services.GitHubAccountStore.Instance.Accounts;
                _githubAccountCombo.SelectedItem = Services.GitHubCredential.FindForRepository(repoPath);
                _changingGitHubSelection = false;
                UpdateGitHubBindingStatus(repoPath, _githubAccountCombo.SelectedItem as Models.GitHubAccount, null);
                args.Handled = true;
            };
            Grid.SetColumn(manage, 2);
            controls.Children.Add(manage);
            gitGrid.Children.Add(controls);

            _githubBindingStatus = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(_githubBindingStatus, statusRow);
            Grid.SetColumn(_githubBindingStatus, 1);
            gitGrid.Children.Add(_githubBindingStatus);

            UpdateGitHubBindingStatus(repoPath, _githubAccountCombo.SelectedItem as Models.GitHubAccount, null);
        }

        private string GetRepositoryPath()
        {
            if (DataContext is not ViewModels.RepositoryConfigure configure)
                return null;

            // Keep this UI port isolated from the heavily customized RepositoryConfigure
            // view model. The source PR stores the repository privately as well.
            var field = typeof(ViewModels.RepositoryConfigure).GetField("_repo", BindingFlags.Instance | BindingFlags.NonPublic);
            return (field?.GetValue(configure) as ViewModels.Repository)?.FullPath;
        }

        private void UpdateGitHubBindingStatus(string repoPath, Models.GitHubAccount account, string source)
        {
            if (_githubBindingStatus == null)
                return;

            if (account != null)
            {
                _githubBindingStatus.Text = string.IsNullOrEmpty(source)
                    ? $"Bound to {account.DisplayName}"
                    : $"Bound to {account.DisplayName} ({source})";
                return;
            }

            _githubBindingStatus.Text = Services.GitHubAccountStore.Instance.Accounts.Count == 0
                ? "No GitHub accounts configured — add one in Preferences"
                : "Unbound — automatic fetch will not open credential prompts";
        }

        private ComboBox _githubAccountCombo;
        private TextBlock _githubBindingStatus;
        private bool _githubBindingAdded;
        private bool _changingGitHubSelection;
    }
}
