using SysDiag.Core.Models;

namespace SysDiag.Core.Abstractions;

/// <summary>
/// Reads the current configuration of the machine. Every platform gets its own
/// implementation (WMI on Windows, procfs on Linux, fixtures in demo mode);
/// the rest of the application only ever sees this interface.
/// </summary>
public interface ISystemCollector
{
    /// <summary>
    /// Stable identifier of the implementation, stored with every snapshot:
    /// "windows-wmi", "linux-procfs", "demo".
    /// </summary>
    string Name { get; }

    /// <summary>
    /// True if this collector can run on the current machine. The factory asks
    /// each candidate instead of hard-coding a platform check in one place.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Collects a full snapshot. Asynchronous because reading hardware data can
    /// take seconds: WMI queries and procfs reads both block on I/O.
    /// </summary>
    Task<SystemSnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
