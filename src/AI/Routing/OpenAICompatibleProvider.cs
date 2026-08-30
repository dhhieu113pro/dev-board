using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.AI.Routing;

public sealed class OpenAICompatibleProvider : IAIProvider
{
    public OpenAICompatibleProvider(
        string id,
        string baseUrl,
        string apiKey,
        HttpClient httpClient,
        string defaultModel = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Provider id is required.", nameof(id)) : id.Trim();
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? throw new ArgumentException("Provider base URL is required.", nameof(baseUrl)) : baseUrl.TrimEnd('/');
        _apiKey = apiKey ?? string.Empty;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _defaultModel = defaultModel?.Trim();
    }

    public string Id { get; }

    public async Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        var path = string.IsNullOrWhiteSpace(request.Path) ? "/v1/chat/completions" : request.Path;
        if (!path.StartsWith('/'))
            path = "/" + path;

        var payload = RewritePayload(request.Payload, model, path);
        using var message = new HttpRequestMessage(HttpMethod.Post, _baseUrl + NormalizePath(path));
        message.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        if (!string.IsNullOrWhiteSpace(_apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;
        return new AIRouterResult(
            response.IsSuccessStatusCode,
            statusCode,
            Id,
            model,
            body,
            response.IsSuccessStatusCode ? null : ExtractError(body, response.ReasonPhrase));
    }

    private string ResolveModel(string model)
    {
        if ((string.IsNullOrWhiteSpace(model) || string.Equals(model, "all", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(_defaultModel))
            return _defaultModel;

        return model;
    }

    private string RewritePayload(string payload, string model, string path)
    {
        var node = JsonNode.Parse(payload) as JsonObject ?? throw new System.Text.Json.JsonException("AI request payload must be a JSON object.");
        if (!string.IsNullOrWhiteSpace(model))
            node["model"] = model;

        if (string.Equals(Id, "opencode", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            node["thinking"] = new JsonObject
            {
                ["type"] = "disabled",
            };
        }

        return node.ToJsonString();
    }

    private static string ExtractError(string body, string reasonPhrase)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var root = JsonNode.Parse(body);
                if (root is JsonObject rootObject)
                {
                    if (rootObject["error"] is JsonObject errorObject && TryGetString(errorObject["message"], out var errorMessage))
                        return errorMessage;
                    if (TryGetString(rootObject["error"], out var errorText))
                        return errorText;
                    if (TryGetString(rootObject["message"], out var message))
                        return message;
                    if (TryGetString(rootObject["detail"], out var detail))
                        return detail;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall through to the raw response text.
            }

            var trimmed = body.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                return trimmed;
        }

        return string.IsNullOrWhiteSpace(reasonPhrase) ? "Provider request failed." : reasonPhrase;
    }

    private static bool TryGetString(JsonNode node, out string value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        value = null;
        return false;
    }

    private string NormalizePath(string path)
    {
        if (_baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
            return path[3..];
        return path;
    }

    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _defaultModel;
    private readonly HttpClient _httpClient;
}
