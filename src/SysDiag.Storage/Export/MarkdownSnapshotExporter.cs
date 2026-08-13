using System.Globalization;
using System.Text;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Formatting;
using SysDiag.Core.Models;

namespace SysDiag.Storage.Export;

/// <summary>
/// Exports a snapshot as Markdown, meant to be pasted into a ticket, a wiki page
/// or an e-mail.
/// </summary>
/// <remarks>
/// Unlike the JSON export this one is written for humans, so values are formatted
/// the same way the console shows them. Every value coming from the machine is
/// escaped: a volume label containing a pipe character would otherwise tear the
/// table apart.
/// </remarks>
public sealed class MarkdownSnapshotExporter : ISnapshotExporter
{
    public string FormatName => "markdown";

    public string FileExtension => ".md";

    public string Render(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();

        builder.AppendLine(CultureInfo.InvariantCulture, $"# Systembericht {Escape(snapshot.MachineName)}");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"Aufgenommen am {snapshot.CreatedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC (Snapshot #{snapshot.Id.ToString(CultureInfo.InvariantCulture)}, Quelle: {Escape(snapshot.CollectorName)})");
        builder.AppendLine();

        builder.AppendLine("## Überblick");
        builder.AppendLine();
        builder.AppendLine("| Komponente | Details |");
        builder.AppendLine("| --- | --- |");
        AppendRow(builder, "Betriebssystem", $"{snapshot.Os.Caption} ({snapshot.Os.Version}, {snapshot.Os.Architecture})");
        AppendRow(builder, "Prozessor", snapshot.Cpu.Name);
        AppendRow(builder, "Kerne", FormatCores(snapshot.Cpu));
        AppendRow(builder, "Arbeitsspeicher",
            $"{ByteSize.Format(snapshot.Memory.TotalBytes)} gesamt, {ByteSize.Format(snapshot.Memory.AvailableBytes)} verfügbar");
        builder.AppendLine();

        AppendDisks(builder, snapshot.Disks);
        AppendAdapters(builder, snapshot.NetworkAdapters);

        return builder.ToString();
    }

    private static void AppendDisks(StringBuilder builder, IReadOnlyList<DiskInfo> disks)
    {
        builder.AppendLine("## Datenträger");
        builder.AppendLine();

        if (disks.Count == 0)
        {
            builder.AppendLine("Keine lokalen Datenträger gemeldet.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Laufwerk | Dateisystem | Kapazität | Frei | Belegt |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (DiskInfo disk in disks)
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| {Escape(disk.Identifier)} | {Escape(disk.FileSystem ?? ByteSize.Unknown)} | {ByteSize.Format(disk.TotalBytes)} | {ByteSize.Format(disk.FreeBytes)} | {FormatUsage(disk)} |");
        }

        builder.AppendLine();
    }

    private static void AppendAdapters(StringBuilder builder, IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        builder.AppendLine("## Netzwerk");
        builder.AppendLine();

        if (adapters.Count == 0)
        {
            builder.AppendLine("Keine Netzwerkadapter gemeldet.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Adapter | Status | Geschwindigkeit |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (NetworkAdapterInfo adapter in adapters)
        {
            string speed = adapter.SpeedMbps is null
                ? ByteSize.Unknown
                : $"{adapter.SpeedMbps.Value.ToString(CultureInfo.InvariantCulture)} Mbit/s";

            // Addresses are left out on purpose: an exported report usually
            // travels further than the machine it describes.
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| {Escape(adapter.Name)} | {(adapter.IsUp ? "verbunden" : "getrennt")} | {speed} |");
        }

        builder.AppendLine();
    }

    private static void AppendRow(StringBuilder builder, string component, string details) =>
        builder.AppendLine(CultureInfo.InvariantCulture, $"| {component} | {Escape(details)} |");

    private static string FormatCores(CpuInfo cpu)
    {
        string cores = cpu.PhysicalCores is null
            ? $"{cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logisch"
            : $"{cpu.PhysicalCores.Value.ToString(CultureInfo.InvariantCulture)} physisch / {cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logisch";

        return cpu.MaxClockMhz is null
            ? cores
            : $"{cores}, bis {cpu.MaxClockMhz.Value.ToString(CultureInfo.InvariantCulture)} MHz";
    }

    private static string FormatUsage(DiskInfo disk)
    {
        if (disk.FreeBytes is null || disk.TotalBytes <= 0)
        {
            return ByteSize.Unknown;
        }

        double usedPercent = 100d * (disk.TotalBytes - disk.FreeBytes.Value) / disk.TotalBytes;
        return $"{usedPercent.ToString("0", CultureInfo.InvariantCulture)} %";
    }

    /// <summary>
    /// Escapes the pipe character, the only one that can break a Markdown table.
    /// Volume labels and adapter names come from the machine and may contain it.
    /// </summary>
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
