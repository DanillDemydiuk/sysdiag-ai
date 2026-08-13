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
    public void Build_AddsUsedPercentagePerDisk()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            Disks = [TestData.Disk("C:", totalBytes: 100 * TestData.Gib, freeBytes: 25 * TestData.Gib)],
        };

        PromptBuilder.Build(snapshot).Should().Contain("75% used");
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
