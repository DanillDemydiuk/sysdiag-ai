using System.Globalization;

namespace SysDiag.Core.Formatting;

/// <summary>
/// Turns raw byte counts into short, readable strings. The models store bytes,
/// so this is the single place where rounding happens - both the diff engine and
/// the console output use it, which keeps "32 GiB" identical everywhere.
/// </summary>
public static class ByteSize
{
    /// <summary>Text used wherever a value is not available.</summary>
    public const string Unknown = "n/a";

    /// <summary>
    /// Binary units, because operating systems report memory and disk capacity in
    /// powers of 1024. Using "GB" for 1024^3 would be the common lie; "GiB" is
    /// what the number actually is.
    /// </summary>
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];

    private const double Step = 1024d;

    public static string Format(long bytes)
    {
        if (bytes < Step)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} {Units[0]}");
        }

        double value = bytes;
        int unitIndex = 0;

        while (value >= Step && unitIndex < Units.Length - 1)
        {
            value /= Step;
            unitIndex++;
        }

        // One decimal below 100, none above: "1.5 GiB" is useful, "1234.7 GiB" is noise.
        string format = value >= 100 ? "0" : "0.0";
        return string.Create(CultureInfo.InvariantCulture, $"{value.ToString(format, CultureInfo.InvariantCulture)} {Units[unitIndex]}");
    }

    public static string Format(long? bytes) => bytes is null ? Unknown : Format(bytes.Value);
}
