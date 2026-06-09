namespace DIS.Dashboard.Models;

public sealed class DashboardState
{
    // ── Current System State ───────────────────────────────────────────
    public string StructureCondition  { get; set; } = "VALID";
    public int    CycleId             { get; set; } = 2;
    public string Regime              { get; set; } = "TRENDING UP";
    public string Volatility          { get; set; } = "NORMAL";
    public string Strength            { get; set; } = "STRONG";
    public string LastLogTime         { get; set; } = "14:25";
    public int    LogBarsFilled       { get; set; } = 6;

    public IReadOnlyList<int> RegimeSparkline   { get; set; } = new[] { 30, 45, 60, 50, 70, 80, 65, 90, 75, 85 };
    public IReadOnlyList<int> VolSparkline      { get; set; } = new[] { 50, 55, 45, 60, 50, 55, 50, 45, 55, 50 };
    public IReadOnlyList<int> StrengthSparkline { get; set; } = new[] { 40, 50, 60, 55, 70, 65, 75, 80, 70, 85 };

    // ── Output Diagnostics ─────────────────────────────────────────────
    public string ConflictState     { get; set; } = "LOW";
    public string DirectionBias     { get; set; } = "LONG";
    public string RiskEnvLeft       { get; set; } = "MEDIUM";
    public string RiskEnvRight      { get; set; } = "MEDIUM";
    public string ExecutionQuality  { get; set; } = "GOOD";
    public string ExecutionQualityNum { get; set; } = "2";
    public string BehaviorType      { get; set; } = "NORMAL";
    public string CrowdCondition    { get; set; } = "BALANCED";
    public string TfAlignment       { get; set; } = "ALIGNED";
    public string ReRankCountdown   { get; set; } = "56s";

    public IReadOnlyList<RankStripSlot> RankStrip { get; set; } = new List<RankStripSlot>
    {
        new(1, "1", true), new(2, "2", false), new(3, "3", true),
        new(4, "4", false), new(5, "5", false)
    };

    // ── Portfolio Overview ─────────────────────────────────────────────
    public int    CycleRank         { get; set; } = 3;
    public int    CycleMax          { get; set; } = 5;
    public string CycleAsset        { get; set; } = "GBPJPY";
    public int    AssetsInCycle     { get; set; } = 3;
    public string TradingPermission { get; set; } = "ALLOW";
    public decimal OpenPnl          { get; set; } = 2540.75m;
    public decimal TotalPnl         { get; set; } = 5740.25m;
    public string SystemState       { get; set; } = "TRENDING";

    // ── Active Trades ─────────────────────────────────────────────────
    public IReadOnlyList<ActiveTrade> ActiveTrades { get; set; } = new List<ActiveTrade>
    {
        new("XAUUSD", "LONG",  "0.5 LOTS", 1950.25m, 820.50m,  1950.00m),
        new("BTCUSD", "SHORT", "0.3 LOTS", 43000m,   -450.25m, 42000m),
        new("GBPJPY", "SHORT", "0.2 LOTS", 154.75m,  770.00m,  182.50m),
    };

    // ── Rank Diagnostics ──────────────────────────────────────────────
    public IReadOnlyList<RankSlot> RankSlots { get; set; } = new List<RankSlot>
    {
        new(1, true,  false),
        new(2, false, false),
        new(3, true,  true),
        new(4, false, false),
        new(5, false, false),
        new(5, true,  false),
    };

    public double DonutFill { get; set; } = 150;
    public double DonutGap  { get; set; } = 51;

    // ── Logging Summary ────────────────────────────────────────────────
    public IReadOnlyList<LogEntry> LogEntries { get; set; } = new List<LogEntry>
    {
        new("14:25", "STATE CHANGE:", "ACTIVECYCLE: 3"),
        new("14:25", "OUTPUT CHANGE:", "STRATEGY"),
        new("14:20", "ENTRY SIGNAL",  "BUY GBPJPY @ 15K.75"),
        new("14:20", "EXECUTION",     "SELL BTCUSD: 0.5"),
        new("14:10", "EXIT",          "BTCUSD @ 43,000 (−$450.25)"),
    };

    // ── Entry & Exit Signals ──────────────────────────────────────────
    public IReadOnlyList<SignalRow> StateSignals { get; set; } = new List<SignalRow>
    {
        new("XAUUSD", "DO-1T NORM-LINE", ""),
        new("BTCUSD", "DIAGN STRAT", "TERSEA RATING"),
    };

    public IReadOnlyList<SignalRow> EntrySignals { get; set; } = new List<SignalRow>
    {
        new("BRKSUI", "3DANC TIME", "FRNS 3 COM"),
        new("EUBUSD", "SNAGE THE KAMROV", "CHOPPY SGNSR"),
    };

    public IReadOnlyList<string> RiskBadges { get; set; } = new[] { "LOD", "SVCLES" };
    public string EntryRisk { get; set; } = "MLAUBN";

    // ── Logging Stats ─────────────────────────────────────────────────
    public int    TotalLogs         { get; set; } = 125;
    public int    StateChanges      { get; set; } = 24;
    public int    OutputChanges     { get; set; } = 16;
    public int    DecisionLogs      { get; set; } = 42;
    public int    ActionEvents      { get; set; } = 43;

    // ── Bottom Logging Summary ────────────────────────────────────────
    public string  PositionSizeLabel { get; set; } = "FULL";
    public decimal EnteredSize       { get; set; } = 1.0m;
    public string  ActiveCycleLabel  { get; set; } = "3 SRATEGY";
    public string  LastLogged        { get; set; } = "STRATEGY";
    public string  StopLossDisplay   { get; set; } = "132.00 GBPRY";
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
