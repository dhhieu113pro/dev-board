using System;
using System.Collections.Generic;
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

        var provider = new OpenAICompatibleProvider(
            settings.Id,
            settings.BaseUrl,
            apiKey ?? string.Empty,
            httpClient,
            settings.DefaultModel);

        var chat = await TestEndpointAsync(provider, "Chat Completions", new AIRouterRequest(
            "all",
            "{\"model\":\"all\",\"messages\":[{\"role\":\"user\",\"content\":\"Reply with OK.\"}],\"max_tokens\":1,\"stream\":false}",
            ChatCompletionsPath), cancellationToken);
        var responses = await TestEndpointAsync(provider, "Responses", new AIRouterRequest(
            "all",
            "{\"model\":\"all\",\"input\":\"Reply with OK.\",\"max_output_tokens\":1,\"stream\":false}",
            ResponsesPath), cancellationToken);

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

    private static async Task<AIRouterEndpointTestResult> TestEndpointAsync(
        OpenAICompatibleProvider provider,
        string name,
        AIRouterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.SendAsync(request, cancellationToken);
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
