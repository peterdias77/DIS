using System.Text.Json;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using Npgsql;
using NpgsqlTypes;

namespace DIS.Logger.Postgres;

/// <summary>
/// Writes every DIS log event to PostgreSQL and implements ILogReader so
/// DIS.Dashboard can query history on startup.
///
/// Table: dis.log_entries
///
/// Design:
///   • NpgsqlDataSource (pooled) — no manual connection management.
///   • Payload stored as JSONB — enables future indexed queries on state/output fields.
///   • All queries return newest-first (ORDER BY id DESC) matching the existing contract.
///   • ILogReader implementation is a drop-in replacement for the previous log writer implementation.
/// </summary>
public sealed class PostgresLogWriter : ILogReader, IAsyncDisposable
{
    private readonly NpgsqlDataSource _ds;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented        = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PostgresLogWriter(string connectionString)
    {
        _ds = NpgsqlDataSource.Create(connectionString);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS dis;

            CREATE TABLE IF NOT EXISTS dis.log_entries (
                id          BIGSERIAL       PRIMARY KEY,
                event_type  TEXT            NOT NULL,
                timestamp   TIMESTAMPTZ     NOT NULL,
                asset       TEXT            NOT NULL,
                cycle_id    INTEGER         NOT NULL,
                payload     JSONB           NOT NULL,
                log_level   TEXT            NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_log_entries_asset
                ON dis.log_entries (asset);

            CREATE INDEX IF NOT EXISTS ix_log_entries_event_type
                ON dis.log_entries (event_type);

            CREATE INDEX IF NOT EXISTS ix_log_entries_timestamp
                ON dis.log_entries (timestamp DESC);
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a log entry and returns the assigned row ID (BIGSERIAL).
    /// Non-blocking — Npgsql manages the connection pool.
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
        var json = JsonSerializer.Serialize(payload, _jsonOptions);

        const string sql = """
            INSERT INTO dis.log_entries (event_type, timestamp, asset, cycle_id, payload, log_level)
            VALUES ($1, $2, $3, $4, $5::jsonb, $6)
            RETURNING id;
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue(eventType);
        cmd.Parameters.AddWithValue(timestamp.ToUniversalTime());
        cmd.Parameters.AddWithValue(asset);
        cmd.Parameters.AddWithValue(cycleId);
        cmd.Parameters.AddWithValue(json);
        cmd.Parameters.AddWithValue(logLevel);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    // ── ILogReader ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DISLogEntry>> GetRecentAsync(
        int count = 500, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload::text, log_level
            FROM   dis.log_entries
            ORDER  BY id DESC
            LIMIT  $1;
            """;

        return await QueryAsync(sql, cmd => cmd.Parameters.AddWithValue(count), ct);
    }

    public async Task<IReadOnlyList<DISLogEntry>> GetByAssetAsync(
        string asset, int count = 200, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload::text, log_level
            FROM   dis.log_entries
            WHERE  asset = $1
            ORDER  BY id DESC
            LIMIT  $2;
            """;

        return await QueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue(asset);
            cmd.Parameters.AddWithValue(count);
        }, ct);
    }

    public async Task<IReadOnlyList<DISLogEntry>> GetByEventTypeAsync(
        string eventType, int count = 200, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload::text, log_level
            FROM   dis.log_entries
            WHERE  event_type = $1
            ORDER  BY id DESC
            LIMIT  $2;
            """;

        return await QueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue(eventType);
            cmd.Parameters.AddWithValue(count);
        }, ct);
    }

    public async Task<IReadOnlyList<DISLogEntry>> GetByTimeRangeAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, event_type, timestamp, asset, cycle_id, payload::text, log_level
            FROM   dis.log_entries
            WHERE  timestamp >= $1 AND timestamp <= $2
            ORDER  BY id DESC;
            """;

        return await QueryAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue(from.ToUniversalTime());
            cmd.Parameters.AddWithValue(to.ToUniversalTime());
        }, ct);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<DISLogEntry>> QueryAsync(
        string sql,
        Action<NpgsqlCommand> parameterise,
        CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        parameterise(cmd);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<DISLogEntry>();

        while (await reader.ReadAsync(ct))
        {
            results.Add(new DISLogEntry
            {
                Id        = reader.GetInt64(0),
                EventType = reader.GetString(1),
                Timestamp = reader.GetDateTime(2).ToUniversalTime(),
                Asset     = reader.GetString(3),
                CycleId   = reader.GetInt32(4),
                Payload   = reader.GetString(5),
                LogLevel  = reader.GetString(6)
            });
        }

        return results;
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync() => await _ds.DisposeAsync();
}
