using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;

using DevBoard.AI.Routing;

namespace DevBoard.AI.Hosting;

public sealed class AIRouterHostService
{
    public static AIRouterHostService Instance { get; } = new();

    public async Task ApplyAsync(AIRouterSettings settings, int port, bool forceRestart = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "AI Router port must be between 1 and 65535.");

        await _gate.WaitAsync();
        try
        {
            if (!settings.Enabled)
            {
                await StopCoreAsync();
                return;
            }

            if (!forceRestart && _application != null && _port == port)
                return;

            await StopCoreAsync();

            var clients = new List<HttpClient>();
            var providers = new List<IAIProvider>();
            try
            {
                foreach (var provider in settings.Providers
                             .Where(x => x.IsActive)
                             .OrderBy(x => x.Priority))
                {
                    provider.Validate();
                    var client = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds),
                    };
                    foreach (var header in provider.ExtraHeaders)
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

                    var apiKey = provider.ApiKey;
                    if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(provider.ApiKeyEnvironment))
                        apiKey = Environment.GetEnvironmentVariable(provider.ApiKeyEnvironment) ?? string.Empty;

                    clients.Add(client);
                    providers.Add(CreateProvider(provider, apiKey, client));
                }

                var options = new AIRouterHostOptions
                {
                    Enabled = settings.Enabled,
                    Port = port,
                    ApiKey = settings.ApiKey,
                };
                var app = AIRouterHost.Build(new AIRouter(providers), options);
                await app.StartAsync();

                _clients = clients;
                _application = app;
                _port = port;
            }
            catch
            {
                foreach (var client in clients)
                    client.Dispose();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IAIProvider CreateProvider(
        AIRouterProviderSettings settings,
        string apiKey,
        HttpClient client)
    {
        var deepSeekCompatible =
            string.Equals(settings.Id, "opencode", StringComparison.OrdinalIgnoreCase) ||
            settings.Id.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase) ||
            settings.BaseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase);

        if (deepSeekCompatible)
        {
            return new DeepSeekCompatibleProvider(
                settings.Id,
                settings.BaseUrl,
                apiKey,
                client,
                settings.DefaultModel,
                settings.Models,
                settings.Mode,
                settings.PassthroughModels);
        }

        return new OpenAICompatibleProvider(
            settings.Id,
            settings.BaseUrl,
            apiKey,
            client,
            settings.DefaultModel,
            settings.Models,
            settings.Mode,
            settings.PassthroughModels);
    }

    private async Task StopCoreAsync()
    {
        if (_application != null)
        {
            try
            {
                await _application.StopAsync();
            }
            finally
            {
                await _application.DisposeAsync();
                _application = null;
                _port = null;
            }
        }

        foreach (var client in _clients)
            client.Dispose();
        _clients.Clear();
    }

    private readonly SemaphoreSlim _gate = new(1, 1);
    private WebApplication _application;
    private List<HttpClient> _clients = [];
    private int? _port;
}
