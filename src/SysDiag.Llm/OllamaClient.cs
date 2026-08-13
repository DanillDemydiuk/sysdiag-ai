using System.Net.Http.Json;
using System.Text.Json;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Diff;
using SysDiag.Core.Models;

namespace SysDiag.Llm;

/// <summary>
/// Talks to a local Ollama server over HTTP.
/// </summary>
/// <remarks>
/// Everything in this class is built around one rule from the requirements: an
/// unreachable model must never take the program down. Every failure path ends
/// in <see cref="ExplanationResult.Unavailable"/> with a sentence the user can
/// act on, and no stack trace ever reaches the console.
/// </remarks>
public sealed class OllamaClient : IExplanationService
{
    private const string GenerateEndpoint = "api/generate";

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    /// <summary>
    /// The <see cref="HttpClient"/> is injected instead of created here: that is
    /// what allows the tests to answer requests with a fake handler, without a
    /// container and without a network.
    /// </summary>
    public OllamaClient(HttpClient httpClient, OllamaOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
    }

    public Task<ExplanationResult> ExplainAsync(
        SystemSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return GenerateAsync(PromptBuilder.Build(snapshot, _options.ResponseLanguage), cancellationToken);
    }

    public Task<ExplanationResult> ExplainDiffAsync(
        SnapshotDiff diff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diff);

        return GenerateAsync(PromptBuilder.BuildForDiff(diff, _options.ResponseLanguage), cancellationToken);
    }

    /// <summary>
    /// Sends one prompt and maps every possible outcome to a result. Both public
    /// methods share it, so the failure handling cannot drift apart.
    /// </summary>
    private async Task<ExplanationResult> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            return ExplanationResult.Unavailable(
                $"The configured Ollama address '{_options.BaseUrl}' is not a valid URL.");
        }

        var requestBody = new OllamaGenerateRequest(
            _options.Model,
            prompt,
            Stream: false,
            OllamaSamplingOptions.Factual);

        // A dedicated timeout per request, linked to the caller's token: Ctrl+C
        // still cancels immediately, but a hanging model gives up on its own.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync(new Uri(baseUri, GenerateEndpoint), requestBody, timeout.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ExplanationResult.Unavailable(
                    $"Ollama answered with HTTP {(int)response.StatusCode}. Is the model '{_options.Model}' pulled?");
            }

            OllamaGenerateResponse? payload = await response.Content
                .ReadFromJsonAsync<OllamaGenerateResponse>(timeout.Token)
                .ConfigureAwait(false);

            string? text = payload?.Response?.Trim();

            return string.IsNullOrEmpty(text)
                ? ExplanationResult.Unavailable("Ollama returned an empty answer.")
                : ExplanationResult.Success(text);
        }
        catch (HttpRequestException exception)
        {
            return ExplanationResult.Unavailable(
                $"Ollama is not reachable at {_options.BaseUrl} ({exception.Message}). Start it with: docker compose up -d");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the internal timeout fired; a user-initiated cancellation is
            // rethrown so Ctrl+C keeps its usual meaning.
            return ExplanationResult.Unavailable(
                $"Ollama did not answer within {_options.TimeoutSeconds} seconds. A smaller model may help.");
        }
        catch (JsonException)
        {
            return ExplanationResult.Unavailable("Ollama returned a response in an unexpected format.");
        }
    }
}
