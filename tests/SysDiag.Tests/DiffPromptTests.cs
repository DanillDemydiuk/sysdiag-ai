using FluentAssertions;
using SysDiag.Core.Diff;
using SysDiag.Llm;

namespace SysDiag.Tests;

/// <summary>
/// Tests for the prompt that describes a comparison. The risk here is the
/// opposite of the snapshot prompt: an empty or purely fluctuating diff invites
/// a model to invent a problem, so the wording has to close that door.
/// </summary>
public sealed class DiffPromptTests
{
    private static readonly DateTimeOffset Earlier = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private static SnapshotDiff Diff(params DiffEntry[] entries) => new()
    {
        LeftSnapshotId = 1,
        RightSnapshotId = 2,
        LeftCreatedAtUtc = Earlier,
        RightCreatedAtUtc = Later,
        Entries = entries,
    };

    private static DiffEntry Modified(string category, string property, string oldValue, string newValue, bool isVolatile = false) => new()
    {
        Category = category,
        Property = property,
        OldValue = oldValue,
        NewValue = newValue,
        Kind = ChangeKind.Modified,
        IsVolatile = isVolatile,
    };

    [Fact]
    public void BuildForDiff_DescribesAModifiedValueWithBothSides()
    {
        SnapshotDiff diff = Diff(Modified("Memory", "Total", "32.0 GiB", "64.0 GiB"));

        string prompt = PromptBuilder.BuildForDiff(diff);

        prompt.Should().Contain("Memory, Total: was 32.0 GiB, is now 64.0 GiB");
        prompt.Should().Contain("2026-08-01 09:00 UTC");
        prompt.Should().Contain("2026-08-08 09:00 UTC");
    }

    [Fact]
    public void BuildForDiff_MarksFluctuatingEntriesOnly()
    {
        SnapshotDiff diff = Diff(
            Modified("Memory", "Available", "18.0 GiB", "12.0 GiB", isVolatile: true),
            Modified("CPU", "Model", "Ryzen 5", "Ryzen 7"));

        string prompt = PromptBuilder.BuildForDiff(diff);

        prompt.Should().Contain("Memory, Available: was 18.0 GiB, is now 12.0 GiB [FLUCTUATING]");
        // Asserted without a line ending on purpose: AppendLine writes \r\n on
        // Windows and \n on the Linux CI agent.
        prompt.Should().Contain("CPU, Model: was Ryzen 5, is now Ryzen 7");
        prompt.Should().NotContain("Ryzen 7 [FLUCTUATING]");
    }

    [Fact]
    public void BuildForDiff_AddedAndRemovedItemsReadAsPresence()
    {
        SnapshotDiff diff = Diff(
            new DiffEntry { Category = "Disk E:", Property = "Volume", NewValue = "2.0 TiB (exFAT)", Kind = ChangeKind.Added },
            new DiffEntry { Category = "Network Wi-Fi", Property = "Adapter", OldValue = "Intel AX200 (up)", Kind = ChangeKind.Removed });

        string prompt = PromptBuilder.BuildForDiff(diff);

        prompt.Should().Contain("Disk E:: newly present - 2.0 TiB (exFAT)");
        prompt.Should().Contain("Network Wi-Fi: no longer present - Intel AX200 (up)");
    }

    [Fact]
    public void BuildForDiff_EmptyDiff_TellsTheModelToSayNothingChanged()
    {
        string prompt = PromptBuilder.BuildForDiff(Diff());

        // Silence would be an invitation to invent changes.
        prompt.Should().Contain("No differences were found at all.");
    }

    [Fact]
    public void BuildForDiff_OnlyFluctuatingEntries_StatesThatTheConfigurationIsUnchanged()
    {
        SnapshotDiff diff = Diff(
            Modified("Memory", "Available", "18.0 GiB", "12.0 GiB", isVolatile: true),
            Modified("Disk C:", "Free space", "412 GiB", "373 GiB", isVolatile: true));

        string prompt = PromptBuilder.BuildForDiff(diff);

        prompt.Should().Contain("All listed entries are fluctuating values.");
    }

    [Fact]
    public void BuildForDiff_RealChangePresent_DoesNotClaimAnUnchangedConfiguration()
    {
        SnapshotDiff diff = Diff(
            Modified("Memory", "Available", "18.0 GiB", "12.0 GiB", isVolatile: true),
            Modified("Memory", "Total", "32.0 GiB", "64.0 GiB"));

        PromptBuilder.BuildForDiff(diff).Should().NotContain("All listed entries are fluctuating values.");
    }

    [Fact]
    public void BuildForDiff_KeepsTheMarkerOutOfTheAnswer()
    {
        // The marker helps the model classify, but a live run printed it verbatim
        // into the German text shown to the user.
        PromptBuilder.BuildForDiff(Diff())
            .Should().Contain("Never write the word FLUCTUATING in your answer");
    }

    [Fact]
    public void BuildForDiff_RespectsTheConfiguredLanguage()
    {
        PromptBuilder.BuildForDiff(Diff(), responseLanguage: "English")
            .Should().Contain("Explain in English what changed");
    }
}
