using System.Globalization;

namespace SysDiag.Collectors.Linux;

/// <summary>
/// Pure text parsing for the files below /proc and /etc/os-release.
/// </summary>
/// <remarks>
/// This class never touches the file system: every method takes the file content
/// as a string. That is what makes the Linux collector testable on a Windows
/// machine and in CI, using recorded fixtures instead of a real kernel.
/// </remarks>
public static class ProcParser
{
    /// <summary>Kernel files report memory sizes in kibibytes, marked with the suffix "kB".</summary>
    private const long BytesPerKibibyte = 1024;

    /// <summary>
    /// Reads the processor name from /proc/cpuinfo. x86 kernels use "model name",
    /// ARM boards such as the Raspberry Pi use "Model" instead.
    /// </summary>
    public static string? ParseCpuModelName(string cpuInfo) =>
        FindFirstValue(cpuInfo, "model name") ?? FindFirstValue(cpuInfo, "Model");

    /// <summary>
    /// Counts logical processors. /proc/cpuinfo contains one "processor" line per
    /// hardware thread, so counting them is the canonical way to get this number.
    /// </summary>
    public static int ParseLogicalCoreCount(string cpuInfo) =>
        EnumerateEntries(cpuInfo).Count(entry => entry.Key == "processor");

    /// <summary>
    /// Counts physical cores. On multi-socket machines "cpu cores" repeats per
    /// socket, so the values are grouped by "physical id" and summed. Kernels on
    /// single-socket systems omit "physical id" entirely, which is handled as a
    /// fallback. Returns <c>null</c> when the file reports no core count at all,
    /// which is normal on ARM.
    /// </summary>
    public static int? ParsePhysicalCoreCount(string cpuInfo)
    {
        var coresPerSocket = new Dictionary<string, int>(StringComparer.Ordinal);
        string? socket = null;
        int? cores = null;

        foreach ((string key, string value) in EnumerateEntries(cpuInfo))
        {
            switch (key)
            {
                case "processor":
                    socket = null;
                    cores = null;
                    break;
                case "physical id":
                    socket = value;
                    break;
                case "cpu cores":
                    cores = TryParseInt(value);
                    break;
            }

            if (socket is not null && cores is not null)
            {
                coresPerSocket[socket] = cores.Value;
            }
        }

        if (coresPerSocket.Count > 0)
        {
            return coresPerSocket.Values.Sum();
        }

        return EnumerateEntries(cpuInfo)
            .Where(entry => entry.Key == "cpu cores")
            .Select(entry => TryParseInt(entry.Value))
            .FirstOrDefault(value => value is not null);
    }

    /// <summary>
    /// Reads a memory value such as "MemTotal" or "MemAvailable" from
    /// /proc/meminfo and converts it to bytes.
    /// </summary>
    public static long? ParseMemoryBytes(string memInfo, string key)
    {
        string? value = FindFirstValue(memInfo, key);
        if (value is null)
        {
            return null;
        }

        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long amount))
        {
            return null;
        }

        bool isKibibytes = parts.Length > 1 && parts[1].Equals("kB", StringComparison.OrdinalIgnoreCase);
        return isKibibytes ? amount * BytesPerKibibyte : amount;
    }

    /// <summary>
    /// Parses /etc/os-release into key/value pairs. The format is shell syntax:
    /// KEY=value, where the value may be quoted and comment lines start with '#'.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseOsRelease(string osRelease)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string rawLine in osRelease.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim().Trim('"', '\'');

            if (key.Length > 0)
            {
                values[key] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Converts the content of /sys/.../cpuinfo_max_freq, which is a clock speed
    /// in kilohertz, into megahertz.
    /// </summary>
    public static int? ParseMaxClockMhz(string sysfsContent)
    {
        if (!long.TryParse(sysfsContent.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long kiloHertz)
            || kiloHertz <= 0)
        {
            return null;
        }

        return (int)(kiloHertz / 1000);
    }

    private static string? FindFirstValue(string content, string key) =>
        EnumerateEntries(content)
            .Where(entry => entry.Key == key)
            .Select(entry => entry.Value)
            .FirstOrDefault(value => value.Length > 0);

    /// <summary>
    /// Splits "key : value" lines, the shared shape of /proc/cpuinfo and
    /// /proc/meminfo. Lines without a colon are ignored, which also skips the
    /// empty separator lines between processor blocks.
    /// </summary>
    private static IEnumerable<(string Key, string Value)> EnumerateEntries(string content)
    {
        foreach (string rawLine in content.Split('\n'))
        {
            int separator = rawLine.IndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            string key = rawLine[..separator].Trim();
            string value = rawLine[(separator + 1)..].Trim();

            if (key.Length > 0)
            {
                yield return (key, value);
            }
        }
    }

    private static int? TryParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
}
