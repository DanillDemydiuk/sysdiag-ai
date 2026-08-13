using System.Net;
using System.Text;
using FluentAssertions;
using SysDiag.Core.Models;
using SysDiag.Llm;

namespace SysDiag.Tests;

/// <summary>
/// Tests for the LLM client. They run entirely without Ollama: the HTTP layer is
/// replaced by a stub handler, which is the only way to test the failure paths -
/// a real server cannot be asked to be unreachable on demand.
/// </summary>
public sealed class OllamaClientTests
{
    private static readonly OllamaOptions Options = new()
    {
        BaseUrl = "http://localhost:11434",
        Model = "llama3.2:3b",
        TimeoutSeconds = 5,
    };

    [Fact]
    public async Task ExplainAsync_WithAnswer_ReturnsSuccess()
    {
        using var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, """{"response":"Der Rechner ist gesund."}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);

        ExplanationResult result = await client.ExplainAsync(TestData.Snapshot());

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("Der Rechner ist gesund.");
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task ExplainAsync_PostsPromptToGenerateEndpoint()
    {
        HttpRequestMessage? captured = null;
        using var handler = new StubHandler(request =>
        {
            captured = request;
            return JsonResponse(HttpStatusCode.OK, """{"response":"ok"}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);

        await client.ExplainAsync(TestData.Snapshot());

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsolutePath.Should().Be("/api/generate");
    }

    [Fact]
    public async Task ExplainAsync_ServerUnreachable_ReturnsUnavailableInsteadOfThrowing()
    {
        using var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);

        ExplanationResult result = await client.ExplainAsync(TestData.Snapshot());

        result.IsSuccess.Should().BeFalse();
        result.Text.Should().BeNull();
        result.FailureReason.Should().Contain("not reachable");
        // The hint has to name the way out, otherwise the message is useless.
        result.FailureReason.Should().Contain("docker compose up -d");
    }

    [Fact]
    public async Task ExplainAsync_ModelNotPulled_ReportsTheStatusCode()
    {
        using var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.NotFound, """{"error":"model not found"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);

        ExplanationResult result = await client.ExplainAsync(TestData.Snapshot());

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("HTTP 404");
        result.FailureReason.Should().Contain("llama3.2:3b");
    }

    [Fact]
    public async Task ExplainAsync_MalformedJson_ReturnsUnavailable()
    {
        using var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "this is not json"));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);

        ExplanationResult result = await client.ExplainAsync(TestData.Snapshot());

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("unexpected format");
    }

    [Fact]
    public async Task ExplainAsync_EmptyAnswer_ReturnsUnavailable()
    {
        using var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, """{"response":"   "}"""));
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);

        ExplanationResult result = await client.ExplainAsync(TestData.Snapshot());

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("empty answer");
    }

    [Fact]
    public async Task ExplainAsync_InvalidBaseUrl_ReturnsUnavailableWithoutCallingTheServer()
    {
        bool called = false;
        using var handler = new StubHandler(_ =>
        {
            called = true;
            return JsonResponse(HttpStatusCode.OK, """{"response":"ok"}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, new OllamaOptions { BaseUrl = "not a url" });

        ExplanationResult result = await client.ExplainAsync(TestData.Snapshot());

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Contain("not a valid URL");
        called.Should().BeFalse();
    }

    [Fact]
    public async Task ExplainAsync_CallerCancels_PropagatesTheCancellation()
    {
        using var handler = new StubHandler(_ => throw new OperationCanceledException());
        using var httpClient = new HttpClient(handler);
        var client = new OllamaClient(httpClient, Options);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> call = () => client.ExplainAsync(TestData.Snapshot(), cancellation.Token);

        // Ctrl+C must stay Ctrl+C: only the internal timeout is converted into a
        // friendly result.
        await call.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    /// <summary>
    /// Minimal HttpMessageHandler that answers with whatever the test decides.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responder(request));
        }
    }
}
