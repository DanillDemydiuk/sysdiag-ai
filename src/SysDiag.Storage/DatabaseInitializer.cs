using Microsoft.Data.Sqlite;

namespace SysDiag.Storage;

/// <summary>
/// Creates the database file and its schema. The application ships no migration
/// tool on purpose: the schema is created with "IF NOT EXISTS" at every start,
/// which is enough for a local, append-only history and keeps the first run to a
/// single command.
/// </summary>
public static class DatabaseInitializer
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS snapshots (
            id                     INTEGER PRIMARY KEY AUTOINCREMENT,
            created_at_utc         TEXT    NOT NULL,
            machine_name           TEXT    NOT NULL,
            collector_name         TEXT    NOT NULL,
            os_platform            TEXT    NOT NULL,
            os_caption             TEXT    NOT NULL,
            os_version             TEXT    NOT NULL,
            os_architecture        TEXT    NOT NULL,
            cpu_name               TEXT    NOT NULL,
            cpu_physical_cores     INTEGER NULL,
            cpu_logical_cores      INTEGER NOT NULL,
            cpu_max_clock_mhz      INTEGER NULL,
            cpu_architecture       TEXT    NOT NULL,
            memory_total_bytes     INTEGER NOT NULL,
            memory_available_bytes INTEGER NULL
        );

        CREATE TABLE IF NOT EXISTS disks (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
            identifier  TEXT    NOT NULL,
            label       TEXT    NULL,
            file_system TEXT    NULL,
            total_bytes INTEGER NOT NULL,
            free_bytes  INTEGER NULL
        );

        CREATE TABLE IF NOT EXISTS network_adapters (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            snapshot_id  INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
            name         TEXT    NOT NULL,
            description  TEXT    NULL,
            mac_address  TEXT    NULL,
            ip_addresses TEXT    NOT NULL,
            speed_mbps   INTEGER NULL,
            is_up        INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_disks_snapshot_id
            ON disks (snapshot_id);

        CREATE INDEX IF NOT EXISTS ix_network_adapters_snapshot_id
            ON network_adapters (snapshot_id);
        """;

    /// <summary>
    /// Makes sure the database file, its directory and all tables exist.
    /// Safe to call on every program start.
    /// </summary>
    public static async Task EnsureCreatedAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using SqliteConnection connection =
            await SqliteConnectionFactory.OpenAsync(databasePath, cancellationToken).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
