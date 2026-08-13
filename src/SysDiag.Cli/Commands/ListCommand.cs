using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Cli.Output;
using SysDiag.Core.Models;

namespace SysDiag.Cli.Commands;

/// <summary>
/// Shows the snapshots stored in the database, newest first.
/// </summary>
public sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    private readonly AppServices _services;
    private readonly IAnsiConsole _console;

    public ListCommand(AppServices services, IAnsiConsole console)
    {
        _services = services;
        _console = console;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-n|--limit")]
        [Description("Maximum number of snapshots to show.")]
        [DefaultValue(20)]
        public int Limit { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        IReadOnlyList<SnapshotSummary> summaries =
            await _services.Repository.ListAsync(settings.Limit, _services.Cancellation).ConfigureAwait(false);

        SnapshotRenderer.RenderList(_console, summaries);
        return ExitCodes.Success;
    }
}
