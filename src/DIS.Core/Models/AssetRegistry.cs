using DIS.Core.Enums;

namespace DIS.Core.Models;

/// <summary>
/// Describes a single tradeable asset registered in the DIS asset map.
/// </summary>
public sealed record AssetDefinition
{
    public required string        Symbol          { get; init; }
    public required AssetClass    Class           { get; init; }
    public required StrategyGroup Group           { get; init; }
    public required AssetRank     Rank            { get; init; }
    public required string        DisplayName     { get; init; }
}

/// <summary>
/// Static registry of all 20 DIS assets across 4 strategy groups.
/// Source of truth for asset mapping, rank allocation, and group membership.
/// </summary>
public static class AssetRegistry
{
    public static readonly IReadOnlyList<AssetDefinition> All = new List<AssetDefinition>
    {
        // ── TREND FOLLOWING (5) ──────────────────────────────────
        new() { Symbol = "XAUUSD",  Class = AssetClass.Gold,      Group = StrategyGroup.TrendFollowing, Rank = AssetRank.Rank1, DisplayName = "Gold" },
        new() { Symbol = "BTCUSD",  Class = AssetClass.Crypto,    Group = StrategyGroup.TrendFollowing, Rank = AssetRank.Rank2, DisplayName = "Bitcoin" },
        new() { Symbol = "NAS100",  Class = AssetClass.Index,     Group = StrategyGroup.TrendFollowing, Rank = AssetRank.Rank3, DisplayName = "NASDAQ 100" },
        new() { Symbol = "SPX500",  Class = AssetClass.Index,     Group = StrategyGroup.TrendFollowing, Rank = AssetRank.Rank4, DisplayName = "S&P 500" },
        new() { Symbol = "USDJPY",  Class = AssetClass.Forex,     Group = StrategyGroup.TrendFollowing, Rank = AssetRank.Rank5, DisplayName = "USD/JPY" },

        // ── BREAKOUT (5) ─────────────────────────────────────────
        new() { Symbol = "ETHUSD",  Class = AssetClass.Crypto,    Group = StrategyGroup.Breakout,       Rank = AssetRank.Rank1, DisplayName = "Ethereum" },
        new() { Symbol = "XTIUSD",  Class = AssetClass.Oil,       Group = StrategyGroup.Breakout,       Rank = AssetRank.Rank2, DisplayName = "Crude Oil (WTI)" },
        new() { Symbol = "GBPJPY",  Class = AssetClass.Forex,     Group = StrategyGroup.Breakout,       Rank = AssetRank.Rank3, DisplayName = "GBP/JPY" },
        new() { Symbol = "SOLUSD",  Class = AssetClass.Crypto,    Group = StrategyGroup.Breakout,       Rank = AssetRank.Rank4, DisplayName = "Solana" },
        new() { Symbol = "DE40",    Class = AssetClass.Index,     Group = StrategyGroup.Breakout,       Rank = AssetRank.Rank5, DisplayName = "DAX" },

        // ── RANGE TRADING (5) ────────────────────────────────────
        new() { Symbol = "EURUSD",  Class = AssetClass.Forex,     Group = StrategyGroup.RangeTrading,   Rank = AssetRank.Rank1, DisplayName = "EUR/USD" },
        new() { Symbol = "GBPUSD",  Class = AssetClass.Forex,     Group = StrategyGroup.RangeTrading,   Rank = AssetRank.Rank2, DisplayName = "GBP/USD" },
        new() { Symbol = "USDCHF",  Class = AssetClass.Forex,     Group = StrategyGroup.RangeTrading,   Rank = AssetRank.Rank3, DisplayName = "USD/CHF" },
        new() { Symbol = "XAGUSD",  Class = AssetClass.Silver,    Group = StrategyGroup.RangeTrading,   Rank = AssetRank.Rank4, DisplayName = "Silver" },
        new() { Symbol = "UK100",   Class = AssetClass.Index,     Group = StrategyGroup.RangeTrading,   Rank = AssetRank.Rank5, DisplayName = "FTSE 100" },

        // ── REVERSAL (5) ─────────────────────────────────────────
        new() { Symbol = "JP225",   Class = AssetClass.Index,     Group = StrategyGroup.Reversal,       Rank = AssetRank.Rank1, DisplayName = "Nikkei 225" },
        new() { Symbol = "NATGAS",  Class = AssetClass.Commodity, Group = StrategyGroup.Reversal,       Rank = AssetRank.Rank2, DisplayName = "Natural Gas" },
        new() { Symbol = "COPPER",  Class = AssetClass.Commodity, Group = StrategyGroup.Reversal,       Rank = AssetRank.Rank3, DisplayName = "Copper" },
        new() { Symbol = "BNBUSD",  Class = AssetClass.Crypto,    Group = StrategyGroup.Reversal,       Rank = AssetRank.Rank4, DisplayName = "BNB" },
        new() { Symbol = "XRPUSD",  Class = AssetClass.Crypto,    Group = StrategyGroup.Reversal,       Rank = AssetRank.Rank5, DisplayName = "XRP" },
    };

    public static AssetDefinition? Find(string symbol) =>
        All.FirstOrDefault(a => a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
}
