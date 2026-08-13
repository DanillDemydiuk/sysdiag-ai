namespace SysDiag.Llm;

/// <summary>
/// Connection settings for the local Ollama instance. Mutable properties with
/// setters on purpose: this type is filled by the configuration binder from
/// appsettings.json.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>Section name in appsettings.json.</summary>
    public const string SectionName = "Ollama";

    public const string DefaultBaseUrl = "http://localhost:11434";
    public const string DefaultModel = "llama3.2:3b";
    public const int DefaultTimeoutSeconds = 90;

    /// <summary>Base address of the Ollama HTTP API.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Model tag as shown by "ollama list", for example "llama3.2:3b".</summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// Time budget for one request. A small model on a CPU-only machine needs
    /// noticeably longer than on a GPU, so this is generous by default.
    /// </summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>Language the model is asked to answer in.</summary>
    public string ResponseLanguage { get; set; } = "German";
}
