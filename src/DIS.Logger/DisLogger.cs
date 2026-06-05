using System.Text.Json;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using Microsoft.Extensions.Logging;

namespace DIS.Logger;

/// <summary>
/// Event-driven structured logger. Emits JSON log entries only when values change.
/// All 34 state changes, 13 output changes, 5 orchestration components,
/// entry decisions, trade control events, risk calculations, executions, and exits
/// are routed through this single interface.
/// </summary>
public sealed class DisLogger : IDISLogger
{
    private readonly ILogger<DisLogger> _inner;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented      = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DisLogger(ILogger<DisLogger> inner) => _inner = inner;

    // ── State change ─────────────────────────────────────────────────────────

    public void LogStateChange<TState>(
        string asset, int cycleId, string stateName,
        TState previous, TState current)
        where TState : Enum
    {
        if (EqualityComparer<TState>.Default.Equals(previous, current)) return;

        var entry = new
        {
            event_type  = "state_change",
            timestamp   = DateTime.UtcNow,
            asset,
            cycle_id    = cycleId,
            state       = stateName,
            from        = previous.ToString(),
            to          = current.ToString()
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Output change ────────────────────────────────────────────────────────

    public void LogOutputChange<TOutput>(
        string asset, int cycleId, string outputName,
        TOutput previous, TOutput current)
        where TOutput : Enum
    {
        if (EqualityComparer<TOutput>.Default.Equals(previous, current)) return;

        var entry = new
        {
            event_type  = "output_change",
            timestamp   = DateTime.UtcNow,
            asset,
            cycle_id    = cycleId,
            output      = outputName,
            from        = previous.ToString(),
            to          = current.ToString()
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Orchestration change ─────────────────────────────────────────────────

    public void LogOrchestrationChange(
        string asset, int cycleId,
        OrchestrationDecision previous,
        OrchestrationDecision current)
    {
        var entry = new
        {
            event_type              = "orchestration_change",
            timestamp               = DateTime.UtcNow,
            asset,
            cycle_id                = cycleId,
            portfolio_permission    = Change(previous.PortfolioPermission, current.PortfolioPermission),
            market_permission       = Change(previous.MarketPermission,    current.MarketPermission),
            strategy                = Change(previous.Strategy,            current.Strategy),
            direction               = Change(previous.Direction,           current.Direction),
            confidence              = Change(previous.Confidence,          current.Confidence)
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Entry ────────────────────────────────────────────────────────────────

    public void LogEntry(string asset, int cycleId, EntryEvent e)
    {
        var entry = new
        {
            event_type  = "entry",
            timestamp   = DateTime.UtcNow,
            asset,
            cycle_id    = cycleId,
            signal      = e.Signal.ToString(),
            price       = e.Price,
            reason      = e.Reason
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Trade control ────────────────────────────────────────────────────────

    public void LogTradeControl(string asset, int cycleId, TradeControlEvent e)
    {
        var entry = new
        {
            event_type          = "trade_control",
            timestamp           = DateTime.UtcNow,
            asset,
            cycle_id            = cycleId,
            active_ranks        = e.ActiveRanks,
            next_rank           = e.NextRank,
            trading_permission  = e.TradingPermission.ToString(),
            market_permission   = e.MarketPermission.ToString()
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Risk ─────────────────────────────────────────────────────────────────

    public void LogRisk(string asset, int cycleId, RiskEvent e)
    {
        var entry = new
        {
            event_type      = "risk",
            timestamp       = DateTime.UtcNow,
            asset,
            cycle_id        = cycleId,
            entry_size      = e.Size.ToString(),
            risk_percent    = e.RiskPercent,
            stop_loss       = e.StopLossPrice,
            sl_distance     = e.SlDistance,
            position_size   = e.PositionSize
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Execution ────────────────────────────────────────────────────────────

    public void LogExecution(string asset, int cycleId, ExecutionEvent e)
    {
        var entry = new
        {
            event_type          = "execution",
            timestamp           = DateTime.UtcNow,
            asset,
            cycle_id            = cycleId,
            order_type          = e.OrderType.ToString(),
            execution_price     = e.ExecutionPrice,
            slippage            = e.Slippage
        };
        _inner.LogInformation("{LogEntry}", Serialize(entry));
    }

    // ── Exit ─────────────────────────────────────────────────────────────────

    public void LogExit(string asset, int cycleId, ExitEvent e)
    {
        var entry = new
        {
            event_type      = "exit",
            timestamp       = DateTime.UtcNow,
            asset,
            cycle_id        = cycleId,
            position_state  = e.State.ToString(),
            reason          = e.Reason,
            tp_hit          = e.TpHit,
            exit_price      = e.ExitPrice,
            pnl             = e.Pnl
        };

        var level = e.State == DIS.Core.Enums.PositionState.Closed
            ? LogLevel.Information
            : LogLevel.Warning;

        _inner.Log(level, "{LogEntry}", Serialize(entry));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string Serialize(object obj) =>
        JsonSerializer.Serialize(obj, _jsonOptions);

    private static object Change<T>(T from, T to) =>
        new { from = from!.ToString(), to = to!.ToString() };
}
