namespace DIS.Core.Models;

/// <summary>
/// Canonical log event record. Written to SQLite by DIS.Logger,
/// broadcast via SignalR by DIS.Engine, and consumed by DIS.Dashboard.
/// Every loggable event in the system maps to one of these.
/// </summary>
public sealed record DISLogEntry
{
    public required long     Id          { get; init; }   // SQLite rowid, assigned on insert
    public required string   EventType   { get; init; }   // "state_change" | "output_change" | "orchestration_change" | "entry" | "trade_control" | "risk" | "execution" | "exit"
    public required DateTime Timestamp   { get; init; }
    public required string   Asset       { get; init; }
    public required int      CycleId     { get; init; }
    public required string   Payload     { get; init; }   // Full JSON blob of the event
    public required string   LogLevel    { get; init; }   // "INFO" | "WARNING" | "ERROR"
}
