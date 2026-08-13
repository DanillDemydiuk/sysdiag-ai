using Microsoft.Data.Sqlite;
using Spectre.Console;

namespace SysDiag.Cli.Output;

/// <summary>
/// Turns exceptions into messages a user can act on.
/// </summary>
/// <remarks>
/// A stack trace in the console tells a non-developer nothing and looks like a
/// crash even when the cause is trivial, such as a read-only folder. Known
/// failures therefore get a sentence and a suggestion; the raw exception is
/// still available, but only when SYSDIAG_DEBUG is set.
/// </remarks>
public static class ErrorPresenter
{
    /// <summary>Environment variable that switches on the technical output.</summary>
    public const string DebugVariable = "SYSDIAG_DEBUG";

    public static void Render(IAnsiConsole console, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        (string headline, string? hint) = Describe(exception);

        console.Write(new Panel(BuildBody(headline, hint))
            .Header("Error")
            .BorderColor(Color.Red));

        if (IsDebugEnabled())
        {
            console.WriteException(exception, ExceptionFormats.ShortenPaths);
        }
        else
        {
            console.MarkupLine($"[grey]Set {DebugVariable}=1 for technical details.[/]");
        }
    }

    private static string BuildBody(string headline, string? hint) =>
        hint is null
            ? $"[red]{Markup.Escape(headline)}[/]"
            : $"[red]{Markup.Escape(headline)}[/]{Environment.NewLine}{Markup.Escape(hint)}";

    private static (string Headline, string? Hint) Describe(Exception exception) => exception switch
    {
        SqliteException sqlite => (
            $"The snapshot database could not be used ({sqlite.SqliteErrorCode}).",
            "Check the DatabasePath in appsettings.json and whether the file is locked by another process."),

        UnauthorizedAccessException => (
            "Access denied while reading system information.",
            "Some values require an elevated shell. Try 'sysdiag scan --demo' to verify the installation."),

        IOException io => (
            $"A file could not be read or written: {io.Message}",
            "Check that the working directory is writable."),

        OperationCanceledException => (
            "The operation was cancelled.",
            null),

        _ => (
            $"Unexpected error: {exception.Message}",
            "This is a bug. Please report it with the command you ran."),
    };

    private static bool IsDebugEnabled() =>
        Environment.GetEnvironmentVariable(DebugVariable) is "1" or "true";
}
