using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.AI.Routing;

public sealed record AIRouterEndpointTestResult(
    string Name,
    string Path,
    bool Success,
    int StatusCode,
    string Error);

public static class AIRouterProviderDiagnostic
{
    public static async Task<IReadOnlyList<AIRouterEndpointTestResult>> TestAsync(
        AIRouterProviderSettings settings,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        settings.Validate();

        var apiKey = settings.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(settings.ApiKeyEnvironment))
            apiKey = Environment.GetEnvironmentVariable(settings.ApiKeyEnvironment);

        foreach (var header in settings.ExtraHeaders)
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

        var provider = CreateProvider(settings, apiKey ?? string.Empty, httpClient);
        var router = new AIRouter([provider]);

        var chat = await TestEndpointAsync(router, "Chat Completions", new AIRouterRequest(
            "all",
            "{\"model\":\"all\",\"messages\":[{\"role\":\"user\",\"content\":\"Reply with OK.\"}],\"max_tokens\":1,\"stream\":false}",
            ChatCompletionsPath), cancellationToken);

        // DevBoard exposes the Responses API as a compatibility layer over Chat Completions,
        // just like AI Studio. Providers therefore only need a working chat-completions
        // endpoint; requiring their own /v1/responses endpoint produces false failures.
        var responses = new AIRouterEndpointTestResult(
            "Responses",
            ResponsesPath,
            chat.Success,
            chat.StatusCode,
            chat.Error);

        return [chat, responses];
    }

    public static string FormatSummary(IEnumerable<AIRouterEndpointTestResult> results, string providerId = null)
    {
        var summaries = new List<string>();
        foreach (var result in results)
        {
            var status = result.Success
                ? "Healthy"
                : result.StatusCode is 401 or 403
                    ? $"Authorization failed ({result.StatusCode}) - {GetAuthorizationGuidance(providerId)}"
                    : result.StatusCode == 0
                        ? $"Unavailable{FormatError(result.Error)}"
                        : $"Unavailable ({result.StatusCode}){FormatError(result.Error)}";
            summaries.Add($"{result.Name}: {status}");
        }

        return string.Join("; ", summaries);
    }

    private static IAIProvider CreateProvider(
        AIRouterProviderSettings settings,
        string apiKey,
        HttpClient httpClient)
    {
        // Treat DefaultModel as configured static metadata for diagnostics so an existing
        // provider health check remains one request when no explicit model list was saved.
        // The embedded host still performs live /models discovery when both are absent.
        IReadOnlyList<string> models = settings.Models.Count > 0
            ? settings.Models
            : !string.IsNullOrWhiteSpace(settings.DefaultModel)
                ? [settings.DefaultModel]
                : [];

        var deepSeekCompatible =
            string.Equals(settings.Id, "opencode", StringComparison.OrdinalIgnoreCase) ||
            settings.Id.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase) ||
            settings.BaseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase);

        return deepSeekCompatible
            ? new DeepSeekCompatibleProvider(
                settings.Id,
                settings.BaseUrl,
                apiKey,
                httpClient,
                settings.DefaultModel,
                models,
                settings.Mode,
                settings.PassthroughModels)
            : new OpenAICompatibleProvider(
                settings.Id,
                settings.BaseUrl,
                apiKey,
                httpClient,
                settings.DefaultModel,
                models,
                settings.Mode,
                settings.PassthroughModels);
    }

    private static async Task<AIRouterEndpointTestResult> TestEndpointAsync(
        AIRouter router,
        string name,
        AIRouterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await router.RouteAsync(request, cancellationToken);
            return new AIRouterEndpointTestResult(name, request.Path, result.Success, result.StatusCode, result.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new AIRouterEndpointTestResult(name, request.Path, false, 0, ex.Message);
        }
    }

    private static string FormatError(string error) => string.IsNullOrWhiteSpace(error) ? string.Empty : $" - {error}";

    private static string GetAuthorizationGuidance(string providerId)
    {
        return string.Equals(providerId, "opencode", StringComparison.OrdinalIgnoreCase)
            ? "run /login"
            : "check credentials";
    }

    private const string ChatCompletionsPath = "/v1/chat/completions";
    private const string ResponsesPath = "/v1/responses";
}
