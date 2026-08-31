using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.AI.Routing;

public class OpenAICompatibleProvider : IAIProvider
{
    public OpenAICompatibleProvider(
        string id,
        string baseUrl,
        string apiKey,
        HttpClient httpClient,
        string defaultModel = null,
        IReadOnlyList<string> models = null,
        string mode = "fallback",
        bool passthroughModels = false)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Provider id is required.", nameof(id)) : id.Trim();
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? throw new ArgumentException("Provider base URL is required.", nameof(baseUrl)) : baseUrl.TrimEnd('/');
        _apiKey = apiKey ?? string.Empty;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _defaultModel = defaultModel?.Trim();
        Models = models?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        Mode = string.IsNullOrWhiteSpace(mode) ? "fallback" : mode.Trim();
        PassthroughModels = passthroughModels;
    }

    public string Id { get; }
    public string DefaultModel => _defaultModel ?? string.Empty;
    public IReadOnlyList<string> Models { get; }
    public string Mode { get; }
    public bool PassthroughModels { get; }

    public virtual async Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        var path = string.IsNullOrWhiteSpace(request.Path) ? "/v1/chat/completions" : request.Path;
        if (!path.StartsWith('/'))
            path = "/" + path;

        var payload = RewritePayload(request.Payload, model, path);
        using var message = new HttpRequestMessage(HttpMethod.Post, _baseUrl + NormalizePath(path));
        message.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        ApplyAuthorization(message);

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

    public virtual async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, GetModelsEndpoint());
        ApplyAuthorization(message);

        try
        {
            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseModels(body);
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    protected virtual bool ShouldDisableThinking =>
        string.Equals(Id, "opencode", StringComparison.OrdinalIgnoreCase) ||
        Id.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase) ||
        _baseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase);

    protected virtual string RewritePayload(string payload, string model, string path)
    {
        var node = JsonNode.Parse(payload) as JsonObject ?? throw new JsonException("AI request payload must be a JSON object.");
        if (!string.IsNullOrWhiteSpace(model))
            node["model"] = model;

        if (ShouldDisableThinking && path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            node["thinking"] = new JsonObject
            {
                ["type"] = "disabled",
            };
        }

        return node.ToJsonString();
    }

    private string ResolveModel(string model)
    {
        if ((string.IsNullOrWhiteSpace(model) || string.Equals(model, "all", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(_defaultModel))
            return _defaultModel;

        return model;
    }

    private void ApplyAuthorization(HttpRequestMessage message)
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    private string GetModelsEndpoint()
    {
        var baseUrl = _baseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return $"{baseUrl}/models";

        var chatIndex = baseUrl.IndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase);
        if (chatIndex > 0)
            return baseUrl[..chatIndex] + "/models";

        return $"{baseUrl}/v1/models";
    }

    private static IReadOnlyList<string> ParseModels(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var models = new List<string>();

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            foreach (var model in data.EnumerateArray())
                AddModelId(model, "id", models);
        }
        else if (root.ValueKind == JsonValueKind.Object &&
                 root.TryGetProperty("models", out var ollamaModels) &&
                 ollamaModels.ValueKind == JsonValueKind.Array)
        {
            foreach (var model in ollamaModels.EnumerateArray())
            {
                if (!AddModelId(model, "model", models))
                    AddModelId(model, "name", models);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var model in root.EnumerateArray())
                AddModelId(model, "id", models);
        }

        return models.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool AddModelId(JsonElement model, string propertyName, ICollection<string> models)
    {
        if (model.ValueKind != JsonValueKind.Object ||
            !model.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            return false;

        models.Add(value.GetString()!);
        return true;
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
            catch (JsonException)
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
