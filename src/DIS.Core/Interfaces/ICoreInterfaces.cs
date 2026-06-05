using DIS.Core.Models;

namespace DIS.Core.Interfaces;

// ─────────────────────────────────────────────────────────────────────────────
// DATA FEED
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Provides real-time market data. Broker-agnostic.
/// Implementations live in DIS.Execution (e.g. Mt5DataFeedAdapter).
/// </summary>
public interface IMarketDataProvider
{
    IAsyncEnumerable<OhlcBar>    StreamOhlcAsync(string symbol, string timeframe, CancellationToken ct);
    IAsyncEnumerable<TickData>   StreamTicksAsync(string symbol, CancellationToken ct);
    IAsyncEnumerable<OrderBook>  StreamOrderBookAsync(string symbol, CancellationToken ct);
    Task<IReadOnlyList<OhlcBar>> GetHistoryAsync(string symbol, string timeframe, int bars, CancellationToken ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// STATE ENGINE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Contract for all 34 state calculators.
/// Each calculator is responsible for exactly one state value.
/// </summary>
public interface IStateCalculator<TState> where TState : Enum
{
    /// <summary>Unique state identifier matching the DIS registry (1–34).</summary>
    int StateId { get; }

    /// <summary>Human-readable name matching the DIS registry (e.g. "portfolio_drawdown_state").</summary>
    string StateName { get; }

    /// <summary>
    /// Evaluates the current state from the provided market context.
    /// Must be deterministic. Must not produce side effects.
    /// </summary>
    TState Calculate(MarketContext context);
}

// ─────────────────────────────────────────────────────────────────────────────
// OUTPUT LAYER
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Contract for all 13 output evaluators.
/// Each evaluator applies the output rule book to a defined set of state values.
/// </summary>
public interface IOutputEvaluator<TOutput> where TOutput : Enum
{
    int    OutputId   { get; }
    string OutputName { get; }

    TOutput Evaluate(StateSnapshot states);
}

// ─────────────────────────────────────────────────────────────────────────────
// ORCHESTRATION
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Contract for the 5 orchestration groups.
/// Each group consumes output values and contributes to the OrchestrationDecision.
/// </summary>
public interface IOrchestrationGroup
{
    int    GroupId   { get; }
    string GroupName { get; }
}

// ─────────────────────────────────────────────────────────────────────────────
// EXECUTION
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Broker execution gateway. Submits orders and receives fill confirmations.
/// MT5 is one implementation; other brokers can be plugged in here.
/// </summary>
public interface IExecutionAdapter
{
    Task<OrderResult> SubmitOrderAsync(OrderRequest request, CancellationToken ct);
    Task<bool>        ClosePositionAsync(string positionId, CancellationToken ct);
    Task<bool>        ModifyPositionAsync(string positionId, decimal newSl, decimal newTp, CancellationToken ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// LOGGER
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Event-driven structured logger. All layers write through this interface.
/// Emits JSON log events only when values change.
/// </summary>
public interface IDISLogger
{
    void LogStateChange<TState>(string asset, int cycleId, string stateName, TState previous, TState current) where TState : Enum;
    void LogOutputChange<TOutput>(string asset, int cycleId, string outputName, TOutput previous, TOutput current) where TOutput : Enum;
    void LogOrchestrationChange(string asset, int cycleId, OrchestrationDecision previous, OrchestrationDecision current);
    void LogEntry(string asset, int cycleId, EntryEvent entry);
    void LogTradeControl(string asset, int cycleId, TradeControlEvent control);
    void LogRisk(string asset, int cycleId, RiskEvent risk);
    void LogExecution(string asset, int cycleId, ExecutionEvent execution);
    void LogExit(string asset, int cycleId, ExitEvent exit);
}
