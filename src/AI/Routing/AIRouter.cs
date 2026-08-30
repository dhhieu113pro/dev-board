using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.AI.Routing;

public sealed record AIRouterRequest(string Model, string Payload, string Path = null);

public sealed record AIRouterResult(
    bool Success,
    int StatusCode,
    string ProviderId,
    string Model,
    string Payload,
    string Error = null);

public interface IAIProvider
{
    string Id { get; }
    string DefaultModel => string.Empty;
    IReadOnlyList<string> Models => Array.Empty<string>();
    string Mode => "fallback";
    bool PassthroughModels => false;

    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Models);

    Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default);
}

public sealed class AIRouter
{
    public AIRouter(IEnumerable<IAIProvider> providers)
    {
        _providers = providers?.ToArray() ?? [];
    }

    public async Task<AIRouterResult> RouteAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var responsesRequest = IsResponsesPath(request.Path);
        var providerRequest = responsesRequest
            ? request with
            {
                Payload = ConvertResponsesToChatPayload(request.Payload),
                Path = ChatCompletionsPath,
            }
            : request;

        var selection = Resolve(request.Model);
        var candidates = GetCandidateProviders(selection.ProviderId);
        if (candidates.Count == 0)
        {
            return new AIRouterResult(
                false,
                400,
                selection.ProviderId ?? string.Empty,
                selection.Model ?? string.Empty,
                null,
                $"Unknown AI provider '{selection.ProviderId}'.");
        }

        // Explicit provider/model requests are intentionally strict. A model-specific
        // failure must be returned as-is rather than silently changing the requested model.
        if (!selection.TryAllModels)
        {
            var provider = candidates[0];
            var result = await provider.SendAsync(
                providerRequest with { Model = selection.Model ?? string.Empty },
                cancellationToken);
            return TranslateResponsesIfNeeded(result, responsesRequest, request.Model);
        }

        AIRouterResult last = null;

