using DIS.Core.Enums;
using DIS.Core.Models;

namespace DIS.ExitManager;

/// <summary>
/// Calculates the fixed 1:4 risk-reward take profit level.
/// </summary>
public sealed class TakeProfitCalculator
{
    /// <summary>
    /// TP = Entry ± (4 × SL distance).
    /// The TP level is fixed at trade entry and never moves.
    /// </summary>
    public decimal Calculate(TradeDirection direction, decimal entryPrice, decimal stopLossPrice)
    {
        var slDistance = Math.Abs(entryPrice - stopLossPrice);
        return direction == TradeDirection.Long
            ? entryPrice + (4m * slDistance)
            : entryPrice - (4m * slDistance);
    }
}

/// <summary>
/// Manages the dynamic state-driven exit ladder for an active position.
/// Position steps down one level per evaluation cycle when conditions deteriorate.
/// Immediate closure occurs on strategy/direction invalidation or TP hit.
/// </summary>
public sealed class ExitLadderManager
{
    /// <summary>
    /// Evaluates current system state and determines the new position state.
    /// Call once per evaluation cycle for each active trade.
    /// </summary>
    public PositionState Evaluate(
        PositionState       currentState,
        StrategyOutput      strategy,
        TradeDirection      direction,
        ConfidenceLevel     confidence,
        TradingPermission   portfolioPermission,
        TradingPermission   marketPermission,
        decimal             currentPrice,
        decimal             takeProfitPrice,
        TradeDirection      tradeDirection)
    {
        // Rule 1 — TP hit: full immediate close
        if (IsTpHit(tradeDirection, currentPrice, takeProfitPrice))
            return PositionState.Closed;

        // Rule 2 — Strong negative shift: immediate close
        if (strategy == StrategyOutput.NoTrade || direction == TradeDirection.NoTrade)
            return PositionState.Closed;

        // Rule 3 — Weakening conditions: reduce one level
        if (confidence == ConfidenceLevel.Low || marketPermission == TradingPermission.Block)
            return ReduceOneLevel(currentState);

        // Rule 4 — Portfolio block: forced reduction one level per cycle
        if (portfolioPermission == TradingPermission.Block)
            return ReduceOneLevel(currentState);

        // No change
        return currentState;
    }

    private static bool IsTpHit(TradeDirection direction, decimal price, decimal tp) =>
        direction == TradeDirection.Long  ? price >= tp :
        direction == TradeDirection.Short ? price <= tp : false;

    /// <summary>
    /// Steps the position down one level in the exit ladder.
    /// FULL → HALF → QUARTER → MINIMAL → CLOSED
    /// </summary>
    public static PositionState ReduceOneLevel(PositionState current) => current switch
    {
        PositionState.Full    => PositionState.Half,
        PositionState.Half    => PositionState.Quarter,
        PositionState.Quarter => PositionState.Minimal,
        PositionState.Minimal => PositionState.Closed,
        _                     => PositionState.Closed
    };

    /// <summary>
    /// Maps an EntrySize to the initial PositionState at trade open.
    /// </summary>
    public static PositionState InitialState(EntrySize entrySize) => entrySize switch
    {
        EntrySize.Full    => PositionState.Full,
        EntrySize.Half    => PositionState.Half,
        EntrySize.Quarter => PositionState.Quarter,
        _                 => PositionState.Closed
    };
}
