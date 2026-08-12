using System.Globalization;
using Microsoft.Data.Sqlite;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;

namespace SysDiag.Storage;

/// <summary>
/// Stores snapshots in a local SQLite file. SQLite was chosen because it needs no
/// server, no configuration and no account: the whole history is one file the
/// user can copy, inspect or delete.
/// </summary>
public sealed class SqliteSnapshotRepository : ISnapshotRepository
{
    /// <summary>Separator for the IP address list inside a single text column.</summary>
    private const char IpAddressSeparator = '\n';

    private readonly string _databasePath;

    public SqliteSnapshotRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    /// <summary>
    /// Writes the snapshot and its child rows in one transaction: a snapshot
    /// without its disks would be worse than no snapshot, because a later diff
    /// would report every disk as removed.
    /// </summary>
    public async Task<long> SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using SqliteConnection connection =
            await SqliteConnectionFactory.OpenAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        long snapshotId = await InsertSnapshotAsync(connection, transaction, snapshot, cancellationToken)
            .ConfigureAwait(false);

        foreach (DiskInfo disk in snapshot.Disks)
        {
            await InsertDiskAsync(connection, transaction, snapshotId, disk, cancellationToken).ConfigureAwait(false);
        }

        foreach (NetworkAdapterInfo adapter in snapshot.NetworkAdapters)
        {
            await InsertAdapterAsync(connection, transaction, snapshotId, adapter, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return snapshotId;
    }

    public async Task<SystemSnapshot?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await SqliteConnectionFactory.OpenAsync(_databasePath, cancellationToken).ConfigureAwait(false);

        return await ReadSnapshotAsync(
            connection,
            "SELECT * FROM snapshots WHERE id = $id;",
            command => command.Parameters.AddWithValue("$id", id),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SystemSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await SqliteConnectionFactory.OpenAsync(_databasePath, cancellationToken).ConfigureAwait(false);

        // Ordering by id, not by timestamp: the id is strictly increasing, while
        // a wall clock can jump backwards after a time synchronisation.
        return await ReadSnapshotAsync(
            connection,
            "SELECT * FROM snapshots ORDER BY id DESC LIMIT 1;",
            _ => { },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SnapshotSummary>> ListAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await SqliteConnectionFactory.OpenAsync(_databasePath, cancellationToken).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, created_at_utc, machine_name, collector_name, os_caption
            FROM snapshots
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var summaries = new List<SnapshotSummary>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            summaries.Add(new SnapshotSummary
            {
                Id = reader.GetInt64(0),
                CreatedAtUtc = ParseTimestamp(reader.GetString(1)),
                MachineName = reader.GetString(2),
                CollectorName = reader.GetString(3),
                OsCaption = reader.GetString(4),
            });
        }

        return summaries;
    }

    private static async Task<long> InsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SystemSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO snapshots (
                created_at_utc, machine_name, collector_name,
                os_platform, os_caption, os_version, os_architecture,
                cpu_name, cpu_physical_cores, cpu_logical_cores, cpu_max_clock_mhz, cpu_architecture,
                memory_total_bytes, memory_available_bytes)
            VALUES (
                $createdAt, $machineName, $collectorName,
                $osPlatform, $osCaption, $osVersion, $osArchitecture,
                $cpuName, $cpuPhysicalCores, $cpuLogicalCores, $cpuMaxClockMhz, $cpuArchitecture,
                $memoryTotal, $memoryAvailable);

            SELECT last_insert_rowid();
            """;

        AddParameter(command, "$createdAt", FormatTimestamp(snapshot.CreatedAtUtc));
        AddParameter(command, "$machineName", snapshot.MachineName);
        AddParameter(command, "$collectorName", snapshot.CollectorName);
        AddParameter(command, "$osPlatform", snapshot.Os.Platform);
        AddParameter(command, "$osCaption", snapshot.Os.Caption);
        AddParameter(command, "$osVersion", snapshot.Os.Version);
        AddParameter(command, "$osArchitecture", snapshot.Os.Architecture);
        AddParameter(command, "$cpuName", snapshot.Cpu.Name);
        AddParameter(command, "$cpuPhysicalCores", snapshot.Cpu.PhysicalCores);
        AddParameter(command, "$cpuLogicalCores", snapshot.Cpu.LogicalCores);
        AddParameter(command, "$cpuMaxClockMhz", snapshot.Cpu.MaxClockMhz);
        AddParameter(command, "$cpuArchitecture", snapshot.Cpu.Architecture);
        AddParameter(command, "$memoryTotal", snapshot.Memory.TotalBytes);
        AddParameter(command, "$memoryAvailable", snapshot.Memory.AvailableBytes);

        object? id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(id, CultureInfo.InvariantCulture);
    }

    private static async Task InsertDiskAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long snapshotId,
        DiskInfo disk,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO disks (snapshot_id, identifier, label, file_system, total_bytes, free_bytes)
            VALUES ($snapshotId, $identifier, $label, $fileSystem, $totalBytes, $freeBytes);
            """;

        AddParameter(command, "$snapshotId", snapshotId);
        AddParameter(command, "$identifier", disk.Identifier);
        AddParameter(command, "$label", disk.Label);
        AddParameter(command, "$fileSystem", disk.FileSystem);
        AddParameter(command, "$totalBytes", disk.TotalBytes);
        AddParameter(command, "$freeBytes", disk.FreeBytes);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertAdapterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long snapshotId,
        NetworkAdapterInfo adapter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO network_adapters (snapshot_id, name, description, mac_address, ip_addresses, speed_mbps, is_up)
            VALUES ($snapshotId, $name, $description, $macAddress, $ipAddresses, $speedMbps, $isUp);
            """;

        AddParameter(command, "$snapshotId", snapshotId);
        AddParameter(command, "$name", adapter.Name);
        AddParameter(command, "$description", adapter.Description);
        AddParameter(command, "$macAddress", adapter.MacAddress);
        AddParameter(command, "$ipAddresses", string.Join(IpAddressSeparator, adapter.IpAddresses));
        AddParameter(command, "$speedMbps", adapter.SpeedMbps);
        AddParameter(command, "$isUp", adapter.IsUp ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SystemSnapshot?> ReadSnapshotAsync(
        SqliteConnection connection,
        string sql,
        Action<SqliteCommand> configureParameters,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters(command);

        long snapshotId;
        SystemSnapshot snapshot;

        await using (SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            snapshotId = reader.GetInt64(reader.GetOrdinal("id"));
            snapshot = MapSnapshot(reader, snapshotId);
        }

        return snapshot with
        {
            Disks = await ReadDisksAsync(connection, snapshotId, cancellationToken).ConfigureAwait(false),
            NetworkAdapters = await ReadAdaptersAsync(connection, snapshotId, cancellationToken).ConfigureAwait(false),
        };
    }

    private static SystemSnapshot MapSnapshot(SqliteDataReader reader, long snapshotId) => new()
    {
        Id = snapshotId,
        CreatedAtUtc = ParseTimestamp(reader.GetString(reader.GetOrdinal("created_at_utc"))),
        MachineName = reader.GetString(reader.GetOrdinal("machine_name")),
        CollectorName = reader.GetString(reader.GetOrdinal("collector_name")),
        Os = new OsInfo
        {
            Platform = reader.GetString(reader.GetOrdinal("os_platform")),
            Caption = reader.GetString(reader.GetOrdinal("os_caption")),
            Version = reader.GetString(reader.GetOrdinal("os_version")),
            Architecture = reader.GetString(reader.GetOrdinal("os_architecture")),
        },
        Cpu = new CpuInfo
        {
            Name = reader.GetString(reader.GetOrdinal("cpu_name")),
            PhysicalCores = ReadNullableInt32(reader, "cpu_physical_cores"),
            LogicalCores = reader.GetInt32(reader.GetOrdinal("cpu_logical_cores")),
            MaxClockMhz = ReadNullableInt32(reader, "cpu_max_clock_mhz"),
            Architecture = reader.GetString(reader.GetOrdinal("cpu_architecture")),
        },
        Memory = new MemoryInfo
        {
            TotalBytes = reader.GetInt64(reader.GetOrdinal("memory_total_bytes")),
            AvailableBytes = ReadNullableInt64(reader, "memory_available_bytes"),
        },
    };

    private static async Task<IReadOnlyList<DiskInfo>> ReadDisksAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT identifier, label, file_system, total_bytes, free_bytes
            FROM disks
            WHERE snapshot_id = $snapshotId
            ORDER BY identifier;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        var disks = new List<DiskInfo>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            disks.Add(new DiskInfo
            {
                Identifier = reader.GetString(0),
                Label = reader.IsDBNull(1) ? null : reader.GetString(1),
                FileSystem = reader.IsDBNull(2) ? null : reader.GetString(2),
                TotalBytes = reader.GetInt64(3),
                FreeBytes = reader.IsDBNull(4) ? null : reader.GetInt64(4),
            });
        }

        return disks;
    }

    private static async Task<IReadOnlyList<NetworkAdapterInfo>> ReadAdaptersAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, description, mac_address, ip_addresses, speed_mbps, is_up
            FROM network_adapters
            WHERE snapshot_id = $snapshotId
            ORDER BY name;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        var adapters = new List<NetworkAdapterInfo>();

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            adapters.Add(new NetworkAdapterInfo
            {
                Name = reader.GetString(0),
                Description = reader.IsDBNull(1) ? null : reader.GetString(1),
                MacAddress = reader.IsDBNull(2) ? null : reader.GetString(2),
                IpAddresses = SplitIpAddresses(reader.GetString(3)),
                SpeedMbps = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                IsUp = reader.GetInt64(5) != 0,
            });
        }

        return adapters;
    }

    private static IReadOnlyList<string> SplitIpAddresses(string value) =>
        value.Split(IpAddressSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// SQLite has no date type. Timestamps are stored in ISO 8601 round-trip
    /// format, which sorts correctly as text and never depends on a locale.
    /// </summary>
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static int? ReadNullableInt32(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long? ReadNullableInt64(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    /// <summary>
    /// Adds a parameter and maps <c>null</c> to <see cref="DBNull"/>, which ADO.NET
    /// requires: a C# null in a parameter value means "parameter not set".
    /// </summary>
    private static void AddParameter(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
}
