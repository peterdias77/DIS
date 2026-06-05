namespace DIS.Core.Enums;

// ─────────────────────────────────────────────
// LAYER 1 — PORTFOLIO / RISK CONTROL
// ─────────────────────────────────────────────

public enum PortfolioDrawdownState    { Normal, Warning, Breach }
public enum OvertradingState          { Normal, Warning, Breach }

// ─────────────────────────────────────────────
// LAYER 2 — MARKET / CORRELATION
// ─────────────────────────────────────────────

public enum CorrelationRegimeState    { Normal, Elevated, Clustered, Collapse }
public enum CorrelationIntegrityState { Intact, Weakening, Broken }

// ─────────────────────────────────────────────
// LAYER 3 — MARKET / STRUCTURE
// ─────────────────────────────────────────────

public enum MarketStructureState      { Valid, Invalid }
public enum StructureQualityState     { Clean, Mixed, Broken }
public enum StructurePhaseState       { Emerging, Established, Extended, Exhausted, RangeBound }
public enum BreakRetestState          { Present, Absent }
public enum BreakoutStructureState    { Strong, Weak, Failed }
public enum StructureDriftState       { Stable, Drifting, Decoupled }
public enum RangeMigrationState       { StableRange, MigratingRange, BrokenRange }
public enum HtfStructureState         { Aligned, Conflicted }

// ─────────────────────────────────────────────
// LAYER 4 — MARKET / VOLATILITY
// ─────────────────────────────────────────────

public enum VolatilityRegimeState     { Compressed, Normal, Elevated, Extreme }
public enum VolatilityTrendState      { Contracting, Accelerating, Unstable }
public enum VolatilityReactionState   { Stable, Amplified, Shock }
public enum VolatilityTfAlignState    { Aligned, Irregular }
public enum VolatilityAsymmetryState  { Symmetric, Asymmetric }

// ─────────────────────────────────────────────
// LAYER 5 — LIQUIDITY / EXECUTION
// ─────────────────────────────────────────────

public enum LiquidityConditionState   { Healthy, Thin, Broken }
public enum SpreadConditionState      { Normal, Wide, Broken }
public enum SlippageState             { Acceptable, Elevated, Severe }

// ─────────────────────────────────────────────
// LAYER 6 — PARTICIPATION
// ─────────────────────────────────────────────

public enum ParticipationTrendState   { Rising, Stable, Declining }
public enum PositioningState          { Balanced, OneSided, Extreme }

// ─────────────────────────────────────────────
// LAYER 7 — MOMENTUM / VOLUME
// ─────────────────────────────────────────────

public enum MomentumState             { Accelerating, Stable, Decaying }
public enum MomentumFollowThrough     { Confirmed, Weak, Failed }
public enum VolumeBehavior            { Confirming, NonConfirming, Spiking }
public enum WickBehaviorState         { Absorptive, StopHunt, Aggressive, Neutral }

// ─────────────────────────────────────────────
// LAYER 8 — SESSION CONTEXT / EVENT
// ─────────────────────────────────────────────

public enum SessionState              { Asia, London, Ny, OffHours }
public enum NewsProximityState        { Clear, NearEvent }
public enum EventImportanceState      { Low, Medium, High }
public enum EventFollowThroughState   { Confirmed, Rejected, None }

// ─────────────────────────────────────────────
// LAYER 9 — CROWD / MACRO / MICROSTRUCTURE / TREND
// ─────────────────────────────────────────────

public enum PriceVsCrowdState         { Aligned, Diverging, Opposing }
public enum IntermarketConfirmState   { Confirming, Mixed, Fractured }
public enum LiquiditySweepState       { None, LocalSweep, MajorSweep }
public enum TrendPhaseState           { Initiation, Development, Exhaustion }
