using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DevBoard.DevSpaces
{
    internal static class DevSpacesBootstrap
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Views.Repository>(OnRepositoryLoaded);
            Control.UnloadedEvent.AddClassHandler<Views.Repository>(OnRepositoryUnloaded);
        }

        private static void OnRepositoryLoaded(Views.Repository view, RoutedEventArgs e)
        {
            if (view.DataContext is not ViewModels.Repository repository)
                return;

            if (_repositoryViews.TryGetValue(view, out _))
                return;

            var integration = RepositoryIntegration.TryCreate(view, repository);
            if (integration != null)
                _repositoryViews.Add(view, integration);
        }

        private static void OnRepositoryUnloaded(Views.Repository view, RoutedEventArgs e)
        {
            if (!_repositoryViews.TryGetValue(view, out var integration))
                return;

            integration.Detach();
            _repositoryViews.Remove(view);
        }

        private sealed class RepositoryIntegration
        {
            public static RepositoryIntegration TryCreate(Views.Repository view, ViewModels.Repository repository)
            {
                if (view.Content is not Grid root)
                    return null;

                var leftPanel = root.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetColumn(x) == 0);
                var dashboard = leftPanel?.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetRow(x) == 1 && x.RowDefinitions.Count == 3);
                var pageSwitcherBorder = dashboard?.Children
                    .OfType<Border>()
                    .FirstOrDefault(x => Grid.GetRow(x) == 0);
                var pageSwitcher = pageSwitcherBorder?.Child as ListBox;

                var rightPanel = root.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetColumn(x) == 2);
                var rightPages = rightPanel?.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetRow(x) == 3);

                if (pageSwitcher == null || rightPages == null || pageSwitcher.ItemsSource != null)
                    return null;

                var item = CreateNavigationItem(view, out var label, out var badge, out var badgeLabel);
                var filesItem = CreateToolNavigationItem(view, "Icons.Folder", App.Text("DevSpaces.Files"), "Files");
                var aiRouterItem = CreateToolNavigationItem(view, "Icons.AIAssist", "AI Router", "AIRouter");
                var roslynItem = CreateToolNavigationItem(view, "Icons.Code", "Roslyn", "Roslyn");
                var terminalsItem = CreateToolNavigationItem(view, "Icons.Terminal", App.Text("DevSpaces.Terminals"), "Terminals");
                var terminalsLabel = GetToolNavigationLabel(terminalsItem);
                pageSwitcher.Items.Add(item);
                pageSwitcher.Items.Add(filesItem);
                pageSwitcher.Items.Add(aiRouterItem);
                pageSwitcher.Items.Add(roslynItem);
                pageSwitcher.Items.Add(terminalsItem);

                var host = new Border
                {
                    IsVisible = false,
                    Opacity = 0,
                    IsHitTestVisible = false,
                };
                rightPages.Children.Add(host);

                return new RepositoryIntegration(
                    repository,
                    pageSwitcher,
                    item,
                    filesItem,
                    aiRouterItem,
                    roslynItem,
                    terminalsItem,
                    label,
                    badge,
                    badgeLabel,
                    terminalsLabel,
                    host);
            }

            private RepositoryIntegration(
                ViewModels.Repository repository,
                ListBox pageSwitcher,
                ListBoxItem navigationItem,
                ListBoxItem filesNavigationItem,
                ListBoxItem aiRouterNavigationItem,
                ListBoxItem roslynNavigationItem,
                ListBoxItem terminalsNavigationItem,
                TextBlock navigationLabel,
                Border navigationBadge,
                TextBlock navigationBadgeLabel,
                TextBlock terminalsNavigationLabel,
                Border host)
            {
                _repository = repository;
                _pageSwitcher = pageSwitcher;
                _navigationItem = navigationItem;
                _filesNavigationItem = filesNavigationItem;
                _aiRouterNavigationItem = aiRouterNavigationItem;
                _roslynNavigationItem = roslynNavigationItem;
                _terminalsNavigationItem = terminalsNavigationItem;
                _navigationLabel = navigationLabel;
                _navigationBadge = navigationBadge;
                _navigationBadgeLabel = navigationBadgeLabel;
                _terminalsNavigationLabel = terminalsNavigationLabel;
                _host = host;

                _repository.PropertyChanged += OnRepositoryPropertyChanged;
                ViewModels.Preferences.Instance.PropertyChanged += OnPreferencesPropertyChanged;
                _pageSwitcher.SelectionChanged += OnPageSwitcherSelectionChanged;

                if (ViewModels.Preferences.Instance.EnableDevSpaces)
                    AttachSpaces();

                Update();
            }

            public void Detach()
            {
                if (_host.Child is Views.DevSpaces spacesView)
                    spacesView.SetPageActive(false);

                _repository.PropertyChanged -= OnRepositoryPropertyChanged;
                ViewModels.Preferences.Instance.PropertyChanged -= OnPreferencesPropertyChanged;
                _pageSwitcher.SelectionChanged -= OnPageSwitcherSelectionChanged;
                DetachSpaces();
            }

            private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ViewModels.Repository.SelectedViewIndex))
                    Update();
            }

            private void OnPreferencesPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ViewModels.Preferences.EnableDevSpaces))
                    Update();
            }

            private void OnSessionsChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                UpdateNavigationLabel();
            }

            private void OnSpacesPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(ViewModels.DevSpaces.ActivePage) || _spaces == null)
                    return;

                var index = _spaces.ActivePage switch
                {
                    Models.DevSpacePage.Files => FilesNavigationIndex,
                    Models.DevSpacePage.AIRouter => AIRouterNavigationIndex,
                    Models.DevSpacePage.Roslyn => RoslynNavigationIndex,
                    Models.DevSpacePage.Terminals => TerminalsNavigationIndex,
                    _ => DevSpacesNavigationIndex,
                };

                if (_repository.SelectedViewIndex == index)
                    return;

                _syncingNavigationSelection = true;
                try
                {
                    _repository.SelectedViewIndex = index;
                }
                finally
                {
                    _syncingNavigationSelection = false;
                }
            }

            private void OnPageSwitcherSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (_syncingNavigationSelection || !ViewModels.Preferences.Instance.EnableDevSpaces)
                    return;

                var selectedIndex = _pageSwitcher.SelectedIndex;
                if (!IsDevSpacesNavigationIndex(selectedIndex))
                    return;

                AttachSpaces();
                if (_spaces == null)
                    return;

                switch (selectedIndex)
                {
                    case FilesNavigationIndex:
                        _spaces.ActivateFiles();
                        break;
                    case AIRouterNavigationIndex:
                        _spaces.ActivateAIRouter();
                        break;
                    case RoslynNavigationIndex:
                        _spaces.ActivateRoslyn();
                        break;
                    case TerminalsNavigationIndex:
                        _spaces.ActivateTerminals();
                        break;
                    default:
                        _spaces.ActivateDashboard();
                        break;
                }

                Update();
            }

            private void AttachSpaces()
            {
                if (_spaces != null)
                    return;

                _spaces = DevSpaceRegistry.Attach(_repository, _host);
                if (_spaces != null)
                {
                    _spaces.Sessions.CollectionChanged += OnSessionsChanged;
                    _spaces.PropertyChanged += OnSpacesPropertyChanged;
                }

                UpdateNavigationLabel();
            }

            private void DetachSpaces()
            {
                if (_spaces != null)
                {
                    _spaces.Sessions.CollectionChanged -= OnSessionsChanged;
                    _spaces.PropertyChanged -= OnSpacesPropertyChanged;
                }

                _spaces = null;
                UpdateNavigationLabel();
            }

            private void Update()
            {
                var enabled = ViewModels.Preferences.Instance.EnableDevSpaces;
                _navigationItem.IsVisible = enabled;
                _filesNavigationItem.IsVisible = enabled;
                _aiRouterNavigationItem.IsVisible = enabled;
                _roslynNavigationItem.IsVisible = enabled;
                _terminalsNavigationItem.IsVisible = enabled;

                if (!enabled)
                {
                    if (_host.Child is Views.DevSpaces spacesView)
                        spacesView.SetPageActive(false);

                    _host.IsVisible = false;
                    _host.Opacity = 0;
                    _host.IsHitTestVisible = false;
                    DetachSpaces();

                    if (IsDevSpacesNavigationIndex(_repository.SelectedViewIndex))
                        _repository.SelectedViewIndex = 0;
                    return;
                }

                AttachSpaces();

                // Keep the terminal subtree mounted and measured while another repository page
                // is active. Hiding with IsVisible would collapse the Avalonia fallback and
                // force its TUI to resize/reload when returning to DevSpaces. Native HWNDs are
                // hidden separately by DevSpaces.SetPageActive.
                _host.IsVisible = true;
                var active = IsDevSpacesNavigationIndex(_repository.SelectedViewIndex);
                _host.Opacity = active ? 1 : 0;
                _host.IsHitTestVisible = active;

                if (_host.Child is Views.DevSpaces activeSpacesView)
                    activeSpacesView.SetPageActive(active);

                if (active)
                    _spaces?.EnsureFirstSession();
            }

            private void UpdateNavigationLabel()
            {
                var count = _spaces?.Sessions.Count ?? 0;
                _navigationLabel.Text = App.Text("DevSpaces");
                _navigationBadge.IsVisible = false;
                _navigationBadgeLabel.Text = count.ToString();
                _terminalsNavigationLabel.Text = $"{App.Text("DevSpaces.Terminals")} ({count})";
            }

            private static bool IsDevSpacesNavigationIndex(int index) =>
                index >= DevSpacesNavigationIndex && index <= TerminalsNavigationIndex;

            private static ListBoxItem CreateNavigationItem(
                Views.Repository view,
                out TextBlock label,
                out Border badge,
                out TextBlock badgeLabel)
            {
                var indicator = new Rectangle
                {
                    Width = 4,
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                indicator.Classes.Add("indicator");

                var icon = new Path
                {
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(6, 0),
                };
                icon.Classes.Add("icon");
                if (view.TryFindResource("Icons.Terminal", out var iconResource) && iconResource is Geometry geometry)
                    icon.Data = geometry;

                label = new TextBlock
                {
                    Text = App.Text("DevSpaces"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                label.Classes.Add("header");

                badgeLabel = new TextBlock
                {
                    Text = "0",
                    FontSize = 10,
                };
                badgeLabel.Bind(TextBlock.ForegroundProperty, view.GetResourceObservable("Brush.BadgeFG"));
                badgeLabel.Bind(TextBlock.FontFamilyProperty, view.GetResourceObservable("Fonts.Monospace"));

                badge = new Border
                {
                    Height = 18,
                    Margin = new Thickness(6, 0),
                    Padding = new Thickness(9, 0),
                    CornerRadius = new CornerRadius(9),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsVisible = false,
                    Child = badgeLabel,
                };
                badge.Bind(Border.BackgroundProperty, view.GetResourceObservable("Brush.Badge"));

                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("4,Auto,*,Auto"),
                };
                content.Children.Add(indicator);
                Grid.SetColumn(icon, 1);
                content.Children.Add(icon);
                Grid.SetColumn(label, 2);
                content.Children.Add(label);
                Grid.SetColumn(badge, 3);
                content.Children.Add(badge);

                return new ListBoxItem
                {
                    Content = content,
                };
            }

            private static ListBoxItem CreateToolNavigationItem(Views.Repository view, string iconKey, string text, string tag)
            {
                var indicator = new Rectangle
                {
                    Width = 4,
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                indicator.Classes.Add("indicator");

                var icon = new Path
                {
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(6, 0),
                };
                icon.Classes.Add("icon");
                if (view.TryFindResource(iconKey, out var iconResource) && iconResource is Geometry geometry)
                    icon.Data = geometry;

                var label = new TextBlock
                {
                    Text = text,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                label.Classes.Add("header");

                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("4,Auto,*"),
                };
                content.Children.Add(indicator);
                Grid.SetColumn(icon, 1);
                content.Children.Add(icon);
                Grid.SetColumn(label, 2);
                content.Children.Add(label);

                return new ListBoxItem
                {
                    Tag = tag,
                    Content = content,
                };
            }

            private static TextBlock GetToolNavigationLabel(ListBoxItem item) =>
                ((Grid)item.Content).Children.OfType<TextBlock>().Single();

            private const int DevSpacesNavigationIndex = 3;
            private const int FilesNavigationIndex = 4;
            private const int AIRouterNavigationIndex = 5;
            private const int RoslynNavigationIndex = 6;
            private const int TerminalsNavigationIndex = 7;

            private readonly ViewModels.Repository _repository;
            private readonly ListBox _pageSwitcher;
            private readonly ListBoxItem _navigationItem;
            private readonly ListBoxItem _filesNavigationItem;
            private readonly ListBoxItem _aiRouterNavigationItem;
            private readonly ListBoxItem _roslynNavigationItem;
            private readonly ListBoxItem _terminalsNavigationItem;
            private readonly TextBlock _navigationLabel;
            private readonly Border _navigationBadge;
            private readonly TextBlock _navigationBadgeLabel;
            private readonly TextBlock _terminalsNavigationLabel;
            private readonly Border _host;
            private ViewModels.DevSpaces _spaces;
            private bool _syncingNavigationSelection;
        }

        private static readonly ConditionalWeakTable<Views.Repository, RepositoryIntegration> _repositoryViews = new();
    }
}