        // Match AI Studio's provider ordering semantics: priority is already applied by
        // the host, fallback providers are tried first, then round-robin providers.
        var fallbackProviders = candidates
            .Where(provider => !string.Equals(provider.Mode, "roundrobin", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var roundRobinProviders = candidates
            .Where(provider => string.Equals(provider.Mode, "roundrobin", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var provider in fallbackProviders)
        {
            var result = await TryProviderModelsAsync(provider, providerRequest, cancellationToken);
            if (result.Success)
                return TranslateResponsesIfNeeded(result, responsesRequest, request.Model);
            last = result;
        }

        if (roundRobinProviders.Length > 0)
        {
            var index = Interlocked.Increment(ref _roundRobinIndex);
            var start = PositiveModulo(index, roundRobinProviders.Length);
            for (var offset = 0; offset < roundRobinProviders.Length; offset++)
            {
                var provider = roundRobinProviders[(start + offset) % roundRobinProviders.Length];
                var result = await TryProviderModelsAsync(provider, providerRequest, cancellationToken);
                if (result.Success)
                    return TranslateResponsesIfNeeded(result, responsesRequest, request.Model);
                last = result;
            }
        }

        return last ?? new AIRouterResult(
            false,
            503,
            string.Empty,
            selection.Model ?? string.Empty,
            null,
            "No AI providers are available.");
    }

    private async Task<AIRouterResult> TryProviderModelsAsync(
        IAIProvider provider,
        AIRouterRequest request,
        CancellationToken cancellationToken)
    {
        var models = await GetProviderModelsAsync(provider, cancellationToken);
        if (models.Count == 0)
        {
            return new AIRouterResult(
                false,
                503,
                provider.Id,
                string.Empty,
                null,
                $"Provider '{provider.Id}' has no available models.");
        }

        IReadOnlyList<string> modelsToTry = models;
        if (models.Count > 1 && string.Equals(provider.Mode, "roundrobin", StringComparison.OrdinalIgnoreCase))
        {
            var index = _providerModelRoundRobinIndices.AddOrUpdate(provider.Id, 0, static (_, previous) => previous + 1);
            var start = PositiveModulo(index, models.Count);
            var rotated = new string[models.Count];
            for (var i = 0; i < models.Count; i++)
                rotated[i] = models[(start + i) % models.Count];
            modelsToTry = rotated;
        }

        AIRouterResult last = null;
        foreach (var model in modelsToTry)
        {
            // In all/provider-only mode a 400 such as "Model is unavailable" is a
            // model-level failure, not a reason to abandon the provider. AI Studio
            // exhausts the provider's model candidates before moving on.
            var result = await provider.SendAsync(request with { Model = model }, cancellationToken);
            if (result.Success)
                return result;
            last = result;
        }

        return last ?? new AIRouterResult(
            false,
            503,
            provider.Id,
            string.Empty,
            null,
            $"Provider '{provider.Id}' did not return a result.");
    }

    private async Task<IReadOnlyList<string>> GetProviderModelsAsync(
        IAIProvider provider,
        CancellationToken cancellationToken)
    {
        var configured = NormalizeModels(provider.Id, provider.Models);
        if (configured.Count > 0)
            return configured;

        try
        {
            var live = NormalizeModels(provider.Id, await provider.ListModelsAsync(cancellationToken));
            if (live.Count > 0)
                return live;
        }
        catch (HttpRequestException)
        {
            // Fall through to the configured default/provider id, matching AI Studio's
            // best-effort live discovery behavior.
        }

        if (!string.IsNullOrWhiteSpace(provider.DefaultModel) &&
            !string.Equals(provider.DefaultModel, "all", StringComparison.OrdinalIgnoreCase))
            return [StripProviderPrefix(provider.Id, provider.DefaultModel.Trim())];

        return [provider.Id];
    }

    private static IReadOnlyList<string> NormalizeModels(string providerId, IReadOnlyList<string> models)
    {
        if (models == null || models.Count == 0)
            return [];

        return models
            .Where(model => !string.IsNullOrWhiteSpace(model) &&
                            !string.Equals(model.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            .Select(model => StripProviderPrefix(providerId, model.Trim()))
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string StripProviderPrefix(string providerId, string model)
    {
        var prefix = providerId + "/";
        return model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? model[prefix.Length..]
            : model;
    }

    private IReadOnlyList<IAIProvider> GetCandidateProviders(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return _providers;

        var normalized = string.Equals(providerId, "oc", StringComparison.OrdinalIgnoreCase)
            ? "opencode"
            : providerId;
        return _providers
            .Where(provider => string.Equals(provider.Id, normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static AIRouterResult TranslateResponsesIfNeeded(
        AIRouterResult result,
        bool responsesRequest,
        string requestedModel)
    {
        if (!result.Success || !responsesRequest)
            return result;

        return result with
        {
            Payload = ConvertChatToResponsesPayload(result.Payload, requestedModel),
        };
    }

    private static int PositiveModulo(int value, int modulus) =>
        ((value % modulus) + modulus) % modulus;

    private static RouteSelection Resolve(string value)
    {
        var model = value?.Trim() ?? string.Empty;
        if (string.Equals(model, "all", StringComparison.OrdinalIgnoreCase))
            return new RouteSelection(null, null, true);

        var slash = model.IndexOf('/');
        if (slash < 0)
        {
            var providerId = string.Equals(model, "oc", StringComparison.OrdinalIgnoreCase)
                ? "opencode"
                : model;
            return new RouteSelection(providerId, null, true);
        }

        var provider = model[..slash];
        if (string.Equals(provider, "oc", StringComparison.OrdinalIgnoreCase))
            provider = "opencode";
        return new RouteSelection(provider, model[(slash + 1)..], false);
    }

    private static string ConvertResponsesToChatPayload(string payload)
    {
        var source = JsonNode.Parse(payload) as JsonObject
            ?? throw new JsonException("Responses API payload must be a JSON object.");

        var chat = new JsonObject();
        CopyProperty(source, chat, "model");

        var messages = new JsonArray();
        if (TryGetString(source["instructions"], out var instructions) && !string.IsNullOrWhiteSpace(instructions))
            messages.Add(CreateMessage("system", JsonValue.Create(instructions)));

        AddResponseInputMessages(source["input"], messages);
        chat["messages"] = messages;

        CopyProperty(source, chat, "temperature");
        CopyProperty(source, chat, "top_p");
        CopyProperty(source, chat, "stream");
        CopyProperty(source, chat, "user");
        CopyProperty(source, chat, "stop");
        CopyProperty(source, chat, "seed");
        CopyProperty(source, chat, "tool_choice");

        if (source["max_output_tokens"] != null)
            chat["max_tokens"] = source["max_output_tokens"].DeepClone();

        if (source["tools"] is JsonArray responseTools)
            chat["tools"] = ConvertResponsesTools(responseTools);

        return chat.ToJsonString();
    }

    private static void AddResponseInputMessages(JsonNode input, JsonArray messages)
    {
        if (input == null)
            return;

        if (TryGetString(input, out var text))
        {
            messages.Add(CreateMessage("user", JsonValue.Create(text)));
            return;
        }

        if (input is not JsonArray inputItems)
        {
            messages.Add(CreateMessage("user", JsonValue.Create(input.ToJsonString())));
            return;
        }

        foreach (var item in inputItems)
        {
            if (item is not JsonObject inputItem)
            {
                if (TryGetString(item, out var itemText))
                    messages.Add(CreateMessage("user", JsonValue.Create(itemText)));
                continue;
            }

            var type = GetString(inputItem["type"]);
            if (string.Equals(type, "input_text", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetString(inputItem["text"], out var partText))
                    messages.Add(CreateMessage("user", JsonValue.Create(partText)));
                continue;
            }

            var role = GetString(inputItem["role"]);
            if (string.IsNullOrWhiteSpace(role))
                role = "user";

            var content = NormalizeResponsesContent(inputItem["content"]);
            if (content != null)
                messages.Add(CreateMessage(role, content));
        }
    }

    private static JsonNode NormalizeResponsesContent(JsonNode content)
    {
        if (content == null)
            return null;

        if (TryGetString(content, out var text))
            return JsonValue.Create(text);

        if (content is not JsonArray parts)
            return content.DeepClone();

        var textParts = new List<string>();
        foreach (var part in parts)
        {
            if (part is JsonObject partObject && TryGetString(partObject["text"], out var partText))
                textParts.Add(partText);
        }

        return textParts.Count > 0
            ? JsonValue.Create(string.Join("\n", textParts))
            : content.DeepClone();
    }

    private static JsonArray ConvertResponsesTools(JsonArray tools)
    {
        var converted = new JsonArray();
        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObject ||
                !string.Equals(GetString(toolObject["type"]), "function", StringComparison.OrdinalIgnoreCase) ||
                toolObject["function"] != null)
            {
                converted.Add(tool?.DeepClone());
                continue;
            }

            var function = new JsonObject();
            CopyProperty(toolObject, function, "name");
            CopyProperty(toolObject, function, "description");
            CopyProperty(toolObject, function, "parameters");
            CopyProperty(toolObject, function, "strict");
            converted.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = function,
            });
        }

        return converted;
    }

    private static string ConvertChatToResponsesPayload(string payload, string requestedModel)
    {
        var chat = JsonNode.Parse(payload) as JsonObject
            ?? throw new JsonException("Chat Completions response must be a JSON object.");

        var outputs = new JsonArray();
        var choices = chat["choices"] as JsonArray;
        var firstChoice = choices?.Count > 0 ? choices[0] as JsonObject : null;
        var message = firstChoice?["message"] as JsonObject;
        var toolCalls = message?["tool_calls"] as JsonArray;

        if (toolCalls?.Count > 0)
        {
            foreach (var toolCall in toolCalls)
            {
                if (toolCall is not JsonObject toolCallObject)
                    continue;

                var function = toolCallObject["function"] as JsonObject;
                outputs.Add(new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = $"fc_{Guid.NewGuid():N}",
                    ["call_id"] = GetString(toolCallObject["id"]) ?? string.Empty,
                    ["status"] = "completed",
                    ["name"] = GetString(function?["name"]) ?? string.Empty,
                    ["arguments"] = GetString(function?["arguments"]) ?? "{}",
                });
            }
        }
        else
        {
            outputs.Add(new JsonObject
            {
                ["type"] = "message",
                ["id"] = $"msg_{Guid.NewGuid():N}",
                ["status"] = "completed",
                ["role"] = "assistant",
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "output_text",
                        ["text"] = ExtractChatText(message?["content"]),
                        ["annotations"] = new JsonArray(),
                    },
                },
            });
        }

        var usage = chat["usage"] as JsonObject;
        var response = new JsonObject
        {
            ["id"] = $"resp_{Guid.NewGuid():N}",
            ["object"] = "response",
            ["created_at"] = chat["created"]?.DeepClone() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["status"] = "completed",
            ["model"] = requestedModel,
            ["output"] = outputs,
            ["parallel_tool_calls"] = true,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = CloneNumberOrZero(usage?["prompt_tokens"]),
                ["output_tokens"] = CloneNumberOrZero(usage?["completion_tokens"]),
                ["total_tokens"] = CloneNumberOrZero(usage?["total_tokens"]),
            },
        };

        return response.ToJsonString();
    }

