using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Core.Models;

namespace SysDiag.Cli.Commands;

/// <summary>
/// Sends a snapshot to the local model and prints the explanation.
/// </summary>
public sealed class ExplainCommand : AsyncCommand<ExplainCommand.Settings>
{
    private readonly AppServices _services;
    private readonly IAnsiConsole _console;

    public ExplainCommand(AppServices services, IAnsiConsole console)
    {
        _services = services;
        _console = console;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[id]")]
        [Description("Snapshot to explain. Defaults to the most recent one.")]
        public long? Id { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        SystemSnapshot? snapshot = settings.Id is null
            ? await _services.Repository.GetLatestAsync().ConfigureAwait(false)
            : await _services.Repository.GetAsync(settings.Id.Value).ConfigureAwait(false);

        if (snapshot is null)
        {
            _console.MarkupLine(settings.Id is null
                ? "[yellow]No snapshots stored yet.[/] Run [bold]sysdiag scan[/] first."
                : $"[red]Snapshot #{settings.Id.Value} does not exist.[/]");
            return ExitCodes.UserError;
        }

        _console.MarkupLine($"[grey]Asking {Markup.Escape(_services.Settings.Ollama.Model)} at {Markup.Escape(_services.Settings.Ollama.BaseUrl)}...[/]");

        ExplanationResult result = await _console
            .Status()
            .StartAsync("Waiting for the model...", _ => _services.Explanations.ExplainAsync(snapshot))
            .ConfigureAwait(false);

        // An unreachable model is an expected situation: the command says what is
        // missing and still exits successfully, because nothing went wrong here.
        if (!result.IsSuccess)
        {
            _console.Write(new Panel($"[yellow]{Markup.Escape(result.FailureReason ?? "No explanation available.")}[/]")
                .Header("Ollama unavailable")
                .BorderColor(Color.Yellow));
            return ExitCodes.Success;
        }

        _console.Write(new Panel(Markup.Escape(result.Text!))
            .Header($"Snapshot #{snapshot.Id}")
            .BorderColor(Color.Green));

        return ExitCodes.Success;
    }
}
