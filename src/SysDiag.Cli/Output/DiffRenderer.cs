using System.Globalization;
using Spectre.Console;
using SysDiag.Core.Diff;

namespace SysDiag.Cli.Output;

/// <summary>
/// Prints the result of a snapshot comparison.
/// </summary>
public static class DiffRenderer
{
    public static void Render(IAnsiConsole console, SnapshotDiff diff)
    {
        console.Write(new Rule(
            $"Snapshot [bold]#{diff.LeftSnapshotId.ToString(CultureInfo.InvariantCulture)}[/] -> [bold]#{diff.RightSnapshotId.ToString(CultureInfo.InvariantCulture)}[/]")
        {
            Justification = Justify.Left,
        });

        console.MarkupLine($"[grey]{SnapshotRenderer.FormatTimestamp(diff.LeftCreatedAtUtc)} UTC  vs  {SnapshotRenderer.FormatTimestamp(diff.RightCreatedAtUtc)} UTC[/]");

        if (diff.Entries.Count == 0)
        {
            console.MarkupLine("[green]The two snapshots are identical.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Change")
            .AddColumn("Component")
            .AddColumn("Property")
            .AddColumn("Before")
            .AddColumn("After");

        foreach (DiffEntry entry in diff.Entries)
        {
            table.AddRow(
                FormatKind(entry),
                Markup.Escape(entry.Category),
                Markup.Escape(entry.Property),
                Markup.Escape(entry.OldValue ?? "-"),
                Markup.Escape(entry.NewValue ?? "-"));
        }

        console.Write(table);

        // The summary line is what the user actually reads: volatile values always
        // differ, so their presence alone must not be reported as a change.
        if (diff.HasRelevantChanges)
        {
            console.MarkupLine("[yellow]The configuration changed.[/]");
        }
        else
        {
            console.MarkupLine("[green]No configuration changes - only values that fluctuate on their own.[/]");
        }
    }

    private static string FormatKind(DiffEntry entry) => entry switch
    {
        { Kind: ChangeKind.Added } => "[green]added[/]",
        { Kind: ChangeKind.Removed } => "[red]removed[/]",
        { IsVolatile: true } => "[grey]changed*[/]",
        _ => "[yellow]changed[/]",
    };
}
