namespace DIS.Core.Models;

/// <summary>
/// Live ingestion status for one symbol.
/// Produced by BarRouterService, published via SignalR to the Dashboard.
/// </summary>
public sealed record SymbolFeedStatus
{
    public required string   Symbol        { get; init; }
    public required bool     EaConnected   { get; init; }   // TCP connection to EA is up
    public required bool     HistoryLoaded { get; init; }   // HIST_START/HIST_END blocks received
    public required long     BarCount      { get; init; }   // Total bars stored in DB for this symbol
    public required DateTime? LastBarTime  { get; init; }   // UTC timestamp of most recent bar
    public required int      BarsThisSession { get; init; } // Bars received since last EA connect
    public required string   Status        { get; init; }   // "OK" | "STALE" | "NO_DATA" | "DISCONNECTED"
}

/// <summary>
/// Snapshot of the entire feed health.
/// Published every 5 seconds via SignalR to keep the dashboard current.
/// </summary>
public sealed record FeedHealthSnapshot
{
    public required DateTime             Timestamp      { get; init; }
    public required bool                 EaConnected    { get; init; }
    public required int                  SymbolsTotal   { get; init; }
    public required int                  SymbolsOk      { get; init; }
    public required int                  SymbolsStale   { get; init; }
    public required int                  SymbolsNoData  { get; init; }
    public required long                 TotalBarsStored { get; init; }
    public required IReadOnlyList<SymbolFeedStatus> Symbols { get; init; }
}
