namespace SysDiag.Core.Models;

/// <summary>
/// A single storage volume. A machine usually has several of them, which is why
/// the diff engine matches volumes by <see cref="Identifier"/> instead of
/// comparing two lists position by position.
/// </summary>
public sealed record DiskInfo
{
    /// <summary>
    /// Stable identifier of the volume: a drive letter on Windows ("C:"),
    /// a mount point on Linux ("/", "/home").
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>Volume label or device name, or <c>null</c> if not set.</summary>
    public string? Label { get; init; }

    /// <summary>File system name, for example "NTFS" or "ext4".</summary>
    public string? FileSystem { get; init; }

    /// <summary>Capacity of the volume in bytes.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Free space in bytes, or <c>null</c> if unknown.</summary>
    public long? FreeBytes { get; init; }
}
