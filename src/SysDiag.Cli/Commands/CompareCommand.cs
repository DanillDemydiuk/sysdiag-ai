using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Cli.Output;
using SysDiag.Core.Diff;
using SysDiag.Core.Models;

namespace SysDiag.Cli.Commands;

/// <summary>
/// Compares two stored snapshots and prints their differences.
/// </summary>
public sealed class CompareCommand : AsyncCommand<CompareCommand.Settings>
{
    private readonly AppServices _services;
    private readonly IAnsiConsole _console;

    public CompareCommand(AppServices services, IAnsiConsole console)
    {
        _services = services;
        _console = console;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<first-id>")]
        [Description("Id of the older snapshot.")]
        public long FirstId { get; init; }

        [CommandArgument(1, "<second-id>")]
        [Description("Id of the newer snapshot.")]
        public long SecondId { get; init; }

        [CommandOption("--explain")]
        [Description("Let the local model judge whether the changes matter.")]
        public bool Explain { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        SystemSnapshot? left = await _services.Repository
            .GetAsync(settings.FirstId, _services.Cancellation)
            .ConfigureAwait(false);
        SystemSnapshot? right = await _services.Repository
            .GetAsync(settings.SecondId, _services.Cancellation)
            .ConfigureAwait(false);

        // A wrong id is a user mistake, not a program failure: report it plainly
        // and end with a non-zero exit code instead of throwing.
        if (left is null || right is null)
        {
            long missingId = left is null ? settings.FirstId : settings.SecondId;
            _console.MarkupLine($"[red]Snapshot #{missingId} does not exist.[/] Use [bold]sysdiag list[/] to see stored ids.");
            return ExitCodes.UserError;
        }

        SnapshotDiff diff = _services.Comparer.Compare(left, right);
        DiffRenderer.Render(_console, diff);

        if (settings.Explain)
        {
            await RenderExplanationAsync(diff).ConfigureAwait(false);
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Adds the model's verdict below the table. The table is printed first and
    /// stays valid on its own: the explanation is an addition, never a
    /// replacement for the measured data.
    /// </summary>
    private async Task RenderExplanationAsync(SnapshotDiff diff)
    {
        _console.MarkupLine($"[grey]Asking {Markup.Escape(_services.Settings.Ollama.Model)} about these changes...[/]");

        ExplanationResult result = await _console
            .Status()
            .StartAsync("Waiting for the model...", _ => _services.Explanations.ExplainDiffAsync(diff, _services.Cancellation))
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            _console.Write(new Panel($"[yellow]{Markup.Escape(result.FailureReason ?? "No explanation available.")}[/]")
                .Header("Ollama unavailable")
                .BorderColor(Color.Yellow));
            return;
        }

        _console.Write(new Panel(Markup.Escape(result.Text!))
            .Header("What the changes mean")
            .BorderColor(Color.Green));
    }
}
