using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevBoard.AI.Routing;
using Xunit;

namespace DevBoard.Tests;

public class OpenAICompatibleProviderTests
{
    [Fact]
    public async Task SendAsync_ForwardsEndpointAndResolvedModel()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var provider = new OpenAICompatibleProvider("openrouter", "https://example.test/v1", "secret", http);

        var result = await provider.SendAsync(new AIRouterRequest(
            "anthropic/claude-sonnet",
            "{\"model\":\"openrouter/anthropic/claude-sonnet\",\"input\":\"hello\"}",
            "/v1/responses"));

        Assert.True(result.Success);
        Assert.Equal("https://example.test/v1/responses", handler.LastUrl);
        Assert.Contains("\"model\":\"anthropic/claude-sonnet\"", handler.LastBody);
        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("secret", handler.LastAuthParameter);
    }

    [Fact]
    public async Task SendAsync_UsesProviderDefaultModelForAllRoute()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var provider = new OpenAICompatibleProvider("local", "http://127.0.0.1:5032/v1", "", http, "qwen3-coder");

        await provider.SendAsync(new AIRouterRequest("all", "{\"model\":\"all\",\"messages\":[]}", "/v1/chat/completions"));

        Assert.Contains("\"model\":\"qwen3-coder\"", handler.LastBody);
    }

    [Fact]
    public async Task SendAsync_ReportsProviderJsonErrorMessage()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"message\":\"Model is not available\",\"type\":\"invalid_request_error\"}}");
        using var http = new HttpClient(handler);
        var provider = new OpenAICompatibleProvider(
            "openrouter",
            "https://example.test/v1",
            string.Empty,
            http,
            "model");

        var result = await provider.SendAsync(new AIRouterRequest(
            "all",
            "{\"model\":\"all\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
            "/v1/chat/completions"));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Model is not available", result.Error);
    }

    [Fact]
    public async Task SendAsync_DisablesThinkingForOpenCode()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var provider = new OpenAICompatibleProvider(
            "opencode",
            "https://example.test/v1",
            "secret",
            http,
            "deepseek-v4-flash-free");

        await provider.SendAsync(new AIRouterRequest(
            "all",
            "{\"model\":\"all\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
            "/v1/chat/completions"));

        using var payload = JsonDocument.Parse(handler.LastBody);
        Assert.Equal(
            "disabled",
            payload.RootElement.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("deepseek-v4-flash-free", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("secret", handler.LastAuthParameter);
    }

    [Fact]
    public async Task SendAsync_DoesNotAddThinkingForOtherProviders()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var provider = new OpenAICompatibleProvider(
            "openrouter",
            "https://example.test/v1",
            string.Empty,
            http,
            "model");

        await provider.SendAsync(new AIRouterRequest(
            "all",
            "{\"model\":\"all\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
            "/v1/chat/completions"));

        using var payload = JsonDocument.Parse(handler.LastBody);
        Assert.False(payload.RootElement.TryGetProperty("thinking", out _));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string responseBody = "{\"id\":\"ok\"}")
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public string LastUrl { get; private set; }
        public string LastBody { get; private set; }
        public string LastAuthScheme { get; private set; }
        public string LastAuthParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString();
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastAuthParameter = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };
        }
    }
}
