using SysDiag.Llm;

namespace SysDiag.Cli.Configuration;

/// <summary>
/// Everything that can be configured without recompiling: where the database
/// lives and how to reach the local model.
/// </summary>
public sealed class AppSettings
{
    public const string FileName = "appsettings.json";

    /// <summary>
    /// Name of the optional, git-ignored override file. It lets a user point the
    /// tool at another database without editing a tracked file.
    /// </summary>
    public const string LocalFileName = "appsettings.local.json";

    /// <summary>
    /// Path of the SQLite file. A relative path is resolved against the current
    /// working directory, which is what a CLI user expects.
    /// </summary>
    public string DatabasePath { get; set; } = "data/sysdiag.db";

    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>Absolute path of the database file.</summary>
    public string ResolveDatabasePath() => Path.GetFullPath(DatabasePath);
}
