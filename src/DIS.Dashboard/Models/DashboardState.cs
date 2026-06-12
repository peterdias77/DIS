namespace DIS.Dashboard.Models;

/// <summary>
/// Single mutable snapshot of all dashboard display values.
/// Owned by IDashboardStateService. Only DashboardStateUpdater writes to it
/// via IDashboardStateService.Update(). All Blazor components read it as a
/// parameter — they never write directly.
/// </summary>
public sealed class DashboardState
{
    // ── Current System State ───────────────────────────────────────────────
    public string StructureCondition  { get; set; } = "—";
    public int    CycleId             { get; set; } = 1;
    public string Regime              { get; set; } = "—";
    public string Volatility          { get; set; } = "—";
    public string Strength            { get; set; } = "—";
    public string LastLogTime         { get; set; } = "—";
    public int    LogBarsFilled       { get; set; } = 0;

    public List<int> RegimeSparkline   { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    public List<int> VolSparkline      { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    public List<int> StrengthSparkline { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    // ── Output Diagnostics ─────────────────────────────────────────────────
    public string ConflictState       { get; set; } = "—";
    public string DirectionBias       { get; set; } = "—";
    public string RiskEnvLeft         { get; set; } = "—";
    public string RiskEnvRight        { get; set; } = "—";
    public string ExecutionQuality    { get; set; } = "—";
    public string ExecutionQualityNum { get; set; } = "—";
    public string BehaviorType        { get; set; } = "—";
    public string CrowdCondition      { get; set; } = "—";
    public string TfAlignment         { get; set; } = "—";
    public string ReRankCountdown     { get; set; } = "—";

    public List<RankStripSlot> RankStrip { get; set; } = new()
    {
        new(1, "1", false), new(2, "2", false), new(3, "3", false),
        new(4, "4", false), new(5, "5", false)
    };

    // ── Portfolio Overview ─────────────────────────────────────────────────
    public int     CycleRank          { get; set; } = 1;
    public int     CycleMax           { get; set; } = 5;
    public string  CycleAsset         { get; set; } = "—";
    public int     AssetsInCycle      { get; set; } = 0;
    public string  TradingPermission  { get; set; } = "—";
    public decimal OpenPnl            { get; set; } = 0m;
    public decimal TotalPnl           { get; set; } = 0m;
    public string  SystemState        { get; set; } = "—";

    // ── Active Trades ──────────────────────────────────────────────────────
    public List<ActiveTrade> ActiveTrades { get; set; } = new();

    // ── Rank Diagnostics ───────────────────────────────────────────────────
    public List<RankSlot> RankSlots { get; set; } = new()
    {
        new(1, false, false), new(2, false, false), new(3, false, false),
        new(4, false, false), new(5, false, false)
    };

    public double DonutFill { get; set; } = 0;
    public double DonutGap  { get; set; } = 201;   // circumference ≈ 201 at r=32

    // ── Logging Summary ────────────────────────────────────────────────────
    public List<LogEntry> LogEntries { get; set; } = new();

    // ── Entry & Exit Signals ───────────────────────────────────────────────
    public List<SignalRow> StateSignals  { get; set; } = new();
    public List<SignalRow> EntrySignals  { get; set; } = new();
    public List<string>    RiskBadges   { get; set; } = new();
    public string          EntryRisk    { get; set; } = "—";

    // ── Rank Stats ─────────────────────────────────────────────────────────
    public int TotalLogs      { get; set; } = 0;
    public int StateChanges   { get; set; } = 0;
    public int OutputChanges  { get; set; } = 0;
    public int DecisionLogs   { get; set; } = 0;
    public int ActionEvents   { get; set; } = 0;

    // ── Logging Footer Summary ─────────────────────────────────────────────
    public string  PositionSizeLabel { get; set; } = "—";
    public decimal EnteredSize       { get; set; } = 0m;
    public string  ActiveCycleLabel  { get; set; } = "—";
    public string  LastLogged        { get; set; } = "—";
    public string  StopLossDisplay   { get; set; } = "—";
    public string  LastError         { get; set; } = "NONE";
}

// ── Value objects ──────────────────────────────────────────────────────────

public sealed record RankStripSlot(int Number, string Sub, bool Active);

public sealed record ActiveTrade(
    string  Asset,
    string  Direction,
    string  Position,
    decimal EntryPrice,
    decimal Pnl,
    decimal StopLoss);

public sealed record RankSlot(int Rank, bool Occupied, bool Highlighted);

public sealed record LogEntry(string Time, string Type, string Detail);

public sealed record SignalRow(string Asset, string ChartLine1, string ChartLine2);
