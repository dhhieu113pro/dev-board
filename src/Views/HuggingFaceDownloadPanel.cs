using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public sealed class HuggingFaceDownloadPanel : StackPanel
    {
        public HuggingFaceDownloadPanel(AI.Service service)
        {
            _service = service;
            Orientation = Orientation.Vertical;
            Margin = new Thickness(0, 12, 0, 0);

            AttachedToVisualTree += (_, _) => AttachState(AI.HuggingFaceModelDownloader.Instance.GetState(_service));
            DetachedFromVisualTree += (_, _) => DetachState();

            Children.Add(new TextBlock { Text = "Download from Hugging Face" });

            _source = new TextBox
            {
                Height = 28,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 4, 0, 0),
                Watermark = "owner/repo or https://huggingface.co/...",
            };
            Children.Add(_source);

            var discoveryRow = new Grid
            {
                Margin = new Thickness(0, 4, 0, 0),
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };
            _files = new ComboBox
            {
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Choose a .gguf file",
            };
            Grid.SetColumn(_files, 0);
            discoveryRow.Children.Add(_files);

            _loadFiles = new Button
            {
                Content = "Load files",
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
            };
            _loadFiles.Click += async (_, _) => await LoadFilesAsync();
            Grid.SetColumn(_loadFiles, 1);
            discoveryRow.Children.Add(_loadFiles);
            Children.Add(discoveryRow);

            var actionRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0),
            };
            _download = new Button { Content = "Download", Height = 28 };
            _download.Click += (_, _) => StartDownload();
            actionRow.Children.Add(_download);

            _cancel = new Button
            {
                Content = "Cancel",
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
                IsVisible = false,
            };
            _cancel.Click += (_, _) => AI.HuggingFaceModelDownloader.Instance.Cancel(_service);
            actionRow.Children.Add(_cancel);

            _retry = new Button
            {
                Content = "Retry",
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
                IsVisible = false,
            };
            _retry.Click += (_, _) => AttachState(AI.HuggingFaceModelDownloader.Instance.Retry(_service));
            actionRow.Children.Add(_retry);
            Children.Add(actionRow);

            _progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 8,
                Margin = new Thickness(0, 8, 0, 0),
                IsVisible = false,
            };
            Children.Add(_progress);

            _progressText = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
            };
            Children.Add(_progressText);

            _statusText = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            };
            Children.Add(_statusText);

            AttachState(AI.HuggingFaceModelDownloader.Instance.GetState(_service));
        }

        private async System.Threading.Tasks.Task LoadFilesAsync()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            _loadFiles.IsEnabled = false;
            _statusText.Text = "Loading GGUF files...";

            try
            {
                var files = await AI.HuggingFaceModelDownloader.Instance.ResolveFilesAsync(_source.Text, _loadCts.Token);
                _resolvedFiles = files.ToList();
                _files.ItemsSource = _resolvedFiles.Select(FormatFile).ToArray();
                _files.SelectedIndex = _resolvedFiles.Count > 0 ? 0 : -1;
                _statusText.Text = _resolvedFiles.Count > 0
                    ? $"Found {_resolvedFiles.Count} GGUF file(s)"
                    : "No .gguf files found in this repository";
            }
            catch (OperationCanceledException)
            {
                _statusText.Text = "Loading canceled";
            }
            catch (Exception ex)
            {
                _resolvedFiles = [];
                _files.ItemsSource = null;
                _statusText.Text = ex.Message;
            }
            finally
            {
                _loadFiles.IsEnabled = true;
                UpdateButtons();
            }
        }

        private void StartDownload()
        {
            if (_files.SelectedIndex < 0 || _files.SelectedIndex >= _resolvedFiles.Count)
                return;

            try
            {
                AttachState(AI.HuggingFaceModelDownloader.Instance.StartDownload(_service, _resolvedFiles[_files.SelectedIndex]));
            }
            catch (Exception ex)
            {
                _statusText.Text = ex.Message;
            }
        }

        private void AttachState(AI.HuggingFaceDownloadState state)
        {
            if (ReferenceEquals(_state, state) && _isSubscribed)
            {
                QueueStateUpdate();
                return;
            }

            DetachState();
            _state = state;
            if (_state != null)
            {
                _state.PropertyChanged += OnStatePropertyChanged;
                _isSubscribed = true;
            }
            QueueStateUpdate();
        }

        private void DetachState()
        {
            if (_state != null && _isSubscribed)
                _state.PropertyChanged -= OnStatePropertyChanged;
            _isSubscribed = false;
            Interlocked.Exchange(ref _updateQueued, 0);
        }

        private void OnStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            QueueStateUpdate();
        }

        private void QueueStateUpdate()
        {
            if (Interlocked.Exchange(ref _updateQueued, 1) != 0)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref _updateQueued, 0);
                if (_isSubscribed || _state == null)
                    UpdateState();
            }, DispatcherPriority.Background);
        }

        private void UpdateState()
        {
            if (_state == null)
            {
                _progress.IsVisible = false;
                _progressText.Text = string.Empty;
                UpdateButtons();
                return;
            }

            _progress.IsVisible = _state.IsRunning || _state.BytesDownloaded > 0;
            _progress.IsIndeterminate = _state.IsRunning && _state.TotalBytes is not > 0;
            _progress.Value = _state.Percent;

            var details = new List<string>();
            if (!string.IsNullOrEmpty(_state.ProgressText))
                details.Add(_state.ProgressText);
            if (!string.IsNullOrEmpty(_state.SpeedText))
                details.Add(_state.SpeedText);
            if (!string.IsNullOrEmpty(_state.EtaText))
                details.Add(_state.EtaText);
            _progressText.Text = string.Join("  •  ", details);

            _statusText.Text = string.IsNullOrEmpty(_state.Error)
                ? _state.Status
                : $"{_state.Status}: {_state.Error}";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var running = _state?.IsRunning == true;
            _download.IsEnabled = !running && _files.SelectedIndex >= 0 && _files.SelectedIndex < _resolvedFiles.Count;
            _cancel.IsVisible = running;
            _cancel.IsEnabled = running;
            _retry.IsVisible = _state?.CanRetry == true;
            _retry.IsEnabled = _state?.CanRetry == true;
            _loadFiles.IsEnabled = !running;
        }

        private static string FormatFile(AI.HuggingFaceModelFile file)
        {
            return file.Size is > 0
                ? $"{file.FileName}  ({AI.HuggingFaceModelDownloader.FormatBytes(file.Size.Value)})"
                : file.FileName;
        }

        private readonly AI.Service _service;
        private readonly TextBox _source;
        private readonly ComboBox _files;
        private readonly Button _loadFiles;
        private readonly Button _download;
        private readonly Button _cancel;
        private readonly Button _retry;
        private readonly ProgressBar _progress;
        private readonly TextBlock _progressText;
        private readonly TextBlock _statusText;
        private CancellationTokenSource _loadCts;
        private List<AI.HuggingFaceModelFile> _resolvedFiles = [];
        private AI.HuggingFaceDownloadState _state;
        private bool _isSubscribed;
        private int _updateQueued;
    }
}