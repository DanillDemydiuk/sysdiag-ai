using SysDiag.Cli.Configuration;
using SysDiag.Collectors;
using SysDiag.Core.Abstractions;
using SysDiag.Llm;
using SysDiag.Storage;
using SysDiag.Storage.Export;

namespace SysDiag.Cli;

/// <summary>
/// Composition root: the single place where interfaces are bound to concrete
/// implementations.
/// </summary>
/// <remarks>
/// Wired by hand instead of with a DI container. The application has four
/// dependencies and no lifetimes to manage, so a container would add a package
/// and a layer of indirection without removing a single line of real work.
/// </remarks>
public sealed class AppServices : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cancellation = new();

    private AppServices(AppSettings settings, HttpClient httpClient)
    {
        Settings = settings;
        _httpClient = httpClient;

        Repository = new SqliteSnapshotRepository(settings.ResolveDatabasePath());
        Comparer = new SnapshotComparer();
        Explanations = new OllamaClient(httpClient, settings.Ollama);
        Exporters = [new JsonSnapshotExporter(), new MarkdownSnapshotExporter()];
    }

    public AppSettings Settings { get; }

    public ISnapshotRepository Repository { get; }

    public ISnapshotComparer Comparer { get; }

    public IExplanationService Explanations { get; }

    /// <summary>
    /// All available export formats. A new format is one entry in this list; the
    /// export command itself needs no change.
    /// </summary>
    public IReadOnlyList<ISnapshotExporter> Exporters { get; }

    /// <summary>
    /// Finds an exporter by its format name, case-insensitively, or returns
    /// <c>null</c> when the user asked for a format that does not exist.
    /// </summary>
    public ISnapshotExporter? FindExporter(string formatName) =>
        Exporters.FirstOrDefault(exporter =>
            string.Equals(exporter.FormatName, formatName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Token that every command passes into its asynchronous calls. It is
    /// cancelled when the user presses Ctrl+C, which turns a hard process kill
    /// into an orderly shutdown - relevant while waiting for a slow model.
    /// </summary>
    public CancellationToken Cancellation => _cancellation.Token;

    /// <summary>Requests cancellation of the running command.</summary>
    public void Cancel() => _cancellation.Cancel();

    /// <summary>
    /// Builds the services and makes sure the database schema exists, so every
    /// command can assume a ready database.
    /// </summary>
    public static async Task<AppServices> CreateAsync(CancellationToken cancellationToken = default)
    {
        AppSettings settings = AppSettingsLoader.Load();

        await DatabaseInitializer
            .EnsureCreatedAsync(settings.ResolveDatabasePath(), cancellationToken)
            .ConfigureAwait(false);

        // One HttpClient for the whole process lifetime: creating one per request
        // is the classic way to exhaust sockets.
        var httpClient = new HttpClient();

        return new AppServices(settings, httpClient);
    }

    /// <summary>
    /// Chooses the collector for this run. Kept as a method because the answer
    /// depends on the --demo flag of the current command.
    /// </summary>
    public CollectorSelection SelectCollector(bool useDemoData) =>
        SystemCollectorFactory.Create(useDemoData);

    public void Dispose()
    {
        _httpClient.Dispose();
        _cancellation.Dispose();
    }
}
