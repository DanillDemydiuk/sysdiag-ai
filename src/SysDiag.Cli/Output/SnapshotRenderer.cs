using System.Globalization;
using Spectre.Console;
using SysDiag.Core.Formatting;
using SysDiag.Core.Models;

namespace SysDiag.Cli.Output;

/// <summary>
/// Prints snapshots and snapshot lists as console tables.
/// </summary>
/// <remarks>
/// All rendering lives here so that commands stay free of layout code and every
/// table in the application looks the same. Every value that comes from the
/// machine is escaped: a volume label may legally contain "[", which Spectre
/// would otherwise read as markup.
/// </remarks>
public static class SnapshotRenderer
{
    public static void Render(IAnsiConsole console, SystemSnapshot snapshot)
    {
        console.Write(new Rule($"[bold]{Escape(snapshot.MachineName)}[/] - {Escape(FormatTimestamp(snapshot.CreatedAtUtc))}")
        {
            Justification = Justify.Left,
        });

        var overview = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Component")
            .AddColumn("Details");

        overview.AddRow("Operating system", Escape($"{snapshot.Os.Caption} ({snapshot.Os.Version}, {snapshot.Os.Architecture})"));
        overview.AddRow("Processor", Escape(snapshot.Cpu.Name));
        overview.AddRow("Cores", Escape(FormatCores(snapshot.Cpu)));
        overview.AddRow("Memory", Escape($"{ByteSize.Format(snapshot.Memory.TotalBytes)} total, {ByteSize.Format(snapshot.Memory.AvailableBytes)} available"));
        overview.AddRow("Collector", Escape(snapshot.CollectorName));

        console.Write(overview);

        RenderDisks(console, snapshot.Disks);
        RenderAdapters(console, snapshot.NetworkAdapters);
    }

    public static void RenderList(IAnsiConsole console, IReadOnlyList<SnapshotSummary> summaries)
    {
        if (summaries.Count == 0)
        {
            console.MarkupLine("[yellow]No snapshots stored yet.[/] Run [bold]sysdiag scan[/] first.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Taken at (UTC)")
            .AddColumn("Machine")
            .AddColumn("Operating system")
            .AddColumn("Collector");

        foreach (SnapshotSummary summary in summaries)
        {
            table.AddRow(
                summary.Id.ToString(CultureInfo.InvariantCulture),
                Escape(FormatTimestamp(summary.CreatedAtUtc)),
                Escape(summary.MachineName),
                Escape(summary.OsCaption),
                Escape(summary.CollectorName));
        }

        console.Write(table);
    }

    private static void RenderDisks(IAnsiConsole console, IReadOnlyList<DiskInfo> disks)
    {
        if (disks.Count == 0)
        {
            console.MarkupLine("[yellow]No local disks reported.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("Disks")
            .AddColumn("Volume")
            .AddColumn("File system")
            .AddColumn("Capacity")
            .AddColumn("Free")
            .AddColumn("Used");

        foreach (DiskInfo disk in disks)
        {
            table.AddRow(
                Escape(disk.Label is null ? disk.Identifier : $"{disk.Identifier} ({disk.Label})"),
                Escape(disk.FileSystem ?? ByteSize.Unknown),
                ByteSize.Format(disk.TotalBytes),
                ByteSize.Format(disk.FreeBytes),
                FormatUsage(disk));
        }

        console.Write(table);
    }

    private static void RenderAdapters(IAnsiConsole console, IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        if (adapters.Count == 0)
        {
            console.MarkupLine("[yellow]No network adapters reported.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("Network")
            .AddColumn("Adapter")
            .AddColumn("Status")
            .AddColumn("Speed")
            .AddColumn("Addresses");

        foreach (NetworkAdapterInfo adapter in adapters)
        {
            table.AddRow(
                Escape(adapter.Name),
                adapter.IsUp ? "[green]up[/]" : "[grey]down[/]",
                Escape(adapter.SpeedMbps is null
                    ? ByteSize.Unknown
                    : $"{adapter.SpeedMbps.Value.ToString(CultureInfo.InvariantCulture)} Mbit/s"),
                Escape(adapter.IpAddresses.Count == 0 ? "-" : string.Join(", ", adapter.IpAddresses)));
        }

        console.Write(table);
    }

    private static string FormatCores(CpuInfo cpu)
    {
        string cores = cpu.PhysicalCores is null
            ? $"{cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logical"
            : $"{cpu.PhysicalCores.Value.ToString(CultureInfo.InvariantCulture)} physical / {cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logical";

        return cpu.MaxClockMhz is null
            ? cores
            : $"{cores}, up to {cpu.MaxClockMhz.Value.ToString(CultureInfo.InvariantCulture)} MHz";
    }

    private static string FormatUsage(DiskInfo disk)
    {
        if (disk.FreeBytes is null || disk.TotalBytes <= 0)
        {
            return ByteSize.Unknown;
        }

        double usedPercent = 100d * (disk.TotalBytes - disk.FreeBytes.Value) / disk.TotalBytes;
        string text = $"{usedPercent.ToString("0", CultureInfo.InvariantCulture)}%";

        // A nearly full volume is the single most useful warning this tool can give.
        return usedPercent >= 90 ? $"[red]{text}[/]" : text;
    }

    internal static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Escape(string value) => Markup.Escape(value);
}
