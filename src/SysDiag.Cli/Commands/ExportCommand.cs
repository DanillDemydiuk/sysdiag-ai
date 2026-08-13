using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;

namespace SysDiag.Cli.Commands;

/// <summary>
/// Writes a stored snapshot to a file or to standard output.
/// </summary>
public sealed class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    private readonly AppServices _services;
    private readonly IAnsiConsole _console;

    public ExportCommand(AppServices services, IAnsiConsole console)
    {
        _services = services;
        _console = console;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[id]")]
        [Description("Snapshot to export. Defaults to the most recent one.")]
        public long? Id { get; init; }

        [CommandOption("-f|--format")]
        [Description("Output format: json or markdown.")]
        [DefaultValue("json")]
        public string Format { get; init; } = "json";

        [CommandOption("-o|--output")]
        [Description("Target file. Without it the result is printed to standard output.")]
        public string? OutputPath { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ISnapshotExporter? exporter = _services.FindExporter(settings.Format);

        if (exporter is null)
        {
            string available = string.Join(", ", _services.Exporters.Select(candidate => candidate.FormatName));
            _console.MarkupLine($"[red]Unknown format '{Markup.Escape(settings.Format)}'.[/] Available: {available}.");
            return ExitCodes.UserError;
        }

        SystemSnapshot? snapshot = settings.Id is null
            ? await _services.Repository.GetLatestAsync(_services.Cancellation).ConfigureAwait(false)
            : await _services.Repository.GetAsync(settings.Id.Value, _services.Cancellation).ConfigureAwait(false);

        if (snapshot is null)
        {
            _console.MarkupLine(settings.Id is null
                ? "[yellow]No snapshots stored yet.[/] Run [bold]sysdiag scan[/] first."
                : $"[red]Snapshot #{settings.Id.Value} does not exist.[/]");
            return ExitCodes.UserError;
        }

        string content = exporter.Render(snapshot);

        // Without --output the result goes to standard output, so the command can
        // be piped: sysdiag export --format json > report.json
        if (string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            _console.WriteLine(content);
            return ExitCodes.Success;
        }

        string path = ResolvePath(settings.OutputPath, snapshot, exporter);
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, _services.Cancellation).ConfigureAwait(false);
        _console.MarkupLine($"[green]Snapshot #{snapshot.Id} written to[/] {Markup.Escape(path)}");

        return ExitCodes.Success;
    }

    /// <summary>
    /// Builds the target path. A directory as target gets a generated file name,
    /// which makes exporting several snapshots into one folder painless.
    /// </summary>
    private static string ResolvePath(string outputPath, SystemSnapshot snapshot, ISnapshotExporter exporter)
    {
        string fullPath = Path.GetFullPath(outputPath);

        if (!Directory.Exists(fullPath))
        {
            return fullPath;
        }

        string fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"snapshot-{snapshot.Id}-{snapshot.CreatedAtUtc.ToUniversalTime():yyyyMMdd-HHmmss}{exporter.FileExtension}");

        return Path.Combine(fullPath, fileName);
    }
}
