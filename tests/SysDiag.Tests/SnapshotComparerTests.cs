using FluentAssertions;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Diff;
using SysDiag.Core.Models;
using SysDiag.Storage;

namespace SysDiag.Tests;

/// <summary>
/// Tests for the diff engine. This is the part of the application with the most
/// branches and the least visible failure mode: a wrong diff still looks like a
/// perfectly normal table.
/// </summary>
public sealed class SnapshotComparerTests
{
    private readonly ISnapshotComparer _comparer = new SnapshotComparer();

    [Fact]
    public void Compare_IdenticalSnapshots_ReportsNoEntries()
    {
        SystemSnapshot snapshot = TestData.Snapshot();

        SnapshotDiff diff = _comparer.Compare(snapshot, snapshot);

        diff.Entries.Should().BeEmpty();
        diff.HasRelevantChanges.Should().BeFalse();
    }

    [Fact]
    public void Compare_OnlyVolatileValuesDiffer_ReportsEntriesButNoRelevantChange()
    {
        SystemSnapshot before = TestData.Snapshot();
        SystemSnapshot after = before with
        {
            Id = 2,
            Memory = before.Memory with { AvailableBytes = 4 * TestData.Gib },
            Disks = [TestData.Disk("C:", freeBytes: 100 * TestData.Gib)],
        };

        SnapshotDiff diff = _comparer.Compare(before, after);

        diff.Entries.Should().HaveCount(2);
        diff.Entries.Should().OnlyContain(entry => entry.IsVolatile);
        // The whole point of the volatile flag: free memory always differs, and
        // that must not be presented as a change of the machine.
        diff.HasRelevantChanges.Should().BeFalse();
    }

    [Fact]
    public void Compare_MemoryUpgrade_ReportsModifiedTotal()
    {
        SystemSnapshot before = TestData.Snapshot();
        SystemSnapshot after = before with
        {
            Memory = new MemoryInfo { TotalBytes = 64 * TestData.Gib, AvailableBytes = 18 * TestData.Gib },
        };

        SnapshotDiff diff = _comparer.Compare(before, after);

        DiffEntry entry = diff.Entries.Should().ContainSingle().Subject;
        entry.Category.Should().Be("Memory");
        entry.Property.Should().Be("Total");
        entry.Kind.Should().Be(ChangeKind.Modified);
        entry.OldValue.Should().Be("32.0 GiB");
        entry.NewValue.Should().Be("64.0 GiB");
        diff.HasRelevantChanges.Should().BeTrue();
    }

    [Fact]
    public void Compare_NewDisk_IsReportedAsAdded()
    {
        SystemSnapshot before = TestData.Snapshot();
        SystemSnapshot after = before with
        {
            Disks = [TestData.Disk("C:"), TestData.Disk("E:", totalBytes: 2048 * TestData.Gib)],
        };

        SnapshotDiff diff = _comparer.Compare(before, after);

        DiffEntry entry = diff.Entries.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Added);
        entry.Category.Should().Be("Disk E:");
        entry.OldValue.Should().BeNull();
    }

    [Fact]
    public void Compare_RemovedAdapter_IsReportedAsRemoved()
    {
        SystemSnapshot before = TestData.Snapshot();
        SystemSnapshot after = before with { NetworkAdapters = [] };

        SnapshotDiff diff = _comparer.Compare(before, after);

        DiffEntry entry = diff.Entries.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Removed);
        entry.Category.Should().Be("Network Ethernet");
        entry.NewValue.Should().BeNull();
    }

    [Fact]
    public void Compare_ReorderedDisks_ReportsNothing()
    {
        SystemSnapshot before = TestData.Snapshot() with
        {
            Disks = [TestData.Disk("C:"), TestData.Disk("D:")],
        };
        SystemSnapshot after = before with
        {
            // Same volumes, different order: the operating system is free to
            // enumerate them differently after a reboot.
            Disks = [TestData.Disk("D:"), TestData.Disk("C:")],
        };

        SnapshotDiff diff = _comparer.Compare(before, after);

        diff.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Compare_ReorderedIpAddresses_ReportsNothing()
    {
        NetworkAdapterInfo adapter = TestData.Adapter("Ethernet");
        SystemSnapshot before = TestData.Snapshot() with
        {
            NetworkAdapters = [adapter with { IpAddresses = ["10.0.0.1", "10.0.0.2"] }],
        };
        SystemSnapshot after = before with
        {
            NetworkAdapters = [adapter with { IpAddresses = ["10.0.0.2", "10.0.0.1"] }],
        };

        SnapshotDiff diff = _comparer.Compare(before, after);

        diff.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Compare_EmptySnapshots_DoesNotThrow()
    {
        SystemSnapshot empty = TestData.Snapshot() with { Disks = [], NetworkAdapters = [] };

        SnapshotDiff diff = _comparer.Compare(empty, empty);

        diff.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DuplicateDiskIdentifiers_DoesNotThrow()
    {
        // Broken systems can report the same mount point twice; the comparison
        // must survive that instead of failing with a dictionary exception.
        SystemSnapshot before = TestData.Snapshot() with
        {
            Disks = [TestData.Disk("C:"), TestData.Disk("C:")],
        };
        SystemSnapshot after = TestData.Snapshot();

        Action compare = () => _comparer.Compare(before, after);

        compare.Should().NotThrow();
    }

    [Fact]
    public void Compare_KeepsSnapshotMetadata()
    {
        SystemSnapshot before = TestData.Snapshot(id: 7, createdAt: TestData.Morning);
        SystemSnapshot after = TestData.Snapshot(id: 9, createdAt: TestData.Evening);

        SnapshotDiff diff = _comparer.Compare(before, after);

        diff.LeftSnapshotId.Should().Be(7);
        diff.RightSnapshotId.Should().Be(9);
        diff.LeftCreatedAtUtc.Should().Be(TestData.Morning);
        diff.RightCreatedAtUtc.Should().Be(TestData.Evening);
    }
}
