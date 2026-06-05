using DIS.Core.Enums;
using DIS.Core.Models;

namespace DIS.EntryEngine;

/// <summary>
/// Evaluates orchestration decisions against the global block gate
/// and produces a typed entry signal with a structure-based price level.
/// </summary>
public sealed class EntrySignalEngine
{
    /// <summary>
    /// Determines the entry signal type from orchestration decisions.
    /// Applies the global block gate first, then strategy-specific signal logic.
    /// </summary>
    public EntrySignal DetermineSignal(OrchestrationDecision decision)
    {
        // TODO: Global block gate (any BLOCK, NO_TRADE on strategy/direction/confidence → NO_ENTRY).
        //       TREND_FOLLOWING + LONG/SHORT + HIGH/MEDIUM → BUY/SELL.
        //       RANGE_TRADING + LONG/SHORT + MEDIUM/LOW → BUY_LIMIT/SELL_LIMIT.
        //       BREAKOUT + LONG/SHORT + HIGH/MEDIUM → BUY_STOP/SELL_STOP.
        //       REVERSAL + LONG/SHORT + HIGH → BUY/SELL.
        //       Else → NO_ENTRY.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Calculates the concrete entry price level from the last confirmed swing
    /// and the asset-class buffer, adjusted for signal type.
    /// </summary>
    public decimal CalculateEntryPrice(
        EntrySignal signal,
        decimal     lastSwingHigh,
        decimal     lastSwingLow,
        AssetClass  assetClass,
        decimal     currentPrice)
    {
        // TODO: Apply buffer table per asset class.
        //       BUY_STOP → lastSwingHigh + buffer.
        //       SELL_STOP → lastSwingLow - buffer.
        //       BUY_LIMIT → lastSwingLow + buffer.
        //       SELL_LIMIT → lastSwingHigh - buffer.
        //       BUY/SELL → market price (no offset).
        throw new NotImplementedException();
    }

    private static decimal GetBuffer(AssetClass assetClass, decimal currentPrice) =>
        assetClass switch
        {
            AssetClass.Forex     => 0.0005m,
            AssetClass.Crypto    => currentPrice * 0.001m,
            AssetClass.Index     => 10m,
            AssetClass.Gold      => 1.5m,
            AssetClass.Silver    => 0.15m,
            AssetClass.Oil       => 0.05m,
            AssetClass.Commodity => 0m,   // TODO: Finalise copper/gas buffers at development
            _                    => 0m
        };
}
