using System.Text.Json;
using FluentAssertions;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;
using SysDiag.Storage.Export;

namespace SysDiag.Tests;

/// <summary>
/// Tests for both export formats. The JSON schema is treated as a public
/// contract: other tools read it, so a renamed key is a breaking change and has
/// to fail here first.
/// </summary>
public sealed class SnapshotExporterTests
{
    private readonly ISnapshotExporter _json = new JsonSnapshotExporter();
    private readonly ISnapshotExporter _markdown = new MarkdownSnapshotExporter();

    [Fact]
    public void Json_UsesTheAgreedKeysAndRawByteValues()
    {
        SystemSnapshot snapshot = TestData.Snapshot();

        using JsonDocument document = JsonDocument.Parse(_json.Render(snapshot));
        JsonElement root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("machineName").GetString().Should().Be("TEST-PC");
        root.GetProperty("operatingSystem").GetProperty("name").GetString().Should().Be("Windows 11 Pro");
        // Bytes stay bytes: a consumer can round, but cannot undo rounding.
        root.GetProperty("memory").GetProperty("totalBytes").GetInt64().Should().Be(32 * TestData.Gib);
        root.GetProperty("disks").GetArrayLength().Should().Be(1);
        root.GetProperty("disks")[0].GetProperty("identifier").GetString().Should().Be("C:");
    }

    [Fact]
    public void Json_MissingValues_AreNullNotOmitted()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            Memory = new MemoryInfo { TotalBytes = 8 * TestData.Gib, AvailableBytes = null },
        };

        using JsonDocument document = JsonDocument.Parse(_json.Render(snapshot));

        // An explicit null tells a consumer "not measured"; a missing key would be
        // indistinguishable from an older schema.
        document.RootElement.GetProperty("memory").GetProperty("availableBytes")
            .ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Json_KeepsNonAsciiCharactersReadable()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with { MachineName = "BÜRO-PC" };

        _json.Render(snapshot).Should().Contain("BÜRO-PC");
    }

    [Fact]
    public void Json_IsValidForASnapshotWithoutDisksAndAdapters()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with { Disks = [], NetworkAdapters = [] };

        using JsonDocument document = JsonDocument.Parse(_json.Render(snapshot));

        document.RootElement.GetProperty("disks").GetArrayLength().Should().Be(0);
        document.RootElement.GetProperty("networkAdapters").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Markdown_RendersTablesWithFormattedValues()
    {
        string report = _markdown.Render(TestData.Snapshot());

        report.Should().StartWith("# Systembericht TEST-PC");
        report.Should().Contain("| Arbeitsspeicher | 32.0 GiB gesamt, 18.0 GiB verfügbar |");
        report.Should().Contain("## Datenträger");
        report.Should().Contain("| C: | NTFS |");
    }

    [Fact]
    public void Markdown_EscapesPipeCharactersFromTheMachine()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with
        {
            NetworkAdapters = [TestData.Adapter("Eth|0")],
        };

        string report = _markdown.Render(snapshot);

        // An unescaped pipe would silently split the row into extra columns.
        report.Should().Contain(@"| Eth\|0 |");
    }

    [Fact]
    public void Markdown_DoesNotExportNetworkAddresses()
    {
        // An exported report is meant to be shared; the addresses of the local
        // network are not part of that.
        string report = _markdown.Render(TestData.Snapshot());

        report.Should().NotContain("192.168.1.42");
        report.Should().NotContain("00:1A:2B:3C:4D:5E");
    }

    [Fact]
    public void Markdown_EmptySnapshot_StatesItInWords()
    {
        SystemSnapshot snapshot = TestData.Snapshot() with { Disks = [], NetworkAdapters = [] };

        string report = _markdown.Render(snapshot);

        report.Should().Contain("Keine lokalen Datenträger gemeldet.");
        report.Should().Contain("Keine Netzwerkadapter gemeldet.");
    }

    [Fact]
    public void Exporters_AnnounceDistinctFormatNamesAndExtensions()
    {
        _json.FormatName.Should().Be("json");
        _json.FileExtension.Should().Be(".json");
        _markdown.FormatName.Should().Be("markdown");
        _markdown.FileExtension.Should().Be(".md");
    }
}
