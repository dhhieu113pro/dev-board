using System.Collections.Generic;
using System.Net.Http;

namespace DevBoard.AI.Routing;

/// <summary>
/// AI Studio-compatible provider for DeepSeek and OpenCode endpoints.
/// These providers require thinking mode to be explicitly disabled for the
/// OpenAI-compatible Chat Completions payload used by DevBoard.
/// </summary>
public sealed class DeepSeekCompatibleProvider : OpenAICompatibleProvider
{
    public DeepSeekCompatibleProvider(
        string id,
        string baseUrl,
        string apiKey,
        HttpClient httpClient,
        string defaultModel = null,
        IReadOnlyList<string> models = null,
        string mode = "fallback",
        bool passthroughModels = false)
        : base(id, baseUrl, apiKey, httpClient, defaultModel, models, mode, passthroughModels)
    {
    }

    protected override bool ShouldDisableThinking => true;
}
