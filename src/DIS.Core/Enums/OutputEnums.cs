namespace DIS.Core.Enums;

// ─────────────────────────────────────────────
// OUTPUT LAYER — 13 OUTPUTS
// ─────────────────────────────────────────────

// Output 1
public enum TradingPermission         { Allow, Restrict, Block }

// Output 2
public enum ExposureControl           { FullSize, ReducedSize, LimitedExposure }

// Output 3
public enum MarketRegime              { EmergingTrend, Trend, LateTrend, ChoppyRange, Transitional, StressDisorder }

// Output 4
public enum StructuralCondition       { Clean, Valid, Weak, Broken }

// Output 5
public enum DirectionalBias           { Long, Short, Neutral }

// Output 6
public enum MarketStrength            { Strong, Moderate, Weak }

// Output 7
public enum VolatilityEnvironment     { Low, Normal, Elevated, Extreme, Unstable }

// Output 8
public enum LiquidityEnvironment      { Healthy, Thin, Unstable, Broken }

// Output 9
public enum ExecutionQuality          { Good, Acceptable, Poor }

// Output 10
public enum RiskEnvironment           { Low, Medium, High, Extreme }

// Output 11
public enum MarketBehaviorType        { Normal, Absorption, Manipulation, Expansion, Reversal }

// Output 12
public enum CrowdCondition            { Balanced, OneSided, Extreme }

// Output 13
public enum TimeframeConsistency      { Aligned, Conflicted }

// ─────────────────────────────────────────────
// ORCHESTRATION — 5 GROUPS
// ─────────────────────────────────────────────

// Group 1 (portfolio) → TradingPermission (reused above)
// Group 2 (safety)    → TradingPermission (reused as MarketPermission)

// Group 3
public enum StrategyOutput            { TrendFollowing, RangeTrading, Breakout, Reversal, NoTrade }

// Group 4
public enum TradeDirection            { Long, Short, NoTrade }

// Group 5
public enum ConfidenceLevel           { High, Medium, Low, NoTrade }

// ─────────────────────────────────────────────
// ENTRY ENGINE
// ─────────────────────────────────────────────

public enum EntrySignal               { Buy, Sell, BuyStop, SellStop, BuyLimit, SellLimit, NoEntry }

// ─────────────────────────────────────────────
// RISK MANAGER
// ─────────────────────────────────────────────

public enum EntrySize                 { Full, Half, Quarter, NoTrade }

// ─────────────────────────────────────────────
// EXIT MANAGER
// ─────────────────────────────────────────────

public enum PositionState             { Full, Half, Quarter, Minimal, Closed }

// ─────────────────────────────────────────────
// ASSET CLASSIFICATION
// ─────────────────────────────────────────────

public enum AssetClass                { Forex, Crypto, Index, Gold, Silver, Oil, Commodity }
public enum StrategyGroup             { TrendFollowing, Breakout, RangeTrading, Reversal }
public enum AssetRank                 { Rank1 = 1, Rank2 = 2, Rank3 = 3, Rank4 = 4, Rank5 = 5 }

// ─────────────────────────────────────────────
// EXECUTION
// ─────────────────────────────────────────────

public enum OrderType                 { Market, Limit, Stop }
public enum OrderSide                 { Buy, Sell }
