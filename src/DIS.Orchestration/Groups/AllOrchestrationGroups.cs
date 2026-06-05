using DIS.Core.Enums;
using DIS.Core.Models;

namespace DIS.Orchestration.Groups;

/// <summary>
/// GROUP 1 — Portfolio Permission
/// Inputs: portfolio_drawdown_state, overtrading_state
/// Output: TradingPermission (portfolio-level gate)
/// </summary>
public sealed class Group1_PortfolioPermission
{
    public TradingPermission Evaluate(PortfolioDrawdownState drawdown, OvertradingState overtrading)
    {
        // TODO: BREACH on either → BLOCK dominates.
        //       WARNING on either → RESTRICT.
        //       Both NORMAL → ALLOW.
        throw new NotImplementedException();
    }
}

/// <summary>
/// GROUP 2 — Safety / Risk Control
/// Inputs: RiskEnvironment, ExecutionQuality, LiquidityEnvironment, VolatilityEnvironment
/// Output: TradingPermission (market-level gate, used as market_permission)
/// </summary>
public sealed class Group2_SafetyControl
{
    public TradingPermission Evaluate(
        RiskEnvironment     risk,
        ExecutionQuality    execution,
        LiquidityEnvironment liquidity,
        VolatilityEnvironment volatility)
    {
        // TODO: EXTREME/POOR/BROKEN/UNSTABLE → BLOCK.
        //       HIGH/MEDIUM/ACCEPTABLE/THIN/ELEVATED/EXTREME → RESTRICT (if not BLOCK).
        //       LOW + GOOD + HEALTHY + LOW/NORMAL → ALLOW.
        //       Else → RESTRICT (safety fallback).
        throw new NotImplementedException();
    }
}

/// <summary>
/// GROUP 3 — Market Type / Strategy Selection
/// Inputs: MarketRegime, MarketBehaviorType
/// Output: StrategyOutput
/// </summary>
public sealed class Group3_MarketType
{
    public StrategyOutput Evaluate(MarketRegime regime, MarketBehaviorType behavior)
    {
        // TODO: TREND/EMERGING_TREND/LATE_TREND + NORMAL/ABSORPTION → TREND_FOLLOWING.
        //       CHOPPY_RANGE + NORMAL/ABSORPTION → RANGE_TRADING.
        //       Any (except STRESS_DISORDER) + EXPANSION → BREAKOUT.
        //       Any (except STRESS_DISORDER) + REVERSAL → REVERSAL.
        //       TRANSITIONAL + NORMAL/ABSORPTION/MANIPULATION → NO_TRADE.
        //       STRESS_DISORDER → NO_TRADE. MANIPULATION → NO_TRADE.
        throw new NotImplementedException();
    }
}

/// <summary>
/// GROUP 4 — Direction
/// Inputs: DirectionalBias, TimeframeConsistency
/// Output: TradeDirection
/// </summary>
public sealed class Group4_Direction
{
    public TradeDirection Evaluate(DirectionalBias bias, TimeframeConsistency consistency)
    {
        // TODO: LONG + ALIGNED → LONG. SHORT + ALIGNED → SHORT.
        //       NEUTRAL + ALIGNED → NO_TRADE. Any + CONFLICTED → NO_TRADE.
        throw new NotImplementedException();
    }
}

/// <summary>
/// GROUP 5 — Quality / Confidence
/// Inputs: MarketStrength, CrowdCondition, StructuralCondition
/// Output: ConfidenceLevel
/// </summary>
public sealed class Group5_Confidence
{
    public ConfidenceLevel Evaluate(
        MarketStrength     strength,
        CrowdCondition     crowd,
        StructuralCondition structure)
    {
        // TODO: STRONG + CLEAN + BALANCED → HIGH.
        //       STRONG + (CLEAN|VALID) + ONE_SIDED → MEDIUM.
        //       MODERATE + CLEAN + BALANCED → MEDIUM.
        //       MODERATE + (VALID|WEAK) → LOW. STRONG + WEAK → LOW. WEAK → LOW.
        //       BROKEN → NO_TRADE. EXTREME crowd → NO_TRADE. Else → NO_TRADE.
        throw new NotImplementedException();
    }
}