    private static JsonNode CloneNumberOrZero(JsonNode value) => value?.DeepClone() ?? JsonValue.Create(0);

    private static string ExtractChatText(JsonNode content)
    {
        if (content == null)
            return string.Empty;
        if (TryGetString(content, out var text))
            return text;
        if (content is not JsonArray parts)
            return content.ToJsonString();

        var textParts = new List<string>();
        foreach (var part in parts)
        {
            if (part is JsonObject partObject && TryGetString(partObject["text"], out var partText))
                textParts.Add(partText);
        }
        return string.Join("\n", textParts);
    }

    private static JsonObject CreateMessage(string role, JsonNode content) => new()
    {
        ["role"] = role,
        ["content"] = content,
    };

    private static void CopyProperty(JsonObject source, JsonObject target, string propertyName)
    {
        if (source[propertyName] != null)
            target[propertyName] = source[propertyName].DeepClone();
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

    private static string GetString(JsonNode node) => TryGetString(node, out var value) ? value : null;

    private static bool IsResponsesPath(string path) =>
        string.Equals(path, ResponsesPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, ResponseAliasPath, StringComparison.OrdinalIgnoreCase);

    private sealed record RouteSelection(string ProviderId, string Model, bool TryAllModels);

    private const string ChatCompletionsPath = "/v1/chat/completions";
    private const string ResponsesPath = "/v1/responses";
    private const string ResponseAliasPath = "/v1/response";
    private readonly IReadOnlyList<IAIProvider> _providers;
    private readonly ConcurrentDictionary<string, int> _providerModelRoundRobinIndices = new();
    private int _roundRobinIndex = -1;
}
