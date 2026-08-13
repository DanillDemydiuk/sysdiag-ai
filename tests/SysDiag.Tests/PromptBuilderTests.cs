using FluentAssertions;
using SysDiag.Core.Models;
using SysDiag.Llm;

namespace SysDiag.Tests;

/// <summary>
/// Tests for the prompt. Two things are checked: that the model gets the facts
/// it needs, and that it does not get anything that identifies the machine.
/// </summary>
public sealed class PromptBuilderTests
{
    [Fact]
    public void Build_ContainsTheHardwareFacts()
    {
        string prompt = PromptBuilder.Build(TestData.Snapshot());

        prompt.Should().Contain("Windows 11 Pro");
        prompt.Should().Contain("AMD Ryzen 5 5600X 6-Core Processor");
        prompt.Should().Contain("6 physical, 12 logical");
        prompt.Should().Contain("32.0 GiB total");
        prompt.Should().Contain("C: (NTFS)");
    }

    [Fact]
    public void Build_DoesNotLeakNetworkIdentifiers()
    {
        SystemSnapshot snapshot = TestData.Snapshot();

        string prompt = PromptBuilder.Build(snapshot);

        // The adapter itself is described, but MAC and IP addresses stay local.
        prompt.Should().Contain("Ethernet");
        prompt.Should().NotContain("00:1A:2B:3C:4D:5E");
        prompt.Should().NotContain("192.168.1.42");
    }

    [Fact]
    public void Build_DescribesAnAdapterAsInstalledBeforeItsLinkState()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            NetworkAdapters = [TestData.Adapter("Ethernet", isUp: false)],
        };

        // A small model turned the earlier wording "not connected" into "the
        // Ethernet port is missing" - a statement about hardware that does exist.
        PromptBuilder.Build(snapshot).Should().Contain("Ethernet: installed, link down");
    }

    [Fact]
    public void Build_NamesWhatThePercentageDescribes()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            Disks = [TestData.Disk("C:", totalBytes: 100 * TestData.Gib, freeBytes: 25 * TestData.Gib)],
        };

        string prompt = PromptBuilder.Build(snapshot);

        // The short form "(75% used)" right after a "free" value was read by a
        // real model as the free share, which turned a full disk into a healthy
        // one. The wording now says what the number means.
        prompt.Should().Contain("75 percent of the capacity is in use");
        prompt.Should().NotContain("75% used");
        // The percentage has to stand before the sizes, otherwise a model reads it
        // as a property of the free space that follows it.
        prompt.Should().MatchRegex(@"75 percent of the capacity is in use;.*total capacity");
    }

    [Fact]
    public void Build_AlmostFullDisk_IsAlsoLabelledInWords()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            Disks = [TestData.Disk("C:", totalBytes: 100 * TestData.Gib, freeBytes: 10 * TestData.Gib)],
        };

        PromptBuilder.Build(snapshot).Should().Contain("this disk is almost full");
    }

    [Fact]
    public void Build_HealthyDisk_IsNotLabelledAsAlmostFull()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            Disks = [TestData.Disk("C:", totalBytes: 100 * TestData.Gib, freeBytes: 60 * TestData.Gib)],
        };

        PromptBuilder.Build(snapshot).Should().NotContain("almost full");
    }

    [Fact]
    public void Build_ForbidsRecalculatingNumbers()
    {
        PromptBuilder.Build(TestData.Snapshot())
            .Should().Contain("Repeat every number exactly as written.");
    }

    [Fact]
    public void Build_RespectsTheConfiguredLanguage()
    {
        string prompt = PromptBuilder.Build(TestData.Snapshot(), responseLanguage: "English");

        prompt.Should().Contain("Explain the following computer configuration in English.");
    }

    [Fact]
    public void Build_WithoutDisksAndAdapters_StatesThatExplicitly()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with { Disks = [], NetworkAdapters = [] };

        string prompt = PromptBuilder.Build(snapshot);

        // Saying "none reported" prevents the model from inventing hardware to
        // fill the gap.
        prompt.Should().Contain("Disks: none reported");
        prompt.Should().Contain("Network adapters: none reported");
    }

    [Fact]
    public void Build_WithUnknownValues_UsesThePlaceholderInsteadOfEmptyText()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            Memory = new MemoryInfo { TotalBytes = 8 * TestData.Gib, AvailableBytes = null },
            Cpu = TestData.Snapshot().Cpu with { PhysicalCores = null },
        };

        string prompt = PromptBuilder.Build(snapshot);

        prompt.Should().Contain("n/a available");
        prompt.Should().Contain("12 logical");
    }
}
