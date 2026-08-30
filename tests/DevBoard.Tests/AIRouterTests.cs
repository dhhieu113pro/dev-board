using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevBoard.AI.Routing;
using Xunit;

namespace DevBoard.Tests;

public class AIRouterTests
{
    [Fact]
    public async Task RouteAsync_FallsBackToNextProvider_WhenFirstProviderFails()
    {
        var first = new StubProvider("first", false, 503);
        var second = new StubProvider("second", true, 200);
        var router = new AIRouter([first, second]);

        var result = await router.RouteAsync(new AIRouterRequest("all", "{}"));

        Assert.True(result.Success);
        Assert.Equal("second", result.ProviderId);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task RouteAsync_UsesExplicitProviderAndModel()
    {
        var first = new StubProvider("openai", true, 200);
        var second = new StubProvider("openrouter", true, 200);
        var router = new AIRouter([first, second]);

        var result = await router.RouteAsync(new AIRouterRequest("openrouter/anthropic/claude-sonnet", "{}"));

        Assert.True(result.Success);
        Assert.Equal("openrouter", result.ProviderId);
        Assert.Equal("anthropic/claude-sonnet", second.LastModel);
        Assert.Equal(0, first.Calls);
    }

    [Fact]
    public async Task RouteAsync_PreservesRequestedChatCompletionsPath()
    {
        var provider = new StubProvider("openrouter", true, 200);
        var router = new AIRouter([provider]);

        await router.RouteAsync(new AIRouterRequest("openrouter/model", "{}", "/v1/chat/completions"));

        Assert.Equal("/v1/chat/completions", provider.LastPath);
    }

    [Fact]
    public async Task RouteAsync_TranslatesResponsesApiThroughChatCompletions()
    {
        const string upstreamResponse = """
            {
              "id":"chatcmpl-1",
              "object":"chat.completion",
              "created":123,
              "model":"model",
              "choices":[{"index":0,"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}],
              "usage":{"prompt_tokens":4,"completion_tokens":1,"total_tokens":5}
            }
            """;
        var provider = new StubProvider("openrouter", true, 200, upstreamResponse);
        var router = new AIRouter([provider]);
        const string requestPayload = """
            {
              "model":"openrouter/model",
              "instructions":"Be brief.",
              "input":"Reply with OK.",
              "max_output_tokens":5,
              "stream":false
            }
            """;

        var result = await router.RouteAsync(new AIRouterRequest(
            "openrouter/model",
            requestPayload,
            "/v1/responses"));

        Assert.True(result.Success);
        Assert.Equal("/v1/chat/completions", provider.LastPath);
        Assert.Contains("\"messages\"", provider.LastPayload);
        Assert.Contains("\"role\":\"system\"", provider.LastPayload);
        Assert.Contains("\"role\":\"user\"", provider.LastPayload);
        Assert.Contains("\"max_tokens\":5", provider.LastPayload);
        Assert.DoesNotContain("max_output_tokens", provider.LastPayload);

        using var response = JsonDocument.Parse(result.Payload);
        var root = response.RootElement;
        Assert.Equal("response", root.GetProperty("object").GetString());
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("openrouter/model", root.GetProperty("model").GetString());
        Assert.Equal(
            "OK",
            root.GetProperty("output")[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(4, root.GetProperty("usage").GetProperty("input_tokens").GetInt32());
        Assert.Equal(1, root.GetProperty("usage").GetProperty("output_tokens").GetInt32());
        Assert.Equal(5, root.GetProperty("usage").GetProperty("total_tokens").GetInt32());
    }

    [Fact]
    public async Task RouteAsync_AllModeTriesNextConfiguredModel_WhenFirstModelReturns400()
    {
        var provider = new ModelAwareStubProvider(
            "opencode",
            ["deepseek-v4-flash-free", "fallback-model"],
            new Dictionary<string, int>
            {
                ["deepseek-v4-flash-free"] = 400,
                ["fallback-model"] = 200,
            });
        var router = new AIRouter([provider]);

        var result = await router.RouteAsync(new AIRouterRequest("all", "{}"));

        Assert.True(result.Success);
        Assert.Equal("opencode", result.ProviderId);
        Assert.Equal("fallback-model", result.Model);
        Assert.Equal(["deepseek-v4-flash-free", "fallback-model"], provider.ModelsTried);
    }

    [Fact]
    public async Task RouteAsync_BareProviderIdTriesConfiguredModels()
    {
        var provider = new ModelAwareStubProvider(
            "opencode",
            ["deepseek-v4-flash-free", "fallback-model"],
            new Dictionary<string, int>
            {
                ["deepseek-v4-flash-free"] = 400,
                ["fallback-model"] = 200,
            });
        var router = new AIRouter([provider]);

        var result = await router.RouteAsync(new AIRouterRequest("opencode", "{}"));

        Assert.True(result.Success);
        Assert.Equal("fallback-model", result.Model);
        Assert.Equal(["deepseek-v4-flash-free", "fallback-model"], provider.ModelsTried);
    }

    [Fact]
    public async Task RouteAsync_ExplicitProviderAndModelDoesNotSwitchModels_OnClientError()
    {
        var provider = new ModelAwareStubProvider(
            "opencode",
            ["deepseek-v4-flash-free", "fallback-model"],
            new Dictionary<string, int>
            {
                ["deepseek-v4-flash-free"] = 400,
                ["fallback-model"] = 200,
            });
        var router = new AIRouter([provider]);

        var result = await router.RouteAsync(new AIRouterRequest("opencode/deepseek-v4-flash-free", "{}"));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("deepseek-v4-flash-free", result.Model);
        Assert.Equal(["deepseek-v4-flash-free"], provider.ModelsTried);
    }

    private sealed class StubProvider : IAIProvider
    {
        private readonly bool _success;
        private readonly int _statusCode;
        private readonly string _responsePayload;

        public StubProvider(string id, bool success, int statusCode, string responsePayload = "{}")
        {
            Id = id;
            _success = success;
            _statusCode = statusCode;
            _responsePayload = responsePayload;
        }

        public string Id { get; }
        public int Calls { get; private set; }
        public string LastModel { get; private set; } = string.Empty;
        public string LastPath { get; private set; }
        public string LastPayload { get; private set; } = string.Empty;

        public Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastModel = request.Model;
            LastPath = request.Path;
            LastPayload = request.Payload;
            return Task.FromResult(new AIRouterResult(
                _success,
                _statusCode,
                Id,
                request.Model,
                _success ? _responsePayload : null));
        }
    }

    private sealed class ModelAwareStubProvider : IAIProvider
    {
        private readonly IReadOnlyDictionary<string, int> _statusCodes;

        public ModelAwareStubProvider(string id, IReadOnlyList<string> models, IReadOnlyDictionary<string, int> statusCodes)
        {
            Id = id;
            Models = models;
            _statusCodes = statusCodes;
        }

        public string Id { get; }
        public IReadOnlyList<string> Models { get; }
        public List<string> ModelsTried { get; } = [];

        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Models);

        public Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
        {
            ModelsTried.Add(request.Model);
            var statusCode = _statusCodes.TryGetValue(request.Model, out var configured) ? configured : 400;
            var success = statusCode is >= 200 and < 300;
            return Task.FromResult(new AIRouterResult(
                success,
                statusCode,
                Id,
                request.Model,
                success ? "{}" : null,
                success ? null : "Model is unavailable"));
        }
    }
}
