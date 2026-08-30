using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

using DevBoard.AI.Hosting;
using DevBoard.AI.Routing;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceAIRouter : ObservableObject
    {
        public AvaloniaList<AIRouterProviderSettings> Providers { get; } = [];

        public AIRouterProviderSettings SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (SetProperty(ref _selectedProvider, value))
                    StatusText = !IsEnabled ? "Disabled" : value == null ? string.Empty : value.IsActive ? "Ready" : "Disabled";
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (!SetProperty(ref _isEnabled, value))
                    return;

                _ = ApplyEnabledStateFromBindingAsync(value);
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                if (SetProperty(ref _port, value))
                    OnPropertyChanged(nameof(Endpoint));
            }
        }

        public string Endpoint => $"http://127.0.0.1:{Port}/v1";

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public DevSpaceAIRouter()
        {
            var settings = AIRouterSettings.Instance;
            _isEnabled = settings.Enabled;
            _port = GetConfiguredPort(settings.ListenUrl);
            foreach (var provider in settings.Providers)
                Providers.Add(provider.Clone());

            SelectedProvider = Providers.FirstOrDefault();
            _ = EnsureRouterStartedAsync();
        }

        public AIRouterProviderSettings AddProvider()
        {
            var provider = new AIRouterProviderSettings
            {
                Id = $"provider-{Providers.Count + 1}",
                Name = "New Provider",
                BaseUrl = "http://127.0.0.1:5032/v1",
            };
            EnsureUniqueId(provider);
            Providers.Add(provider);
            SelectedProvider = provider;
            return provider;
        }

        public AIRouterProviderSettings DuplicateSelected()
        {
            if (SelectedProvider == null)
                return null;

            var copy = SelectedProvider.Clone(createNewId: true);
            copy.Name = $"{SelectedProvider.Name} Copy";
            copy.Id = $"{SelectedProvider.Id}-copy";
            EnsureUniqueId(copy);
            Providers.Add(copy);
            SelectedProvider = copy;
            Save();
            return copy;
        }

        public void DeleteSelected()
        {
            if (SelectedProvider == null)
                return;

            var index = Providers.IndexOf(SelectedProvider);
            Providers.Remove(SelectedProvider);
            SelectedProvider = Providers.Count == 0 ? null : Providers[Math.Min(index, Providers.Count - 1)];
            Save();
        }

        public void ToggleSelected()
        {
            if (SelectedProvider == null)
                return;

            SelectedProvider.IsActive = !SelectedProvider.IsActive;
            StatusText = !IsEnabled ? "Disabled" : SelectedProvider.IsActive ? "Ready" : "Disabled";
            Save();
            OnPropertyChanged(nameof(SelectedProvider));
        }

        public void Save()
        {
            ValidatePort(Port);
            foreach (var provider in Providers)
                provider.Validate();

            var ids = Providers.Select(x => x.Id).ToArray();
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                throw new ArgumentException("AI Router provider IDs must be unique.");

            var settings = AIRouterSettings.Instance;
            settings.Enabled = IsEnabled;
            settings.ListenUrl = $"http://127.0.0.1:{Port}";
            settings.Providers = Providers.Select(x => x.Clone()).ToList();
            settings.Save();
            OnPropertyChanged(nameof(Endpoint));
            StatusText = !IsEnabled ? "Disabled" : SelectedProvider?.IsActive == false ? "Disabled" : "Saved";
        }

        public async Task SaveAndRebindAsync()
        {
            Save();
            await AIRouterHostService.Instance.ApplyAsync(AIRouterSettings.Instance, Port, forceRestart: IsEnabled);
            StatusText = !IsEnabled ? "Disabled" : SelectedProvider?.IsActive == false ? "Disabled" : "Saved";
        }

        public async Task SetEnabledAsync(bool enabled)
        {
            if (_isEnabled != enabled)
            {
                _isEnabled = enabled;
                OnPropertyChanged(nameof(IsEnabled));
            }

            var settings = AIRouterSettings.Instance;
            settings.Enabled = enabled;
            settings.ListenUrl = $"http://127.0.0.1:{Port}";
            settings.Save();

            await AIRouterHostService.Instance.ApplyAsync(settings, Port, forceRestart: enabled);
            StatusText = enabled ? "Running" : "Disabled";
        }

        public async Task ExportAsync(Stream stream, bool includeSecrets = false)
        {
            Save();
            await AIRouterProviderExchange.ExportAsync(Providers, stream, includeSecrets);
            StatusText = "Exported";
        }

        public async Task<int> ImportAsync(Stream stream)
        {
            var imported = await AIRouterProviderExchange.ImportAsync(stream);
            foreach (var provider in imported)
            {
                var existing = Providers.FirstOrDefault(x => string.Equals(x.Id, provider.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    Providers[Providers.IndexOf(existing)] = provider;
                else
                    Providers.Add(provider);
            }

            SelectedProvider = imported.Count > 0
                ? Providers.FirstOrDefault(x => string.Equals(x.Id, imported[^1].Id, StringComparison.OrdinalIgnoreCase))
                : SelectedProvider;
            Save();
            StatusText = $"Imported {imported.Count} provider(s)";
            return imported.Count;
        }

        public async Task TestSelectedAsync()
        {
            if (SelectedProvider == null)
                return;

            await SaveAndRebindAsync();
            var provider = SelectedProvider;
            StatusText = "Testing…";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Min(provider.TimeoutSeconds, 30)) };
                var results = await AIRouterProviderDiagnostic.TestAsync(provider, client);
                StatusText = AIRouterProviderDiagnostic.FormatSummary(results, provider.Id);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                StatusText = "Unavailable";
            }
        }

        internal static int GetConfiguredPort(string listenUrl)
        {
            return Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri) && uri.Port is >= 1 and <= 65535
                ? uri.Port
                : AIRouterHostOptions.DefaultPort;
        }

        internal static void ValidatePort(int port)
        {
            if (port is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "AI Router port must be between 1 and 65535.");
        }

        private async Task ApplyEnabledStateFromBindingAsync(bool enabled)
        {
            try
            {
                await SetEnabledAsync(enabled);
            }
            catch (Exception ex)
            {
                StatusText = $"Unavailable: {ex.Message}";
            }
        }

        private async Task EnsureRouterStartedAsync()
        {
            try
            {
                await AIRouterHostService.Instance.ApplyAsync(AIRouterSettings.Instance, Port);
                StatusText = IsEnabled ? "Running" : "Disabled";
            }
            catch (Exception ex)
            {
                StatusText = $"Unavailable: {ex.Message}";
            }
        }

        private void EnsureUniqueId(AIRouterProviderSettings provider)
        {
            var seed = string.IsNullOrWhiteSpace(provider.Id) ? "provider" : provider.Id.Trim();
            var candidate = seed;
            var suffix = 2;
            while (Providers.Any(x => !ReferenceEquals(x, provider) && string.Equals(x.Id, candidate, StringComparison.OrdinalIgnoreCase)))
                candidate = $"{seed}-{suffix++}";
            provider.Id = candidate;
        }

        private AIRouterProviderSettings _selectedProvider;
        private bool _isEnabled;
        private int _port;
        private string _statusText = string.Empty;
    }
}
