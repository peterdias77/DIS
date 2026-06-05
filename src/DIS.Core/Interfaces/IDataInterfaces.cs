using DIS.Core.Models;

namespace DIS.Core.Interfaces;

/// <summary>
/// Publishes a log entry to all connected SignalR clients.
/// Implemented in DIS.Engine (SignalRPublisher).
/// Called by DIS.Logger after every SQLite write.
/// </summary>
public interface ISignalRPublisher
{
    Task PublishAsync(DISLogEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Reads historical log entries from SQLite.
/// Implemented in DIS.Logger (SqliteLogWriter).
/// Called by DIS.Dashboard on startup to hydrate the UI with history.
/// </summary>
public interface ILogReader
{
    /// <summary>Returns the most recent N entries, newest first.</summary>
    Task<IReadOnlyList<DISLogEntry>> GetRecentAsync(int count = 500, CancellationToken ct = default);

    /// <summary>Returns all entries for a specific asset, newest first.</summary>
    Task<IReadOnlyList<DISLogEntry>> GetByAssetAsync(string asset, int count = 200, CancellationToken ct = default);

    /// <summary>Returns all entries of a specific event type, newest first.</summary>
    Task<IReadOnlyList<DISLogEntry>> GetByEventTypeAsync(string eventType, int count = 200, CancellationToken ct = default);

    /// <summary>Returns entries within a time range.</summary>
    Task<IReadOnlyList<DISLogEntry>> GetByTimeRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
