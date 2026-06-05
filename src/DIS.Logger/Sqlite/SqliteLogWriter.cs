using System.Text.Json;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using Microsoft.Data.Sqlite;

namespace DIS.Logger.Sqlite;

/// <summary>
/// Writes every DIS log event to a SQLite database and implements ILogReader
/// so DIS.Dashboard can query history on startup.
///
/// Database file: dis_logs.db (created alongside the DIS.Engine executable).
/// Table: log_entries
/// </summary>
public sealed class SqliteLogWriter : ILogReader, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim    _lock = new(1, 1);

    public SqliteLogWriter(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        await _connection.OpenAsync(ct);
        await CreateSchemaAsync(ct);
    }

    private async Task CreateSchemaAsync(CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS log_entries (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                event_type  TEXT    NOT NULL,
                timestamp   TEXT    NOT NULL,
                asset       TEXT    NOT NULL,
                cycle_id    INTEGER NOT NULL,
                payload     TEXT    NOT NULL,
                log_level   TEXT    NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_asset
                ON log_entries (asset);

            CREATE INDEX IF NOT EXISTS idx_event_type
                ON log_entries (event_type);

            CREATE INDEX IF NOT EXISTS idx_timestamp
                ON log_entries (timestamp);
            """;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a log entry and returns the assigned row ID.
    /// Thread-safe via semaphore (SQLite single-writer constraint).
    /// </summary>
    public async Task<long> WriteAsync(
        string   eventType,
        DateTime timestamp,
        string   asset,
        int      cycleId,
        object   payload,
        string   logLevel = "INFO",
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented        = false
        });

        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO log_entries (event_type, timestamp, asset, cycle_id, payload, log_level)
                VALUES ($event_type, $timestamp, $asset, $cycle_id, $payload, $log_level);
                SELECT last_insert_rowid();
                """;

            cmd.Parameters.AddWithValue("$event_type", eventType);
            cmd.Parameters.AddWithValue("$timestamp",  timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$asset",      asset);
            cmd.Parameters.AddWithValue("$cycle_id",   cycleId);
            cmd.Parameters.AddWithValue("$payload",    json);
            cmd.Parameters.AddWithValue("$log_level",  logLevel);

            var rowId = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(rowId);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── ILogReader ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DISLogEntry>> GetRecentAsync(
        int count = 500, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload, log_level
            FROM log_entries
            ORDER BY id DESC
            LIMIT $count;
            """;
        return await QueryAsync(sql, cmd =>
            cmd.Parameters.AddWithValue("$count", count), ct);
    }

    public async Task<IReadOnlyList<DISLogEntry>> GetByAssetAsync(
        string asset, int count = 200, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload, log_level
            FROM log_entries
            WHERE asset = $asset
            ORDER BY id DESC
            LIMIT $count;
            """;
        return await QueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("$asset", asset);
            cmd.Parameters.AddWithValue("$count", count);
        }, ct);
    }

    public async Task<IReadOnlyList<DISLogEntry>> GetByEventTypeAsync(
        string eventType, int count = 200, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload, log_level
            FROM log_entries
            WHERE event_type = $event_type
            ORDER BY id DESC
            LIMIT $count;
            """;
        return await QueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("$event_type", eventType);
            cmd.Parameters.AddWithValue("$count",      count);
        }, ct);
    }

    public async Task<IReadOnlyList<DISLogEntry>> GetByTimeRangeAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload, log_level
            FROM log_entries
            WHERE timestamp >= $from AND timestamp <= $to
            ORDER BY id DESC;
            """;
        return await QueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("$from", from.ToString("O"));
            cmd.Parameters.AddWithValue("$to",   to.ToString("O"));
        }, ct);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<DISLogEntry>> QueryAsync(
        string sql,
        Action<SqliteCommand> parameterise,
        CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            parameterise(cmd);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var results = new List<DISLogEntry>();

            while (await reader.ReadAsync(ct))
            {
                results.Add(new DISLogEntry
                {
                    Id        = reader.GetInt64(0),
                    EventType = reader.GetString(1),
                    Timestamp = DateTime.Parse(reader.GetString(2)),
                    Asset     = reader.GetString(3),
                    CycleId   = reader.GetInt32(4),
                    Payload   = reader.GetString(5),
                    LogLevel  = reader.GetString(6)
                });
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
