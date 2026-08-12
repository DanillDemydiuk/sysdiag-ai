using SysDiag.Core.Models;

namespace SysDiag.Core.Abstractions;

/// <summary>
/// Persistent storage for snapshots. The interface is deliberately narrow: the
/// application only needs to append snapshots and read them back, so there is no
/// update and no delete. SQLite is an implementation detail behind this contract.
/// </summary>
public interface ISnapshotRepository
{
    /// <summary>
    /// Stores a snapshot and returns the id assigned by the database.
    /// </summary>
    Task<long> SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a complete snapshot, or <c>null</c> if no snapshot has that id.
    /// </summary>
    Task<SystemSnapshot?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the most recent snapshot, or <c>null</c> if the database is empty.
    /// Used by "explain" when the user does not pass an id.
    /// </summary>
    Task<SystemSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists stored snapshots, newest first. Returns summaries instead of full
    /// snapshots so that the "list" command does not read every disk and adapter
    /// row just to print a table.
    /// </summary>
    Task<IReadOnlyList<SnapshotSummary>> ListAsync(int limit = 50, CancellationToken cancellationToken = default);
}
