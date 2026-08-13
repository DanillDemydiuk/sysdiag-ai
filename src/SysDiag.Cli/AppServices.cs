using SysDiag.Cli.Configuration;
using SysDiag.Collectors;
using SysDiag.Core.Abstractions;
using SysDiag.Llm;
using SysDiag.Storage;

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
    }

    public AppSettings Settings { get; }

    public ISnapshotRepository Repository { get; }

    public ISnapshotComparer Comparer { get; }

    public IExplanationService Explanations { get; }

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
