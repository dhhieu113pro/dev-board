using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using SourceGit.DevSpaces.Roslyn;

namespace SourceGit.ViewModels
{
    public sealed class RoslynSymbolCommandPalette : ICommandPalette, IDisposable
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    QueueSearch();
            }
        }

        public List<RoslynSymbolSearchResult> VisibleSymbols
        {
            get => _visibleSymbols;
            private set => SetProperty(ref _visibleSymbols, value);
        }

        public RoslynSymbolSearchResult SelectedSymbol
        {
            get => _selectedSymbol;
            set => SetProperty(ref _selectedSymbol, value);
        }

        public RoslynSymbolCommandPalette(string repo)
        {
            _repo = repo;
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void Launch()
        {
            var selected = _selectedSymbol;
            if (selected == null)
                return;

            Close();
            Native.OS.OpenWithDefaultEditor(GetAbsolutePath(selected.FilePath));
        }

        public void Dispose()
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
        }

        private void QueueSearch()
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = new CancellationTokenSource();
            var cancellationToken = _searchCancellation.Token;
            var query = _filter.Trim();

            if (string.IsNullOrEmpty(query))
            {
                IsLoading = false;
                VisibleSymbols = [];
                SelectedSymbol = null;
                return;
            }

            IsLoading = true;
            _ = SearchAsync(query, cancellationToken);
        }

        private async Task SearchAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(180, cancellationToken).ConfigureAwait(false);
                var results = await RoslynSymbolSearchSessions.SearchAsync(_repo, query, cancellationToken).ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    IsLoading = false;
                    VisibleSymbols = new List<RoslynSymbolSearchResult>(results);
                    SelectedSymbol = VisibleSymbols.Count > 0 ? VisibleSymbols[0] : null;
                });
            }
            catch (OperationCanceledException)
            {
                // A newer query superseded this one.
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    IsLoading = false;
                    var fallback = new OpenFileCommandPalette(_repo)
                    {
                        Filter = query,
                    };
                    fallback.Open();
                });
            }
        }

        private string GetAbsolutePath(string filePath)
        {
            if (Path.IsPathRooted(filePath))
                return filePath;
            return Native.OS.GetAbsPath(_repo, filePath);
        }

        private readonly string _repo;
        private bool _isLoading;
        private string _filter = string.Empty;
        private List<RoslynSymbolSearchResult> _visibleSymbols = [];
        private RoslynSymbolSearchResult _selectedSymbol;
        private CancellationTokenSource _searchCancellation;
    }
}
