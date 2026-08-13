using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Collectors;
using SysDiag.Core.Models;

namespace SysDiag.Cli.Commands;

/// <summary>
/// Reads the current machine configuration and stores it as a new snapshot.
/// </summary>
public sealed class ScanCommand : AsyncCommand<ScanCommand.Settings>
{
    private readonly AppServices _services;
    private readonly IAnsiConsole _console;

    public ScanCommand(AppServices services, IAnsiConsole console)
    {
        _services = services;
        _console = console;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--demo")]
        [Description("Use the built-in example machine instead of reading real hardware.")]
        public bool UseDemoData { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        CollectorSelection selection = _services.SelectCollector(settings.UseDemoData);

        if (selection.Notice is not null)
        {
            _console.MarkupLine($"[yellow]{Markup.Escape(selection.Notice)}[/]");
        }

        if (settings.UseDemoData)
        {
            _console.MarkupLine("[grey]Demo mode: the data below describes a fictional machine.[/]");
        }

        SystemSnapshot snapshot = await _console
            .Status()
            .StartAsync("Collecting system data...", _ => selection.Collector.CollectAsync(_services.Cancellation))
            .ConfigureAwait(false);

        long id = await _services.Repository.SaveAsync(snapshot, _services.Cancellation).ConfigureAwait(false);

        // The stored id belongs to the snapshot from now on, so the printed record
        // matches what "list" and "compare" will show.
        Output.SnapshotRenderer.Render(_console, snapshot with { Id = id });
        _console.MarkupLine($"[green]Stored as snapshot #{id}.[/]");

        return ExitCodes.Success;
    }
}
