using FluentAssertions;
using SysDiag.Collectors.Linux;

namespace SysDiag.Tests;

/// <summary>
/// Tests for the mount filter. Every case below was taken from the output of a
/// real container run, where the first version of the collector listed kernel
/// mounts such as /proc/acpi as if they were hard disks.
/// </summary>
public sealed class MountFilterTests
{
    [Theory]
    [InlineData("/", "ext4")]
    [InlineData("/home", "ext4")]
    [InlineData("/mnt/backup", "xfs")]
    [InlineData("/boot/efi", "vfat")]
    [InlineData("/app", "v9fs")]
    public void IsRealVolume_OrdinaryMounts_AreKept(string mountPoint, string fileSystem)
    {
        MountFilter.IsRealVolume(mountPoint, fileSystem).Should().BeTrue();
    }

    [Theory]
    [InlineData("/dev", "udev")]
    [InlineData("/dev/shm", "udev")]
    [InlineData("/proc/acpi", "udev")]
    [InlineData("/proc/scsi", "udev")]
    [InlineData("/sys/firmware", "udev")]
    [InlineData("/run/lock", "tmpfs")]
    [InlineData("/snap/core22/1", "squashfs")]
    [InlineData("/", "overlay")]
    public void IsRealVolume_KernelAndRuntimeMounts_AreDropped(string mountPoint, string fileSystem)
    {
        MountFilter.IsRealVolume(mountPoint, fileSystem).Should().BeFalse();
    }

    [Fact]
    public void IsRealVolume_DirectoryStartingLikeAPseudoMount_IsKept()
    {
        // "/development" merely starts with the text "/dev"; comparing prefixes
        // without the path separator would hide a real volume.
        MountFilter.IsRealVolume("/development", "ext4").Should().BeTrue();
        MountFilter.IsRealVolume("/system-data", "ext4").Should().BeTrue();
    }

    [Fact]
    public void IsRealVolume_UnknownFileSystem_IsKeptUnlessTheMountPointSaysOtherwise()
    {
        MountFilter.IsRealVolume("/data", fileSystem: null).Should().BeTrue();
        MountFilter.IsRealVolume("/proc/self", fileSystem: null).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsRealVolume_EmptyMountPoint_IsDropped(string mountPoint)
    {
        MountFilter.IsRealVolume(mountPoint, "ext4").Should().BeFalse();
    }

    [Fact]
    public void IsRealVolume_FileSystemComparison_IsCaseInsensitive()
    {
        MountFilter.IsRealVolume("/mnt/x", "TMPFS").Should().BeFalse();
    }
}
