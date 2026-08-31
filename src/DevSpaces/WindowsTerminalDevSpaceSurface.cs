using System;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;

using Porta.Pty;

using DevBoard.DevSpaces.Terminal;

namespace DevBoard.DevSpaces
{
    internal sealed class WindowsTerminalDevSpaceSurface : IDevSpaceTerminalSurface
    {
        internal WindowsTerminalDevSpaceSurface(TerminalTranscriptStore transcript)
        {
            _transcriptSink = new TerminalTranscriptSink(transcript);

            _view.ColumnDefinitions = new ColumnDefinitions("*,Auto");
            _view.Children.Add(_host);

            _scrollBar.Orientation = Orientation.Vertical;
            _scrollBar.AllowAutoHide = false;
            _scrollBar.IsVisible = true;
            _scrollBar.Minimum = 0;
            _scrollBar.Maximum = 0;
            _scrollBar.Value = 0;
            _scrollBar.ViewportSize = 0;
            _scrollBar.IsEnabled = false;
            Grid.SetColumn(_scrollBar, 1);
            _view.Children.Add(_scrollBar);

            _host.InputGenerated += OnInputGenerated;
            _host.TerminalResized += OnTerminalResized;
            _host.ScrollChanged += OnScrollChanged;
            _scrollBar.PropertyChanged += OnScrollBarPropertyChanged;
        }

        public Control View => _view;

        public string BackendName => "Windows Terminal";

        public event EventHandler<DevSpaceTerminalExitedEventArgs> Exited;

        public async Task StartAsync(DevSpaceLaunchSpec spec)
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                throw new InvalidOperationException("The terminal surface has already been started.");

            await _host.WaitForNativeCreatedAsync(_cts.Token).ConfigureAwait(false);

            var options = new PtyOptions
            {
                Name = spec.Process,
                App = spec.Process,
                CommandLine = spec.Arguments,
                Cwd = spec.WorkingDirectory,
                Cols = 80,
                Rows = 25,
            };

            _pty = await PtyProvider.SpawnAsync(options, _cts.Token).ConfigureAwait(false);
            _pty.ProcessExited += OnProcessExited;
            _acceptInput = true;

            _readerTask = ReadOutputAsync(_cts.Token);
            _writerTask = WriteInputAsync(_cts.Token);
        }

        public void SetPageActive(bool active)
        {
            // Native HWNDs ignore Avalonia opacity. Toggle the NativeControlHost itself so
            // inactive repository pages/overflow panes cannot bleed over adjacent content.
            _view.IsVisible = active;
            _host.IsVisible = active;
            _host.IsHitTestVisible = active;
            _scrollBar.IsVisible = active;
            _scrollBar.IsHitTestVisible = active;
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            _acceptInput = false;
            _input.Writer.TryComplete();
            _cts.Cancel();

            _host.InputGenerated -= OnInputGenerated;
            _host.TerminalResized -= OnTerminalResized;
            _host.ScrollChanged -= OnScrollChanged;
            _scrollBar.PropertyChanged -= OnScrollBarPropertyChanged;

            var pty = _pty;
            _pty = null;
            if (pty == null)
                return;

            pty.ProcessExited -= OnProcessExited;
            try
            {
                pty.Kill();
            }
            catch
            {
                // The PTY may already have exited.
            }
            finally
            {
                pty.Dispose();
            }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }

        private async Task ReadOutputAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[0x10000];
            var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
            var decoder = Encoding.UTF8.GetDecoder();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var pty = _pty;
                    if (pty == null)
                        break;

                    var read = await pty.ReaderStream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        RaiseExited(pty.ExitCode);
                        break;
                    }

                    var charCount = decoder.GetChars(buffer, 0, read, chars, 0, flush: false);
                    if (charCount > 0)
                    {
                        var output = new string(chars, 0, charCount);
                        _transcriptSink.WriteOutput(output);
                        _host.SendOutput(output);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during shutdown.
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when the PTY is disposed during shutdown.
            }
        }

        private async Task WriteInputAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var text in _input.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (string.IsNullOrEmpty(text))
                        continue;

                    var pty = _pty;
                    if (pty == null)
                        break;

                    var bytes = Encoding.UTF8.GetBytes(text);
                    await pty.WriterStream.WriteAsync(
                        bytes,
                        0,
                        bytes.Length,
                        cancellationToken).ConfigureAwait(false);
                    await pty.WriterStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during shutdown.
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when the PTY is disposed during shutdown.
            }
        }

        private void OnInputGenerated(string text)
        {
            if (_acceptInput && !string.IsNullOrEmpty(text))
                _input.Writer.TryWrite(text);
        }

        private void OnTerminalResized(int cols, int rows)
        {
            if (cols <= 0 || rows <= 0)
                return;

            try
            {
                _pty?.Resize(cols, rows);
            }
            catch (ObjectDisposedException)
            {
                // Resize raced terminal shutdown.
            }
        }

        private void OnScrollChanged(int viewTop, int viewHeight, int bufferSize)
        {
            Dispatcher.UIThread.Post(() => SyncScrollBar(viewTop, viewHeight, bufferSize));
        }

        private void SyncScrollBar(int viewTop, int viewHeight, int bufferSize)
        {
            if (Volatile.Read(ref _stopped) != 0)
                return;

            var viewport = Math.Max(0, viewHeight);
            var maxTop = Math.Max(0, bufferSize - viewport);
            var value = Math.Clamp(viewTop, 0, maxTop);

            _syncingScrollBar = true;
            try
            {
                _scrollBar.Minimum = 0;
                _scrollBar.Maximum = maxTop;
                _scrollBar.ViewportSize = viewport;
                _scrollBar.Value = value;
                _scrollBar.IsEnabled = maxTop > 0;
            }
            finally
            {
                _syncingScrollBar = false;
            }
        }

        private void OnScrollBarPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_syncingScrollBar || e.Property != ScrollBar.ValueProperty)
                return;

            _host.UserScroll((int)Math.Round(_scrollBar.Value));
        }

        private void OnProcessExited(object sender, PtyExitedEventArgs e)
        {
            RaiseExited(e.ExitCode);
        }

        private void RaiseExited(int exitCode)
        {
            if (Interlocked.Exchange(ref _exitRaised, 1) != 0)
                return;

            _transcriptSink.RecordExit(exitCode);
            Exited?.Invoke(this, new DevSpaceTerminalExitedEventArgs(exitCode));
        }

        private readonly Grid _view = new();
        private readonly Views.WindowsTerminalNativeHost _host = new();
        private readonly ScrollBar _scrollBar = new();
        private readonly TerminalTranscriptSink _transcriptSink;
        private readonly CancellationTokenSource _cts = new();
        private readonly Channel<string> _input = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

        private IPtyConnection _pty;
        private Task _readerTask;
        private Task _writerTask;
        private bool _acceptInput;
        private bool _syncingScrollBar;
        private int _started;
        private int _stopped;
        private int _exitRaised;
    }
}
