using DIS.Core.Enums;

namespace DIS.Core.Models;

// ─────────────────────────────────────────────────────────────────────────────
// MARKET DATA MODELS
// ─────────────────────────────────────────────────────────────────────────────

public sealed record OhlcBar
{
    public required string   Symbol      { get; init; }
    public required string   Timeframe   { get; init; }
    public required DateTime Time        { get; init; }
    public required decimal  Open        { get; init; }
    public required decimal  High        { get; init; }
    public required decimal  Low         { get; init; }
    public required decimal  Close       { get; init; }
    public required long     Volume      { get; init; }
}

public sealed record TickData
{
    public required string   Symbol      { get; init; }
    public required DateTime Time        { get; init; }
    public required decimal  Bid         { get; init; }
    public required decimal  Ask         { get; init; }
    public required decimal  Last        { get; init; }
    public required long     Volume      { get; init; }
}

public sealed record OrderBook
{
    public required string              Symbol      { get; init; }
    public required DateTime            Time        { get; init; }
    public required IReadOnlyList<BookLevel> Bids   { get; init; }
    public required IReadOnlyList<BookLevel> Asks   { get; init; }
}

public sealed record BookLevel
{
    public required decimal Price       { get; init; }
    public required decimal Volume      { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// MARKET CONTEXT — INPUT TO STATE CALCULATORS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// All raw data available to state calculators for one evaluation cycle.
/// State calculators are stateless — they derive everything from this context.
/// </summary>
public sealed class MarketContext
{
    public required string                  Symbol          { get; init; }
    public required DateTime                Timestamp       { get; init; }
    public required IReadOnlyList<OhlcBar>  OhlcBars        { get; init; }
    public required IReadOnlyList<OhlcBar>  OhlcBarsHtf     { get; init; }
    public required IReadOnlyList<TickData> RecentTicks     { get; init; }
    public required OrderBook?              OrderBook       { get; init; }
    public required decimal                 CurrentEquity   { get; init; }
    public required decimal                 PeakEquity      { get; init; }
    public required IReadOnlyList<TradeRecord> TradeLog     { get; init; }
    public required IReadOnlyList<EconomicEvent> Calendar   { get; init; }
    public required IReadOnlyList<CorrelatedInstrument> CorrelatedInstruments { get; init; }
}

public sealed record TradeRecord
{
    public required DateTime    Time        { get; init; }
    public required string      Symbol      { get; init; }
    public required string      StateRef    { get; init; }
}

public sealed record EconomicEvent
{
    public required DateTime    EventTime   { get; init; }
    public required string      Name        { get; init; }
    public required string      Importance  { get; init; }  // Raw provider value, normalised in State 30
}

public sealed record CorrelatedInstrument
{
    public required string      Symbol          { get; init; }
    public required string      Relationship    { get; init; }  // "POSITIVE" | "NEGATIVE"
    public          decimal     Weight          { get; init; } = 1.0m;
    public required IReadOnlyList<OhlcBar> Bars { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// EXECUTION MODELS
// ─────────────────────────────────────────────────────────────────────────────

public sealed record OrderRequest
{
    public required string       Symbol      { get; init; }
    public required EntrySignal  Signal      { get; init; }
    public required decimal      EntryPrice  { get; init; }
    public required decimal      StopLoss    { get; init; }
    public required decimal      TakeProfit  { get; init; }
    public required decimal      Lots        { get; init; }
    public required int          CycleId     { get; init; }
}

public sealed record OrderResult
{
    public required bool     Success         { get; init; }
    public          string?  PositionId      { get; init; }
    public          decimal? ExecutionPrice  { get; init; }
    public          decimal  Slippage        { get; init; }
    public          string?  Error           { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// LOG EVENT MODELS
// ─────────────────────────────────────────────────────────────────────────────

public sealed record EntryEvent
{
    public required EntrySignal Signal          { get; init; }
    public required decimal     Price           { get; init; }
    public required string      Reason          { get; init; }
}

public sealed record TradeControlEvent
{
    public required int              CycleId         { get; init; }
    public required List<int>        ActiveRanks      { get; init; }
    public required int              NextRank         { get; init; }
    public required TradingPermission TradingPermission { get; init; }
    public required TradingPermission MarketPermission  { get; init; }
}

public sealed record RiskEvent
{
    public required EntrySize   Size            { get; init; }
    public required decimal     RiskPercent     { get; init; }
    public required decimal     StopLossPrice   { get; init; }
    public required decimal     SlDistance      { get; init; }
    public required decimal     PositionSize    { get; init; }
}

public sealed record ExecutionEvent
{
    public required OrderType   OrderType       { get; init; }
    public required decimal     ExecutionPrice  { get; init; }
    public required decimal     Slippage        { get; init; }
}

public sealed record ExitEvent
{
    public required PositionState   State           { get; init; }
    public required string          Reason          { get; init; }
    public required bool            TpHit           { get; init; }
    public          decimal?        ExitPrice       { get; init; }
    public          decimal?        Pnl             { get; init; }
}
