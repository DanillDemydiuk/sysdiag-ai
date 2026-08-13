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
internal sealed record OllamaGenerateRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("stream")] bool Stream);

/// <summary>
/// Relevant part of the response body. Ollama returns more fields (timings,
/// token counts); only the generated text is mapped.
/// </summary>
internal sealed record OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; init; }
}
