using SysDiag.Collectors.Linux;
using SysDiag.Collectors.Windows;
using SysDiag.Core.Abstractions;

namespace SysDiag.Collectors;

/// <summary>
/// Picks the collector implementation that fits the machine the program is
/// running on. This is the only place in the code base that knows which
/// platform-specific classes exist; everything else works with
/// <see cref="ISystemCollector"/>.
/// </summary>
public static class SystemCollectorFactory
{
    /// <summary>
    /// Creates a collector for the current run.
    /// </summary>
    /// <param name="useDemoData">
    /// Set by the --demo flag. Demo data wins over platform detection, so the
    /// flag also works on a machine where the real collector would succeed.
    /// </param>
    /// <param name="timeProvider">Clock, injected to keep snapshots reproducible in tests.</param>
    public static CollectorSelection Create(bool useDemoData = false, TimeProvider? timeProvider = null)
    {
        if (useDemoData)
        {
            return new CollectorSelection
            {
                Collector = new DemoCollector(timeProvider),
            };
        }

        ISystemCollector? platformCollector = CreatePlatformCollector(timeProvider);

        if (platformCollector is not null && platformCollector.IsSupported)
        {
            return new CollectorSelection
            {
                Collector = platformCollector,
            };
        }

        // macOS, FreeBSD or anything else: the program keeps working and states
        // clearly that the numbers are not from this machine.
        return new CollectorSelection
        {
            Collector = new DemoCollector(timeProvider),
            Notice = $"Platform '{GetPlatformName()}' is not supported yet - showing demo data instead.",
        };
    }

    /// <summary>
    /// Instantiates the platform collector, or returns <c>null</c> on a platform
    /// without an implementation.
    /// </summary>
    /// <remarks>
    /// The <c>OperatingSystem.IsWindows()</c> / <c>IsLinux()</c> checks are not
    /// decoration: the platform collectors are annotated with
    /// <c>[SupportedOSPlatform]</c>, and without these guards the compiler refuses
    /// to build the project (CA1416). The additional <c>IsSupported</c> check in
    /// <see cref="Create"/> is the collector's own verdict at runtime.
    /// </remarks>
    private static ISystemCollector? CreatePlatformCollector(TimeProvider? timeProvider)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsCollector(timeProvider);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxCollector(timeProvider);
        }

        return null;
    }

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        if (OperatingSystem.IsFreeBSD())
        {
            return "FreeBSD";
        }

        return Environment.OSVersion.Platform.ToString();
    }
}
