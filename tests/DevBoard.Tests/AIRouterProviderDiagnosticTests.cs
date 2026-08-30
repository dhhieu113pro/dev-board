using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DevBoard.AI.Routing;
using Xunit;

namespace DevBoard.Tests;

public class AIRouterProviderDiagnosticTests
{
    [Fact]
    public async Task TestAsync_ExercisesBothInferenceEndpointsWithTheirRequiredPayloads()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var settings = CreateSettings();
        settings.ApiKey = "secret";
        settings.ExtraHeaders["X-Provider"] = "opencode";

        var results = await AIRouterProviderDiagnostic.TestAsync(settings, http);

        Assert.Collection(handler.Requests,
            request =>
            {
                Assert.Equal("https://example.test/v1/chat/completions", request.Url);
                Assert.Contains("\"model\":\"deepseek-v4-flash-free\"", request.Body);
                Assert.Contains("\"messages\":[{\"role\":\"user\"", request.Body);
                Assert.Equal("secret", request.BearerToken);
                Assert.Equal("opencode", request.ProviderHeader);
            },
            request =>
            {
                Assert.Equal("https://example.test/v1/responses", request.Url);
                Assert.Contains("\"model\":\"deepseek-v4-flash-free\"", request.Body);
                Assert.Contains("\"input\":\"Reply with OK.\"", request.Body);
                Assert.Equal("secret", request.BearerToken);
                Assert.Equal("opencode", request.ProviderHeader);
            });
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
            var handler = new RecordingHandler(HttpStatusCode.OK, HttpStatusCode.OK);
            using var http = new HttpClient(handler);
            var settings = CreateSettings();
            settings.ApiKeyEnvironment = variable;

            await AIRouterProviderDiagnostic.TestAsync(settings, http);

            Assert.All(handler.Requests, request => Assert.Equal("environment-secret", request.BearerToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task TestAsync_ReportsAuthorizationFailureForEachInferenceEndpoint()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        using var http = new HttpClient(handler);

        var results = await AIRouterProviderDiagnostic.TestAsync(CreateSettings(), http);

        Assert.Equal(
            "Chat Completions: Authorization failed (401) - run /login; Responses: Authorization failed (403) - run /login",
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
    public async Task TestAsync_StillTestsResponsesWhenChatCompletionsCannotConnect()
    {
        var handler = new FailingChatHandler();
        using var http = new HttpClient(handler);

        var results = await AIRouterProviderDiagnostic.TestAsync(CreateSettings(), http);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(
            "Chat Completions: Unavailable - connection failed; Responses: Healthy",
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
        public RecordingHandler(params HttpStatusCode[] statusCodes)
        {
            _statusCodes = statusCodes;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri?.ToString(),
                await request.Content!.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-Provider", out var values) ? string.Join(",", values) : null));
            var status = _statusCodes[Requests.Count - 1];
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }

        private readonly HttpStatusCode[] _statusCodes;
    }

    private sealed record RecordedRequest(string? Url, string Body, string? BearerToken, string? ProviderHeader);

    private sealed class FailingChatHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri?.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal) == true)
                throw new HttpRequestException("connection failed");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }
}
