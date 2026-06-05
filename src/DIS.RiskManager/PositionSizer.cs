using DIS.Core.Enums;
using DIS.Core.Models;

namespace DIS.RiskManager;

/// <summary>
/// Determines entry size, maps to risk percent, calculates structure-based
/// stop loss with volatility-adjusted buffer, enforces minimum SL distance,
/// and derives the final position size.
/// </summary>
public sealed class PositionSizer
{
    /// <summary>Step 1 — Determine entry size from orchestration state.</summary>
    public EntrySize DetermineEntrySize(
        TradingPermission   portfolioPermission,
        TradingPermission   marketPermission,
        StrategyOutput      strategy,
        ConfidenceLevel     confidence,
        VolatilityEnvironment volatility)
    {
        // TODO: BLOCK or UNSTABLE → NO_TRADE.
        //       TREND + ALLOW + ALLOW + HIGH → FULL.
        //       TREND/BREAKOUT/REVERSAL + ALLOW/RESTRICT + ALLOW/RESTRICT + MEDIUM → HALF.
        //       RANGE + RESTRICT + RESTRICT → QUARTER.
        //       LOW confidence + permitted → QUARTER.
        //       Else → NO_TRADE.
        throw new NotImplementedException();
    }

    /// <summary>Step 2 — Map entry size to risk percentage.</summary>
    public decimal GetRiskPercent(EntrySize size) => size switch
    {
        EntrySize.Full    => 0.005m,   // 0.5%
        EntrySize.Half    => 0.003m,   // 0.3%
        EntrySize.Quarter => 0.002m,   // 0.2%
        _                 => 0m
    };

    /// <summary>Step 3 — Calculate stop loss price using structure + volatility-adjusted buffer.</summary>
    public decimal CalculateStopLoss(
        TradeDirection      direction,
        decimal             lastSwingHigh,
        decimal             lastSwingLow,
        AssetClass          assetClass,
        VolatilityEnvironment volatility,
        decimal             entryPrice)
    {
        var baseBuffer    = GetBaseBuffer(assetClass, entryPrice);
        var adjBuffer     = AdjustBuffer(baseBuffer, volatility);
        var rawSl         = direction == TradeDirection.Long
                              ? lastSwingLow  - adjBuffer
                              : lastSwingHigh + adjBuffer;

        // Enforce minimum SL distance floor
        var slDistance    = Math.Abs(entryPrice - rawSl);
        var minSl         = GetMinimumSlDistance(assetClass, entryPrice);
        if (slDistance < minSl)
            slDistance    = minSl;

        return direction == TradeDirection.Long
            ? entryPrice - slDistance
            : entryPrice + slDistance;
    }

    /// <summary>Step 4 — Final position size formula: (capital × risk%) / SL distance.</summary>
    public decimal CalculatePositionSize(decimal capital, EntrySize size, decimal entryPrice, decimal slPrice)
    {
        var risk       = GetRiskPercent(size);
        var slDistance = Math.Abs(entryPrice - slPrice);
        if (slDistance == 0 || risk == 0) return 0;
        return (capital * risk) / slDistance;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static decimal GetBaseBuffer(AssetClass assetClass, decimal price) => assetClass switch
    {
        AssetClass.Forex     => 0.0005m,
        AssetClass.Crypto    => price * 0.001m,
        AssetClass.Index     => 10m,
        AssetClass.Gold      => 1.5m,
        AssetClass.Silver    => 0.15m,
        AssetClass.Oil       => 0.05m,
        AssetClass.Commodity => 0m,    // TODO: Finalise copper / gas at development
        _                    => 0m
    };

    private static decimal AdjustBuffer(decimal baseBuffer, VolatilityEnvironment vol) => vol switch
    {
        VolatilityEnvironment.Low      => baseBuffer * 1m,
        VolatilityEnvironment.Normal   => baseBuffer * 2m,
        VolatilityEnvironment.Elevated => baseBuffer * 3m,
        VolatilityEnvironment.Extreme  => baseBuffer * 4m,
        VolatilityEnvironment.Unstable => throw new InvalidOperationException(
            "UNSTABLE volatility → NO_TRADE. Entry size must be checked before SL calculation."),
        _ => baseBuffer
    };

    private static decimal GetMinimumSlDistance(AssetClass assetClass, decimal price) => assetClass switch
    {
        AssetClass.Forex     => 0.0010m,
        AssetClass.Gold      => 3.0m,
        AssetClass.Silver    => 0.30m,
        AssetClass.Crypto    => price * 0.002m,
        _                    => 0m
    };
}
