using Microsoft.Data.Sqlite;

namespace SysDiag.Storage;

/// <summary>
/// Creates and opens connections to the snapshot database. Centralised so that
/// every connection in the application uses the same options - especially
/// "PRAGMA foreign_keys", which SQLite disables by default on each connection.
/// </summary>
internal static class SqliteConnectionFactory
{
    public static async Task<SqliteConnection> OpenAsync(string databasePath, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var connection = new SqliteConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using SqliteCommand pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
