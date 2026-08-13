namespace SysDiag.Collectors.Linux;

/// <summary>
/// Decides whether a Linux mount point describes real storage.
/// </summary>
/// <remarks>
/// Filtering by file system type alone is not enough: .NET reports the mount
/// source for some kernel mounts, so /dev and /proc/acpi arrive as "udev" rather
/// than "devtmpfs". The mount point itself is the reliable second signal, which
/// is why both checks exist. Kept free of file system access so the rules can be
/// unit tested on any operating system.
/// </remarks>
public static class MountFilter
{
    /// <summary>
    /// File system types that never describe a physical volume.
    /// </summary>
    private static readonly HashSet<string> PseudoFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "autofs", "binfmt_misc", "bpf", "cgroup", "cgroup2", "cgroup2fs", "configfs", "debugfs", "devpts",
        "devtmpfs", "efivarfs", "fuse", "fusectl", "hugetlbfs", "mqueue", "nsfs", "overlay", "overlayfs",
        "pstore", "proc", "procfs", "ramfs", "rpc_pipefs", "securityfs", "squashfs", "sysfs", "tmpfs",
        "tracefs", "udev",
    };

    /// <summary>
    /// Directories owned by the kernel or by the runtime. Everything mounted below
    /// them is infrastructure, whatever its file system type claims to be.
    /// </summary>
    private static readonly string[] PseudoMountPoints =
    [
        "/proc", "/sys", "/dev", "/run", "/snap", "/var/lib/docker",
    ];

    /// <summary>
    /// True if the mount should appear in a snapshot as a disk.
    /// </summary>
    /// <param name="mountPoint">Mount point, for example "/" or "/home".</param>
    /// <param name="fileSystem">File system type as reported by the runtime, if known.</param>
    public static bool IsRealVolume(string mountPoint, string? fileSystem)
    {
        if (string.IsNullOrWhiteSpace(mountPoint))
        {
            return false;
        }

        if (fileSystem is not null && PseudoFileSystems.Contains(fileSystem))
        {
            return false;
        }

        return !IsBelowPseudoMountPoint(mountPoint);
    }

    private static bool IsBelowPseudoMountPoint(string mountPoint)
    {
        foreach (string pseudo in PseudoMountPoints)
        {
            // Compared as a path segment, not as a plain prefix: "/development"
            // is an ordinary directory and must not be swallowed by "/dev".
            bool isExactMatch = mountPoint.Equals(pseudo, StringComparison.Ordinal);
            bool isBelow = mountPoint.StartsWith(pseudo + "/", StringComparison.Ordinal);

            if (isExactMatch || isBelow)
            {
                return true;
            }
        }

        return false;
    }
}
