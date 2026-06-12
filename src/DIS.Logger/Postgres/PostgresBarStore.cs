using System.Globalization;
using DIS.Core.Models;
using Npgsql;
using NpgsqlTypes;

namespace DIS.Logger.Postgres;

/// <summary>
/// Persists OHLC bars to PostgreSQL and serves historical queries.
///
/// Table: dis.ohlc_bars
/// Unique index: (symbol, timeframe, bar_time) — INSERT ON CONFLICT DO NOTHING
/// guarantees deduplication on reconnect without errors.
///
/// Design decisions:
///   • Decimal prices stored as NUMERIC(20,8) — no float precision loss.
///   • NpgsqlDataSource (pooled) — all methods borrow from the shared pool.
///   • Bulk bootstrap uses COPY FROM STDIN (binary) — fastest Postgres insert path.
///   • All public methods are async, allocation-lean, CancellationToken-aware.
/// </summary>
public sealed class PostgresBarStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _ds;

    public PostgresBarStore(string connectionString)
    {
        _ds = NpgsqlDataSource.Create(connectionString);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates schema objects if they do not already exist.
    /// Safe to call on every startup (idempotent).
    /// </summary>
    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS dis;

            CREATE TABLE IF NOT EXISTS dis.ohlc_bars (
                id          BIGSERIAL       PRIMARY KEY,
                symbol      TEXT            NOT NULL,
                timeframe   TEXT            NOT NULL,
                bar_time    TIMESTAMPTZ     NOT NULL,
                open        NUMERIC(20,8)   NOT NULL,
                high        NUMERIC(20,8)   NOT NULL,
                low         NUMERIC(20,8)   NOT NULL,
                close       NUMERIC(20,8)   NOT NULL,
                volume      BIGINT          NOT NULL,
                received_at TIMESTAMPTZ     NOT NULL DEFAULT NOW()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS uix_ohlc_bars_symbol_tf_time
                ON dis.ohlc_bars (symbol, timeframe, bar_time);

            CREATE INDEX IF NOT EXISTS ix_ohlc_bars_symbol_time
                ON dis.ohlc_bars (symbol, bar_time DESC);
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Write — single bar ────────────────────────────────────────────────────

    /// <summary>
    /// Persists one bar. ON CONFLICT DO NOTHING — safe to call on duplicates.
    /// Returns true if inserted (new bar), false if skipped (duplicate).
    /// </summary>
    public async Task<bool> WriteBarAsync(OhlcBar bar, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dis.ohlc_bars
                (symbol, timeframe, bar_time, open, high, low, close, volume)
            VALUES
                ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (symbol, timeframe, bar_time) DO NOTHING;
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue(bar.Symbol);
        cmd.Parameters.AddWithValue(bar.Timeframe);
        cmd.Parameters.AddWithValue(bar.Time.ToUniversalTime());
        cmd.Parameters.AddWithValue(bar.Open);
        cmd.Parameters.AddWithValue(bar.High);
        cmd.Parameters.AddWithValue(bar.Low);
        cmd.Parameters.AddWithValue(bar.Close);
        cmd.Parameters.AddWithValue(bar.Volume);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ── Write — bulk (COPY) ───────────────────────────────────────────────────

    /// <summary>
    /// Bulk-inserts a batch of bars using PostgreSQL COPY FROM STDIN (binary).
    /// This is the fastest insert path — used for the history bootstrap.
    /// Duplicate bars are skipped after the COPY via a dedup step.
    ///
    /// Strategy:
    ///   1. COPY into a temp table (no constraints — never fails).
    ///   2. INSERT … ON CONFLICT DO NOTHING from temp → permanent table.
    ///   3. DROP temp table.
    ///
    /// Returns count of rows actually inserted into the permanent table.
    /// </summary>
    public async Task<int> WriteBarsAsync(IReadOnlyList<OhlcBar> bars, CancellationToken ct = default)
    {
        if (bars.Count == 0) return 0;

        await using var conn = await _ds.OpenConnectionAsync(ct);

        // ── Step 1: create temp table ─────────────────────────────────────────
        await using (var cmd = new NpgsqlCommand("""
            CREATE TEMP TABLE tmp_ohlc_bars (
                symbol    TEXT,
                timeframe TEXT,
                bar_time  TIMESTAMPTZ,
                open      NUMERIC(20,8),
                high      NUMERIC(20,8),
                low       NUMERIC(20,8),
                close     NUMERIC(20,8),
                volume    BIGINT
            ) ON COMMIT DROP;
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── Step 2: COPY into temp ────────────────────────────────────────────
        await using (var writer = await conn.BeginBinaryImportAsync(
            "COPY tmp_ohlc_bars (symbol, timeframe, bar_time, open, high, low, close, volume) FROM STDIN (FORMAT BINARY)",
            ct))
        {
            foreach (var bar in bars)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(bar.Symbol,                NpgsqlDbType.Text,        ct);
                await writer.WriteAsync(bar.Timeframe,             NpgsqlDbType.Text,        ct);
                await writer.WriteAsync(bar.Time.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
                await writer.WriteAsync(bar.Open,                  NpgsqlDbType.Numeric,     ct);
                await writer.WriteAsync(bar.High,                  NpgsqlDbType.Numeric,     ct);
                await writer.WriteAsync(bar.Low,                   NpgsqlDbType.Numeric,     ct);
                await writer.WriteAsync(bar.Close,                 NpgsqlDbType.Numeric,     ct);
                await writer.WriteAsync(bar.Volume,                NpgsqlDbType.Bigint,      ct);
            }
            await writer.CompleteAsync(ct);
        }

        // ── Step 3: dedup insert into permanent table ─────────────────────────
        await using var insertCmd = new NpgsqlCommand("""
            INSERT INTO dis.ohlc_bars
                (symbol, timeframe, bar_time, open, high, low, close, volume)
            SELECT symbol, timeframe, bar_time, open, high, low, close, volume
            FROM   tmp_ohlc_bars
            ON CONFLICT (symbol, timeframe, bar_time) DO NOTHING;
            """, conn);

        int inserted = await insertCmd.ExecuteNonQueryAsync(ct);
        return inserted;
    }

    // ── Read — recent bars ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the N most recent bars for a symbol, ordered oldest → newest.
    /// Suitable for feeding directly into state calculators.
    /// </summary>
    public async Task<IReadOnlyList<OhlcBar>> GetRecentAsync(
        string symbol, string timeframe, int count, CancellationToken ct = default)
    {
        const string sql = """
            SELECT symbol, timeframe, bar_time, open, high, low, close, volume
            FROM   dis.ohlc_bars
            WHERE  symbol = $1 AND timeframe = $2
            ORDER  BY bar_time DESC
            LIMIT  $3;
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(symbol);
        cmd.Parameters.AddWithValue(timeframe);
        cmd.Parameters.AddWithValue(count);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<OhlcBar>();

        while (await reader.ReadAsync(ct))
        {
            results.Add(new OhlcBar
            {
                Symbol    = reader.GetString(0),
                Timeframe = reader.GetString(1),
                Time      = reader.GetDateTime(2).ToUniversalTime(),
                Open      = reader.GetDecimal(3),
                High      = reader.GetDecimal(4),
                Low       = reader.GetDecimal(5),
                Close     = reader.GetDecimal(6),
                Volume    = reader.GetInt64(7)
            });
        }

        // Result set is DESC from DB; reverse to oldest → newest for callers
        results.Reverse();
        return results;
    }

    // ── Read — feed health queries ────────────────────────────────────────────

    /// <summary>
    /// Returns total bar count per symbol.
    /// Used by BarRouterService to populate FeedHealthSnapshot.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, long>> GetBarCountsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT symbol, COUNT(*) AS cnt
            FROM   dis.ohlc_bars
            GROUP  BY symbol
            ORDER  BY symbol;
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new Dictionary<string, long>();
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt64(1);

        return result;
    }

    /// <summary>
    /// Returns the UTC timestamp of the most recent bar per symbol.
    /// Used by BarRouterService to detect stale feeds.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, DateTime>> GetLastBarTimesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT symbol, MAX(bar_time) AS last_time
            FROM   dis.ohlc_bars
            GROUP  BY symbol
            ORDER  BY symbol;
            """;

        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new Dictionary<string, DateTime>();
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetDateTime(1).ToUniversalTime();

        return result;
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync() => await _ds.DisposeAsync();
}
