using System.Globalization;
using System.Text;
using SysDiag.Core.Formatting;
using SysDiag.Core.Models;

namespace SysDiag.Llm;

/// <summary>
/// Builds the prompt that is sent to the local model.
/// </summary>
/// <remarks>
/// Separated from the HTTP client so it can be unit tested without a running
/// model: the tests assert what the prompt contains - and, just as important,
/// what it does not contain.
/// </remarks>
public static class PromptBuilder
{
    /// <summary>
    /// Describes the snapshot as a compact fact sheet and asks for an explanation
    /// a non-technical user can follow.
    /// </summary>
    /// <param name="snapshot">The snapshot to describe.</param>
    /// <param name="responseLanguage">Language the answer should be written in.</param>
    public static string Build(SystemSnapshot snapshot, string responseLanguage = "German")
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();

        builder.AppendLine("You are a helpful IT support assistant.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Explain the following computer configuration in {responseLanguage}.");
        builder.AppendLine("Write for a non-technical reader: short paragraphs, no bullet lists longer than five items.");
        builder.AppendLine("Say whether the machine looks healthy and mention anything that deserves attention,");
        builder.AppendLine("for example little free disk space or an unusually small amount of memory.");
        builder.AppendLine("Use only the facts below. Do not invent hardware that is not listed.");
        builder.AppendLine();
        builder.AppendLine("=== SYSTEM SNAPSHOT ===");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Taken at (UTC): {snapshot.CreatedAtUtc:yyyy-MM-dd HH:mm}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Operating system: {snapshot.Os.Caption} (version {snapshot.Os.Version}, {snapshot.Os.Architecture})");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Processor: {snapshot.Cpu.Name}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Cores: {FormatCores(snapshot.Cpu)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Memory: {ByteSize.Format(snapshot.Memory.TotalBytes)} total, {ByteSize.Format(snapshot.Memory.AvailableBytes)} available");

        AppendDisks(builder, snapshot.Disks);
        AppendAdapters(builder, snapshot.NetworkAdapters);

        builder.AppendLine("=== END OF SNAPSHOT ===");

        return builder.ToString();
    }

    private static void AppendDisks(StringBuilder builder, IReadOnlyList<DiskInfo> disks)
    {
        if (disks.Count == 0)
        {
            builder.AppendLine("Disks: none reported");
            return;
        }

        builder.AppendLine("Disks:");

        foreach (DiskInfo disk in disks)
        {
            string usage = FormatUsage(disk);
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"- {disk.Identifier} ({disk.FileSystem ?? "unknown file system"}): {ByteSize.Format(disk.TotalBytes)} total, {ByteSize.Format(disk.FreeBytes)} free{usage}");
        }
    }

    private static void AppendAdapters(StringBuilder builder, IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        if (adapters.Count == 0)
        {
            builder.AppendLine("Network adapters: none reported");
            return;
        }

        builder.AppendLine("Network adapters:");

        foreach (NetworkAdapterInfo adapter in adapters)
        {
            // MAC and IP addresses are deliberately left out. They identify the
            // machine and its network, and they add nothing to an explanation of
            // the hardware - the smallest useful prompt is also the safest one.
            string speed = adapter.SpeedMbps is null
                ? string.Empty
                : $", {adapter.SpeedMbps.Value.ToString(CultureInfo.InvariantCulture)} Mbit/s";

            builder.AppendLine(CultureInfo.InvariantCulture,
                $"- {adapter.Name}: {(adapter.IsUp ? "connected" : "not connected")}{speed}");
        }
    }

    private static string FormatCores(CpuInfo cpu) =>
        cpu.PhysicalCores is null
            ? $"{cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logical"
            : $"{cpu.PhysicalCores.Value.ToString(CultureInfo.InvariantCulture)} physical, {cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logical";

    /// <summary>
    /// Adds the used percentage, because "70 GiB free" alone says nothing without
    /// the capacity next to it - and percentages are what a reader reacts to.
    /// </summary>
    private static string FormatUsage(DiskInfo disk)
    {
        if (disk.FreeBytes is null || disk.TotalBytes <= 0)
        {
            return string.Empty;
        }

        double usedPercent = 100d * (disk.TotalBytes - disk.FreeBytes.Value) / disk.TotalBytes;
        return $" ({usedPercent.ToString("0", CultureInfo.InvariantCulture)}% used)";
    }
}
