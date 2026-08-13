using System.Text.Json.Serialization;

namespace SysDiag.Llm;

/// <summary>
/// Request body of Ollama's /api/generate endpoint.
/// </summary>
/// <param name="Model">Model tag, for example "llama3.2:3b".</param>
/// <param name="Prompt">The full prompt text.</param>
/// <param name="Stream">
/// Always false here: the CLI prints one finished answer, so there is no reason
/// to consume a token stream.
/// </param>
/// <param name="Options">Sampling settings, see <see cref="OllamaSamplingOptions"/>.</param>
internal sealed record OllamaGenerateRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("options")] OllamaSamplingOptions Options);

/// <summary>
/// Sampling settings for the model.
/// </summary>
/// <remarks>
/// The task is to retell measured values, not to write prose, so the sampling is
/// kept close to deterministic. With the default temperature the model invented
/// percentages that were not in the prompt.
/// </remarks>
internal sealed record OllamaSamplingOptions
{
    public static readonly OllamaSamplingOptions Factual = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; init; } = 0.2;

    [JsonPropertyName("top_p")]
    public double TopP { get; init; } = 0.9;
}

/// <summary>
/// Relevant part of the response body. Ollama returns more fields (timings,
/// token counts); only the generated text is mapped.
/// </summary>
internal sealed record OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; init; }
}
