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
        // Guard rail added after a live run: the model turned "81% in use" into
        // "18% in use" and called a nearly full disk healthy. Numbers must be
        // repeated, never recomputed.
        builder.AppendLine("Repeat every number exactly as written. Never recalculate a percentage or a size.");
        // Second guard rail from the same live run: the model spelled numbers out
        // in German words and produced "einhundertsechsundachtzig Prozent" (186%)
        // by merging two different values.
        builder.AppendLine("Write numbers as digits, never as words.");
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
            // Occupancy comes first: while it stood behind the free space, models
            // attached the percentage to the wrong value ("66.8 GiB free, which is
            // 81 percent of the capacity").
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"- {disk.Identifier} ({disk.FileSystem ?? "unknown file system"}): {FormatUsage(disk)}total capacity {ByteSize.Format(disk.TotalBytes)}; free space {ByteSize.Format(disk.FreeBytes)}");
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

            // "not connected" was read by a small model as "the adapter is
            // missing". Saying that the adapter exists first removes that reading.
            string state = adapter.IsUp ? "installed, link up" : "installed, link down";

            builder.AppendLine(CultureInfo.InvariantCulture, $"- {adapter.Name}: {state}{speed}");
        }
    }

    private static string FormatCores(CpuInfo cpu) =>
        cpu.PhysicalCores is null
            ? $"{cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logical"
            : $"{cpu.PhysicalCores.Value.ToString(CultureInfo.InvariantCulture)} physical, {cpu.LogicalCores.ToString(CultureInfo.InvariantCulture)} logical";

    /// <summary>
    /// Adds the occupancy as a full sentence fragment. The short form
    /// "(81% used)" next to a "free" value was misread by the model as the free
    /// share, so the wording now names what the number describes, and a nearly
    /// full disk is labelled in words as well.
    /// </summary>
    private static string FormatUsage(DiskInfo disk)
    {
        if (disk.FreeBytes is null || disk.TotalBytes <= 0)
        {
            return string.Empty;
        }

        double usedPercent = 100d * (disk.TotalBytes - disk.FreeBytes.Value) / disk.TotalBytes;
        string percent = usedPercent.ToString("0", CultureInfo.InvariantCulture);
        string warning = usedPercent >= 85 ? ", this disk is almost full" : string.Empty;

        return $"{percent} percent of the capacity is in use{warning}; ";
    }
}
