using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevBoard.AI.Routing;
using Xunit;

namespace DevBoard.Tests;

public class AIRouterProviderDiagnosticTests
{
    [Fact]
    public async Task TestAsync_DiscoversModelsThenTestsProviderChatOnceAndReportsResponsesCompatibility()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var settings = CreateSettings();
        settings.ApiKey = "secret";
        settings.ExtraHeaders["X-Provider"] = "opencode";

        var results = await AIRouterProviderDiagnostic.TestAsync(settings, http);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://example.test/v1/models", handler.Requests[0].Url);
        var request = handler.Requests[1];
        Assert.Equal("https://example.test/v1/chat/completions", request.Url);
        Assert.Contains("\"model\":\"deepseek-v4-flash-free\"", request.Body);
        Assert.Contains("\"messages\":[{\"role\":\"user\"", request.Body);
        Assert.Contains("\"thinking\":{\"type\":\"disabled\"}", request.Body);
        Assert.All(handler.Requests, recorded => Assert.Equal("secret", recorded.BearerToken));
        Assert.All(handler.Requests, recorded => Assert.Equal("opencode", recorded.ProviderHeader));

        Assert.Collection(results,
            result =>
            {
                Assert.Equal("Chat Completions", result.Name);
                Assert.Equal("/v1/chat/completions", result.Path);
                Assert.True(result.Success);
            },
            result =>
            {
                Assert.Equal("Responses", result.Name);
                Assert.Equal("/v1/responses", result.Path);
                Assert.True(result.Success);
            });
    }

    [Fact]
    public async Task TestAsync_AllModeFallsBackToNextConfiguredModel_WhenDefaultModelIsUnavailable()
    {
        var handler = new ModelFallbackHandler();
        using var http = new HttpClient(handler);
        var settings = CreateSettings();
        settings.Models = ["deepseek-v4-flash-free", "fallback-model"];

        var results = await AIRouterProviderDiagnostic.TestAsync(settings, http);

        Assert.Equal(["deepseek-v4-flash-free", "fallback-model"], handler.Models);
        Assert.All(results, result => Assert.True(result.Success));
    }

    [Fact]
    public async Task TestAsync_AllModeDiscoversLiveModels_WhenOnlyDefaultModelIsSaved()
    {
        var handler = new LiveModelFallbackHandler();
        using var http = new HttpClient(handler);
        var settings = CreateSettings();
        settings.Models = [];

        var results = await AIRouterProviderDiagnostic.TestAsync(settings, http);

        Assert.Equal(1, handler.ModelsRequestCount);
        Assert.Equal(["deepseek-v4-flash-free", "fallback-model"], handler.Models);
        Assert.All(results, result => Assert.True(result.Success));
    }

    [Fact]
    public async Task TestAsync_UsesApiKeyEnvironmentVariableWhenApiKeyIsEmpty()
    {
        const string variable = "DEVBOARD_TEST_PROVIDER_API_KEY";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "environment-secret");
        try
        {
            var handler = new RecordingHandler(HttpStatusCode.OK);
            using var http = new HttpClient(handler);
            var settings = CreateSettings();
            settings.ApiKeyEnvironment = variable;

            await AIRouterProviderDiagnostic.TestAsync(settings, http);

            Assert.Equal(2, handler.Requests.Count);
            Assert.All(handler.Requests, request => Assert.Equal("environment-secret", request.BearerToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task TestAsync_ReportsAuthorizationFailureForBothLocalInferenceApis()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var http = new HttpClient(handler);

        var results = await AIRouterProviderDiagnostic.TestAsync(CreateSettings(), http);

        Assert.Equal(
            "Chat Completions: Authorization failed (401) - run /login; Responses: Authorization failed (401) - run /login",
            AIRouterProviderDiagnostic.FormatSummary(results, "opencode"));
    }

    [Fact]
    public void FormatSummary_UsesProviderNeutralCredentialGuidanceOutsideOpenCode()
    {
        AIRouterEndpointTestResult[] results =
        [
            new("Responses", "/v1/responses", false, 401, "Unauthorized"),
        ];

        Assert.Equal(
            "Responses: Authorization failed (401) - check credentials",
            AIRouterProviderDiagnostic.FormatSummary(results, "openrouter"));
    }

    [Fact]
    public async Task TestAsync_ReportsProviderErrorBodyForBothLocalInferenceApis()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"message\":\"Model is not available\"}}");
        using var http = new HttpClient(handler);

        var results = await AIRouterProviderDiagnostic.TestAsync(CreateSettings(), http);

        Assert.Equal(
            "Chat Completions: Unavailable (400) - Model is not available; Responses: Unavailable (400) - Model is not available",
            AIRouterProviderDiagnostic.FormatSummary(results, "opencode"));
    }

    [Fact]
    public async Task TestAsync_WhenModelDiscoveryAndChatCannotConnect_ReportsBothLocalApisUnavailable()
    {
        var handler = new FailingChatHandler();
        using var http = new HttpClient(handler);

        var results = await AIRouterProviderDiagnostic.TestAsync(CreateSettings(), http);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(
            "Chat Completions: Unavailable - connection failed; Responses: Unavailable - connection failed",
            AIRouterProviderDiagnostic.FormatSummary(results));
    }

    private static AIRouterProviderSettings CreateSettings() => new()
    {
        Id = "opencode",
        Name = "OpenCode",
        BaseUrl = "https://example.test/v1",
        DefaultModel = "deepseek-v4-flash-free",
        TimeoutSeconds = 120,
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public RecordingHandler(HttpStatusCode statusCode, string responseBody = "{}")
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri?.ToString(),
                request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-Provider", out var values) ? string.Join(",", values) : null));
            return new HttpResponseMessage(_statusCode) { Content = new StringContent(_responseBody) };
        }

        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
    }

    private sealed class ModelFallbackHandler : HttpMessageHandler
    {
        public List<string> Models { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[]}")
                };
            }

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            var model = json.RootElement.GetProperty("model").GetString() ?? string.Empty;
            Models.Add(model);
            var status = model == "deepseek-v4-flash-free" ? HttpStatusCode.BadRequest : HttpStatusCode.OK;
            var responseBody = status == HttpStatusCode.OK
                ? "{\"id\":\"ok\"}"
                : "{\"error\":{\"message\":\"Model is unavailable\"}}";
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class LiveModelFallbackHandler : HttpMessageHandler
    {
        public int ModelsRequestCount { get; private set; }
        public List<string> Models { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                ModelsRequestCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[{\"id\":\"deepseek-v4-flash-free\"},{\"id\":\"fallback-model\"}]}")
                };
            }

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            var model = json.RootElement.GetProperty("model").GetString() ?? string.Empty;
            Models.Add(model);
            var status = model == "deepseek-v4-flash-free" ? HttpStatusCode.BadRequest : HttpStatusCode.OK;
            var responseBody = status == HttpStatusCode.OK
                ? "{\"id\":\"ok\"}"
                : "{\"error\":{\"message\":\"Model is unavailable\"}}";
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }

    private sealed record RecordedRequest(string? Url, string Body, string? BearerToken, string? ProviderHeader);

    private sealed class FailingChatHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new HttpRequestException("connection failed");
        }
    }
}
