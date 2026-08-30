using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.DevSpaces
{
    public sealed class RoslynDevSpaceService : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public RoslynDevSpaceState State
        {
            get => _state;
            private set
            {
                if (_state == value)
                    return;
                _state = value;
                OnPropertyChanged();
            }
        }

        public string FailureReason
        {
            get => _failureReason;
            private set
            {
                if (string.Equals(_failureReason, value, StringComparison.Ordinal))
                    return;
                _failureReason = value;
                OnPropertyChanged();
            }
        }

        public string WorkspacePath
        {
            get => _workspacePath;
            private set
            {
                if (string.Equals(_workspacePath, value, StringComparison.Ordinal))
                    return;
                _workspacePath = value;
                OnPropertyChanged();
            }
        }

        public IReadOnlyList<RoslynUnusedCodeItem> UnusedCode
        {
            get => _unusedCode;
            private set
            {
                _unusedCode = value ?? Array.Empty<RoslynUnusedCodeItem>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(UnusedCodeCount));
            }
        }

        public int UnusedCodeCount => UnusedCode.Count;

        public bool IsAnalyzingUnusedCode
        {
            get => _isAnalyzingUnusedCode;
            private set
            {
                if (_isAnalyzingUnusedCode == value)
                    return;
                _isAnalyzingUnusedCode = value;
                OnPropertyChanged();
            }
        }

        public string UnusedCodeFailureReason
        {
            get => _unusedCodeFailureReason;
            private set
            {
                if (string.Equals(_unusedCodeFailureReason, value, StringComparison.Ordinal))
                    return;
                _unusedCodeFailureReason = value;
                OnPropertyChanged();
            }
        }

        public IRoslynLoadedWorkspace LoadedWorkspace => _loadedWorkspace;

        public RoslynDevSpaceService(string workspaceRoot, IRoslynWorkspaceLoader loader)
            : this(workspaceRoot, loader, RoslynWorkspaceDiscovery.FindWorkspace)
        {
        }

        internal RoslynDevSpaceService(
            string workspaceRoot,
            IRoslynWorkspaceLoader loader,
            Func<string, string> discovery)
        {
            _workspaceRoot = workspaceRoot ?? string.Empty;
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(RoslynDevSpaceService));

                if (State == RoslynDevSpaceState.Available)
                    return Task.CompletedTask;

                if (_initializationTask != null)
                    return _initializationTask;

                FailureReason = string.Empty;
                State = RoslynDevSpaceState.Initializing;
                _initializationTask = InitializeCoreAsync(cancellationToken);
                return _initializationTask;
            }
        }

        public async Task RefreshUnusedCodeAsync(CancellationToken cancellationToken = default)
        {
            IRoslynLoadedWorkspace loaded;
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(RoslynDevSpaceService));
                loaded = _loadedWorkspace;
            }

            if (loaded == null || State != RoslynDevSpaceState.Available)
                return;

            IsAnalyzingUnusedCode = true;
            UnusedCodeFailureReason = string.Empty;
            try
            {
                UnusedCode = await loaded.FindUnusedCodeAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                UnusedCodeFailureReason = "Unused code analysis was canceled.";
            }
            catch (Exception ex)
            {
                UnusedCodeFailureReason = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Unused code analysis failed."
                    : ex.Message;
            }
            finally
            {
                IsAnalyzingUnusedCode = false;
            }
        }

        public void Dispose()
        {
            IRoslynLoadedWorkspace loaded;
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                loaded = _loadedWorkspace;
                _loadedWorkspace = null;
            }

            loaded?.Dispose();
        }

        private async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workspacePath = _discovery(_workspaceRoot);
                if (string.IsNullOrWhiteSpace(workspacePath))
                    throw new InvalidOperationException("No .slnx, .sln, or .csproj workspace was found.");

                WorkspacePath = workspacePath;
                var loaded = await _loader.LoadAsync(workspacePath, cancellationToken);
                if (loaded == null)
                    throw new InvalidOperationException("Roslyn did not return a loaded workspace.");

                if (loaded.ProjectCount <= 0)
                {
                    loaded.Dispose();
                    throw new InvalidOperationException("Roslyn loaded the workspace but found no projects.");
                }

                IRoslynLoadedWorkspace previous;
                lock (_gate)
                {
                    if (_disposed)
                    {
                        loaded.Dispose();
                        return;
                    }

                    previous = _loadedWorkspace;
                    _loadedWorkspace = loaded;
                }

                previous?.Dispose();
                State = RoslynDevSpaceState.Available;
                await RefreshUnusedCodeAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                FailureReason = "Roslyn initialization was canceled.";
                State = RoslynDevSpaceState.Failed;
            }
            catch (Exception ex)
            {
                FailureReason = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Roslyn initialization failed."
                    : ex.Message;
                State = RoslynDevSpaceState.Failed;
            }
            finally
            {
                lock (_gate)
                    _initializationTask = null;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private readonly object _gate = new();
        private readonly string _workspaceRoot;
        private readonly IRoslynWorkspaceLoader _loader;
        private readonly Func<string, string> _discovery;
        private RoslynDevSpaceState _state = RoslynDevSpaceState.Unavailable;
        private string _failureReason = string.Empty;
        private string _workspacePath = string.Empty;
        private Task _initializationTask;
        private IRoslynLoadedWorkspace _loadedWorkspace;
        private IReadOnlyList<RoslynUnusedCodeItem> _unusedCode = Array.Empty<RoslynUnusedCodeItem>();
        private bool _isAnalyzingUnusedCode;
        private string _unusedCodeFailureReason = string.Empty;
        private bool _disposed;
    }
}
