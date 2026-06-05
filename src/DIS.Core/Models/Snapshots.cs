using DIS.Core.Enums;

namespace DIS.Core.Models;

/// <summary>
/// Immutable snapshot of all 34 computed states for a single asset at a point in time.
/// Produced by the StateEngine and consumed by the OutputLayer.
/// </summary>
public sealed record StateSnapshot
{
    public required string      Asset           { get; init; }
    public required DateTime    Timestamp       { get; init; }

    // Layer 1 — Portfolio
    public PortfolioDrawdownState   DrawdownState       { get; init; }
    public OvertradingState         OvertradingState    { get; init; }

    // Layer 2 — Correlation
    public CorrelationRegimeState   CorrelationRegime   { get; init; }
    public CorrelationIntegrityState CorrelationIntegrity { get; init; }

    // Layer 3 — Structure
    public MarketStructureState     MarketStructure     { get; init; }
    public StructureQualityState    StructureQuality    { get; init; }
    public StructurePhaseState      StructurePhase      { get; init; }
    public BreakRetestState         BreakRetest         { get; init; }
    public BreakoutStructureState   BreakoutStructure   { get; init; }
    public StructureDriftState      StructureDrift      { get; init; }
    public RangeMigrationState      RangeMigration      { get; init; }
    public HtfStructureState        HtfStructure        { get; init; }

    // Layer 4 — Volatility
    public VolatilityRegimeState    VolatilityRegime    { get; init; }
    public VolatilityTrendState     VolatilityTrend     { get; init; }
    public VolatilityReactionState  VolatilityReaction  { get; init; }
    public VolatilityTfAlignState   VolatilityTfAlign   { get; init; }
    public VolatilityAsymmetryState VolatilityAsymmetry { get; init; }

    // Layer 5 — Liquidity / Execution
    public LiquidityConditionState  LiquidityCondition  { get; init; }
    public SpreadConditionState     SpreadCondition     { get; init; }
    public SlippageState            Slippage            { get; init; }

    // Layer 6 — Participation
    public ParticipationTrendState  ParticipationTrend  { get; init; }
    public PositioningState         Positioning         { get; init; }

    // Layer 7 — Momentum / Volume
    public MomentumState            Momentum            { get; init; }
    public MomentumFollowThrough    MomentumFollowThrough { get; init; }
    public VolumeBehavior           Volume              { get; init; }
    public WickBehaviorState        WickBehavior        { get; init; }

    // Layer 8 — Session / Event
    public SessionState             Session             { get; init; }
    public NewsProximityState       NewsProximity       { get; init; }
    public PriceVsCrowdState        PriceVsCrowd        { get; init; }
    public EventImportanceState     EventImportance     { get; init; }
    public EventFollowThroughState  EventFollowThrough  { get; init; }

    // Layer 9 — Crowd / Macro / Microstructure / Trend
    public IntermarketConfirmState  IntermarketConfirm  { get; init; }
    public LiquiditySweepState      LiquiditySweep      { get; init; }
    public TrendPhaseState          TrendPhase          { get; init; }
}

/// <summary>
/// Immutable snapshot of all 13 output evaluator results for a single asset.
/// Produced by the OutputLayer and consumed by the Orchestration layer.
/// </summary>
public sealed record OutputSnapshot
{
    public required string      Asset           { get; init; }
    public required DateTime    Timestamp       { get; init; }

    public TradingPermission    TradingPermission   { get; init; }   // Output 1
    public ExposureControl      ExposureControl     { get; init; }   // Output 2
    public MarketRegime         MarketRegime        { get; init; }   // Output 3
    public StructuralCondition  StructuralCondition { get; init; }   // Output 4
    public DirectionalBias      DirectionalBias     { get; init; }   // Output 5
    public MarketStrength       MarketStrength      { get; init; }   // Output 6
    public VolatilityEnvironment VolatilityEnv      { get; init; }   // Output 7
    public LiquidityEnvironment LiquidityEnv        { get; init; }   // Output 8
    public ExecutionQuality     ExecutionQuality    { get; init; }   // Output 9
    public RiskEnvironment      RiskEnvironment     { get; init; }   // Output 10
    public MarketBehaviorType   MarketBehavior      { get; init; }   // Output 11
    public CrowdCondition       CrowdCondition      { get; init; }   // Output 12
    public TimeframeConsistency TimeframeConsistency { get; init; }  // Output 13
}

/// <summary>
/// Orchestration decision packet produced by the 5 orchestration groups.
/// Consumed by the EntryEngine and RiskManager.
/// </summary>
public sealed record OrchestrationDecision
{
    public required string      Asset               { get; init; }
    public required DateTime    Timestamp           { get; init; }
    public required int         CycleId             { get; init; }

    public TradingPermission    PortfolioPermission { get; init; }   // Group 1
    public TradingPermission    MarketPermission    { get; init; }   // Group 2
    public StrategyOutput       Strategy            { get; init; }   // Group 3
    public TradeDirection       Direction           { get; init; }   // Group 4
    public ConfidenceLevel      Confidence          { get; init; }   // Group 5
}
