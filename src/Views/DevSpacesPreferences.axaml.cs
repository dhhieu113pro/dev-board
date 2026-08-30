using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace DevBoard.Views
{
    public partial class DevSpacesPreferences : UserControl
    {
        public DevSpacesPreferences()
        {
            InitializeComponent();
            var mcpSettings = Mcp.DevBoardMcpSettings.Instance;
            mcpSettings.EnsureAuthToken();
            mcpSettings.PropertyChanged += OnMcpSettingsPropertyChanged;
            McpSettingsPanel.DataContext = mcpSettings;
            DataContextChanged += (_, _) => NormalizeLegacyLayout();
            AttachedToVisualTree += (_, _) =>
            {
                if (!_isDetached)
                    return;

                _isDetached = false;
                mcpSettings.PropertyChanged += OnMcpSettingsPropertyChanged;
            };
            DetachedFromVisualTree += (_, _) =>
            {
                _isDetached = true;
                mcpSettings.PropertyChanged -= OnMcpSettingsPropertyChanged;
                CancelMcpConnectionTest();
            };
        }

        private void OnEnableChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.Preferences preferences || sender is not CheckBox checkBox)
                return;

            preferences.EnableDevSpaces = checkBox.IsChecked == true;
            if (!preferences.EnableDevSpaces)
                DevBoard.DevSpaces.DevSpaceRegistry.DisableAll();

            e.Handled = true;
        }

        private void OnRegenerateMcpToken(object sender, RoutedEventArgs e)
        {
            Mcp.DevBoardMcpSettings.Instance.RegenerateAuthToken();
            e.Handled = true;
        }

        private void OnToggleMcpToken(object sender, RoutedEventArgs e)
        {
            var reveal = !VisibleMcpTokenTextBox.IsVisible;
            VisibleMcpTokenTextBox.IsVisible = reveal;
            MaskedMcpTokenTextBox.IsVisible = !reveal;
            ToggleMcpTokenButton.Content = reveal ? "Hide" : "Show";
            e.Handled = true;
        }

        private async void OnCopyMcpEndpoint(object sender, RoutedEventArgs e)
        {
            await CopyToClipboardAsync(Mcp.DevBoardMcpSettings.Instance.DisplayEndpoint);
            e.Handled = true;
        }

        private async void OnCopyMcpToken(object sender, RoutedEventArgs e)
        {
            await CopyToClipboardAsync(Mcp.DevBoardMcpSettings.Instance.AuthToken);
            e.Handled = true;
        }

        private async void OnCopyMcpConfiguration(object sender, RoutedEventArgs e)
        {
            var settings = Mcp.DevBoardMcpSettings.Instance;
            settings.EnsureAuthToken();
            await CopyToClipboardAsync(settings.McpClientConfiguration);
            e.Handled = true;
        }

        private async void OnTestMcpConnection(object sender, RoutedEventArgs e)
        {
            var settings = Mcp.DevBoardMcpSettings.Instance;
            CancelMcpConnectionTest();
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _mcpConnectionTestCancellation = cancellation;
            McpConnectionTestResult.Text = "Testing…";
            TestMcpConnectionButton.IsEnabled = false;

            try
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, settings.DisplayEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AuthToken);
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellation.Token);
                if (_mcpConnectionTestCancellation == cancellation)
                {
                    McpConnectionTestResult.Text = response.IsSuccessStatusCode
                        ? "Connected"
                        : $"Failed ({(int)response.StatusCode})";
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (_mcpConnectionTestCancellation == cancellation)
                    McpConnectionTestResult.Text = "Connection test timed out";
            }
            catch (Exception ex)
            {
                if (_mcpConnectionTestCancellation == cancellation)
                    McpConnectionTestResult.Text = $"Failed: {ex.Message}";
            }
            finally
            {
                if (_mcpConnectionTestCancellation == cancellation)
                {
                    _mcpConnectionTestCancellation = null;
                    TestMcpConnectionButton.IsEnabled = true;
                }

                cancellation.Dispose();
            }

            e.Handled = true;
        }

        private async System.Threading.Tasks.Task CopyToClipboardAsync(string value)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(value ?? string.Empty);
        }

        private void OnMcpSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Mcp.DevBoardMcpSettings.Enabled) or
                nameof(Mcp.DevBoardMcpSettings.Port) or
                nameof(Mcp.DevBoardMcpSettings.AuthToken) or
                nameof(Mcp.DevBoardMcpSettings.DisplayEndpoint))
            {
                if (Dispatcher.UIThread.CheckAccess())
                    InvalidateMcpConnectionTest();
                else
                    Dispatcher.UIThread.Post(InvalidateMcpConnectionTest);
            }
        }

        private void InvalidateMcpConnectionTest()
        {
            if (_isDetached)
                return;

            CancelMcpConnectionTest();
            McpConnectionTestResult.Text = string.Empty;
            TestMcpConnectionButton.IsEnabled = true;
        }

        private void CancelMcpConnectionTest()
        {
            var cancellation = _mcpConnectionTestCancellation;
            _mcpConnectionTestCancellation = null;
            cancellation?.Cancel();
        }

        private void NormalizeLegacyLayout()
        {
            if (DataContext is ViewModels.Preferences preferences &&
                preferences.DevSpacesDefaultLayout == Models.DevSpaceLayout.FourByFour)
            {
                preferences.DevSpacesDefaultLayout = Models.DevSpaceLayout.ThreeByThree;
            }
        }

        private CancellationTokenSource _mcpConnectionTestCancellation;
        private bool _isDetached;
    }
}
