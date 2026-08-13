using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;

namespace SysDiag.Storage.Export;

/// <summary>
/// Exports a snapshot as JSON for other tools: a ticket system, a spreadsheet,
/// an inventory script.
/// </summary>
/// <remarks>
/// The document is built by hand instead of serialising <see cref="SystemSnapshot"/>
/// directly. Serialising the model would tie the public file format to internal
/// property names, so renaming a C# property would silently break someone else's
/// script. Here the schema is written out once and stays put.
/// </remarks>
public sealed class JsonSnapshotExporter : ISnapshotExporter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Default escaping turns every non-ASCII character into \uXXXX, which
        // would make German machine names unreadable in the exported file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Version of the file format, so consumers can detect a change.</summary>
    private const int SchemaVersion = 1;

    public string FormatName => "json";

    public string FileExtension => ".json";

    public string Render(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var document = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["id"] = snapshot.Id,
            ["createdAtUtc"] = snapshot.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["machineName"] = snapshot.MachineName,
            ["collector"] = snapshot.CollectorName,
            ["operatingSystem"] = new JsonObject
            {
                ["platform"] = snapshot.Os.Platform,
                ["name"] = snapshot.Os.Caption,
                ["version"] = snapshot.Os.Version,
                ["architecture"] = snapshot.Os.Architecture,
            },
            ["cpu"] = new JsonObject
            {
                ["name"] = snapshot.Cpu.Name,
                ["physicalCores"] = snapshot.Cpu.PhysicalCores,
                ["logicalCores"] = snapshot.Cpu.LogicalCores,
                ["maxClockMhz"] = snapshot.Cpu.MaxClockMhz,
                ["architecture"] = snapshot.Cpu.Architecture,
            },
            ["memory"] = new JsonObject
            {
                // Raw bytes, never a rounded "31.1 GiB": a consumer can format,
                // but it cannot undo a rounding.
                ["totalBytes"] = snapshot.Memory.TotalBytes,
                ["availableBytes"] = snapshot.Memory.AvailableBytes,
            },
            ["disks"] = new JsonArray(snapshot.Disks.Select(MapDisk).ToArray<JsonNode?>()),
            ["networkAdapters"] = new JsonArray(snapshot.NetworkAdapters.Select(MapAdapter).ToArray<JsonNode?>()),
        };

        return document.ToJsonString(WriteOptions);
    }

    private static JsonNode MapDisk(DiskInfo disk) => new JsonObject
    {
        ["identifier"] = disk.Identifier,
        ["label"] = disk.Label,
        ["fileSystem"] = disk.FileSystem,
        ["totalBytes"] = disk.TotalBytes,
        ["freeBytes"] = disk.FreeBytes,
    };

    private static JsonNode MapAdapter(NetworkAdapterInfo adapter) => new JsonObject
    {
        ["name"] = adapter.Name,
        ["description"] = adapter.Description,
        ["macAddress"] = adapter.MacAddress,
        ["ipAddresses"] = new JsonArray(adapter.IpAddresses.Select(address => (JsonNode?)JsonValue.Create(address)).ToArray()),
        ["speedMbps"] = adapter.SpeedMbps,
        ["isUp"] = adapter.IsUp,
    };
}
