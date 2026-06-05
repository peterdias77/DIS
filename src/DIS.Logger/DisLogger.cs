using System.Text.Json;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Logger.Sqlite;
using Microsoft.Extensions.Logging;

namespace DIS.Logger;

/// <summary>
/// Event-driven structured logger.
/// Every call:
///   1. Serialises the event to JSON
///   2. Writes to SQLite via SqliteLogWriter
///   3. Publishes to connected Dashboard clients via ISignalRPublisher
///
/// Only logs when values actually change — no-op if previous == current.
/// </summary>
public sealed class DisLogger : IDISLogger
{
    private readonly SqliteLogWriter    _db;
    private readonly ISignalRPublisher  _publisher;
    private readonly ILogger<DisLogger> _inner;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented        = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DisLogger(
        SqliteLogWriter    db,
        ISignalRPublisher  publisher,
        ILogger<DisLogger> inner)
    {
        _db        = db;
        _publisher = publisher;
        _inner     = inner;
    }

    // ── State change ─────────────────────────────────────────────────────────

    public void LogStateChange<TState>(
        string asset, int cycleId, string stateName,
        TState previous, TState current)
        where TState : Enum
    {
        if (EqualityComparer<TState>.Default.Equals(previous, current)) return;

        var payload = new
        {
            state    = stateName,
            from     = previous.ToString(),
            to       = current.ToString()
        };

        Fire("state_change", asset, cycleId, payload, "INFO");
    }

    // ── Output change ────────────────────────────────────────────────────────

    public void LogOutputChange<TOutput>(
        string asset, int cycleId, string outputName,
        TOutput previous, TOutput current)
        where TOutput : Enum
    {
        if (EqualityComparer<TOutput>.Default.Equals(previous, current)) return;

        var payload = new
        {
            output   = outputName,
            from     = previous.ToString(),
            to       = current.ToString()
        };

        Fire("output_change", asset, cycleId, payload, "INFO");
    }

    // ── Orchestration change ─────────────────────────────────────────────────

    public void LogOrchestrationChange(
        string asset, int cycleId,
        OrchestrationDecision previous,
        OrchestrationDecision current)
    {
        var payload = new
        {
            portfolio_permission = Change(previous.PortfolioPermission, current.PortfolioPermission),
            market_permission    = Change(previous.MarketPermission,    current.MarketPermission),
            strategy             = Change(previous.Strategy,            current.Strategy),
            direction            = Change(previous.Direction,           current.Direction),
            confidence           = Change(previous.Confidence,          current.Confidence)
        };

        Fire("orchestration_change", asset, cycleId, payload, "INFO");
    }

    // ── Entry ────────────────────────────────────────────────────────────────

    public void LogEntry(string asset, int cycleId, EntryEvent e)
    {
        var payload = new
        {
            signal = e.Signal.ToString(),
            price  = e.Price,
            reason = e.Reason
        };

        Fire("entry", asset, cycleId, payload, "INFO");
    }

    // ── Trade control ────────────────────────────────────────────────────────

    public void LogTradeControl(string asset, int cycleId, TradeControlEvent e)
    {
        var payload = new
        {
            active_ranks        = e.ActiveRanks,
            next_rank           = e.NextRank,
            trading_permission  = e.TradingPermission.ToString(),
            market_permission   = e.MarketPermission.ToString()
        };

        Fire("trade_control", asset, cycleId, payload, "INFO");
    }

    // ── Risk ─────────────────────────────────────────────────────────────────

    public void LogRisk(string asset, int cycleId, RiskEvent e)
    {
        var payload = new
        {
            entry_size    = e.Size.ToString(),
            risk_percent  = e.RiskPercent,
            stop_loss     = e.StopLossPrice,
            sl_distance   = e.SlDistance,
            position_size = e.PositionSize
        };

        Fire("risk", asset, cycleId, payload, "INFO");
    }

    // ── Execution ────────────────────────────────────────────────────────────

    public void LogExecution(string asset, int cycleId, ExecutionEvent e)
    {
        var payload = new
        {
            order_type       = e.OrderType.ToString(),
            execution_price  = e.ExecutionPrice,
            slippage         = e.Slippage
        };

        Fire("execution", asset, cycleId, payload, "INFO");
    }

    // ── Exit ─────────────────────────────────────────────────────────────────

    public void LogExit(string asset, int cycleId, ExitEvent e)
    {
        var level = e.State == DIS.Core.Enums.PositionState.Closed ? "INFO" : "WARNING";

        var payload = new
        {
            position_state = e.State.ToString(),
            reason         = e.Reason,
            tp_hit         = e.TpHit,
            exit_price     = e.ExitPrice,
            pnl            = e.Pnl
        };

        Fire("exit", asset, cycleId, payload, level);
    }

    // ── Core fire-and-forget pipeline ────────────────────────────────────────

    private void Fire(string eventType, string asset, int cycleId, object payload, string logLevel)
    {
        // Run DB write + SignalR publish on a background thread
        // so the trading loop is never blocked
        _ = Task.Run(async () =>
        {
            try
            {
                var rowId = await _db.WriteAsync(
                    eventType, DateTime.UtcNow, asset, cycleId, payload, logLevel);

                var entry = new DISLogEntry
                {
                    Id        = rowId,
                    EventType = eventType,
                    Timestamp = DateTime.UtcNow,
                    Asset     = asset,
                    CycleId   = cycleId,
                    Payload   = JsonSerializer.Serialize(payload, _jsonOptions),
                    LogLevel  = logLevel
                };

                await _publisher.PublishAsync(entry);
            }
            catch (Exception ex)
            {
                _inner.LogError(ex, "DIS Logger pipeline failed for event {EventType} on {Asset}", eventType, asset);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object Change<T>(T from, T to) =>
        new { from = from!.ToString(), to = to!.ToString() };
}
