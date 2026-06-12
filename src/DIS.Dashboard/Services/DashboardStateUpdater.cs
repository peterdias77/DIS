using System.Text.Json;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Dashboard.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Dashboard.Services;

/// <summary>
/// The bridge between live data and the UI. No panel or page touches
/// the hub or the database directly — everything flows through here.
///
/// Two data sources:
///
///   1. STARTUP — PostgreSQL history (ILogReader)
///      Replays recent log entries oldest→newest so the dashboard is
///      fully populated before any live event arrives.
///
///   2. LIVE — SignalR hub events (DashboardHubClient)
///      OnLogEntry  → ApplyEntry() → IDashboardStateService.Update()
///      OnFeedHealth → ignored here (panels subscribe directly if needed)
///
/// Event → DashboardState mapping:
///   state_change          → log list, regime/volatility/strength/structure fields
///   output_change         → direction, risk env, execution quality, crowd,
///                           TF alignment, behavior type, market regime,
///                           trading permission
///   orchestration_change  → trading permission, active cycle label, direction bias,
///                           last logged (confidence), cycle id + rank strip
///   entry                 → entry signals list, active trades, log list
///   risk                  → position size label, stop loss display, active trade update
///   execution             → log list, action counter
///   exit                  → active trades, open PnL, log list
/// </summary>
public sealed class DashboardStateUpdater : BackgroundService
{
    private readonly DashboardHubClient             _hub;
    private readonly ILogReader                     _db;
    private readonly IDashboardStateService         _state;
    private readonly ILogger<DashboardStateUpdater> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // In-memory active trade ledger — rebuilt from entry/risk/exit events
    private readonly Dictionary<string, ActiveTrade> _activeTrades = new();

    public DashboardStateUpdater(
        DashboardHubClient             hub,
        ILogReader                     db,
        IDashboardStateService         state,
        ILogger<DashboardStateUpdater> log)
    {
        _hub   = hub;
        _db    = db;
        _state = state;
        _log   = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ── Step 1: populate from DB before subscribing to live stream ────────
        await HydrateFromHistoryAsync(stoppingToken);

        // ── Step 2: subscribe to live hub events ──────────────────────────────
        _hub.OnLogEntry += OnLiveLogEntry;
        _log.LogInformation("DashboardStateUpdater: live subscription active.");

        await Task.Delay(Timeout.Infinite, stoppingToken);

        _hub.OnLogEntry -= OnLiveLogEntry;
    }

    // ── History hydration ─────────────────────────────────────────────────────

    private async Task HydrateFromHistoryAsync(CancellationToken ct)
    {
        try
        {
            _log.LogInformation("DashboardStateUpdater: hydrating from DB...");

            var all = new List<DISLogEntry>();
            foreach (var type in new[]
            {
                "state_change", "output_change", "orchestration_change",
                "entry", "risk", "execution", "exit"
            })
            {
                var entries = await _db.GetByEventTypeAsync(type, 200, ct);
                all.AddRange(entries);
            }

            // Oldest → newest so the last value written wins
            foreach (var e in all.OrderBy(x => x.Id))
                ApplyEntry(e);

            _log.LogInformation(
                "DashboardStateUpdater: replayed {Count} entries from DB.", all.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "DashboardStateUpdater: DB hydration failed — dashboard starts empty.");
        }
    }

    // ── Live handler ──────────────────────────────────────────────────────────

    private void OnLiveLogEntry(DISLogEntry entry) => ApplyEntry(entry);

    // ── Dispatcher ────────────────────────────────────────────────────────────

    private void ApplyEntry(DISLogEntry entry)
    {
        try
        {
            switch (entry.EventType)
            {
                case "state_change":         ApplyStateChange(entry);         break;
                case "output_change":        ApplyOutputChange(entry);        break;
                case "orchestration_change": ApplyOrchestrationChange(entry); break;
                case "entry":                ApplyEntry_(entry);              break;
                case "risk":                 ApplyRisk(entry);                break;
                case "execution":            ApplyExecution(entry);           break;
                case "exit":                 ApplyExit(entry);                break;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex,
                "DashboardStateUpdater: failed to apply {Type} id={Id}", entry.EventType, entry.Id);
        }
    }

    // ── state_change ──────────────────────────────────────────────────────────

    private void ApplyStateChange(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var name      = Str(root, "state");
        var from      = Str(root, "from");
        var to        = Str(root, "to");

        _state.Update(s =>
        {
            switch (name)
            {
                case "structure_phase_state":
                    s.Regime = to;
                    PushSparkline(s.RegimeSparkline, PhaseToInt(to));
                    break;

                case "volatility_regime_state":
                    s.Volatility = to;
                    PushSparkline(s.VolSparkline, VolToInt(to));
                    break;

                case "momentum_state":
                    s.Strength = to;
                    PushSparkline(s.StrengthSparkline, MomToInt(to));
                    break;

                case "market_structure_state":
                case "structure_quality_state":
                    s.StructureCondition = to;
                    break;
            }

            PrependLog(s, entry.Timestamp, "STATE CHANGE:", $"{name}: {from} → {to}");
            s.LastLogTime  = entry.Timestamp.ToString("HH:mm");
            s.StateChanges++;
            s.TotalLogs++;
            RefreshLogBars(s);
        });
    }

    // ── output_change ─────────────────────────────────────────────────────────

    private void ApplyOutputChange(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var output    = Str(root, "output");
        var from      = Str(root, "from");
        var to        = Str(root, "to");

        _state.Update(s =>
        {
            switch (output)
            {
                case "structural_condition":
                    s.StructureCondition = to; break;

                case "directional_bias":
                    s.DirectionBias = to;
                    s.ConflictState = to == "NEUTRAL" ? "HIGH" : "LOW";
                    break;

                case "volatility_environment":
                    s.Volatility = to; break;

                case "market_strength":
                    s.Strength = to;
                    PushSparkline(s.StrengthSparkline, MomToInt(to));
                    break;

                case "risk_environment":
                    s.RiskEnvLeft  = to;
                    s.RiskEnvRight = to;
                    break;

                case "execution_quality":
                    s.ExecutionQuality    = to;
                    s.ExecutionQualityNum = to switch
                    {
                        "GOOD"       => "1",
                        "ACCEPTABLE" => "2",
                        "POOR"       => "3",
                        _            => "—"
                    };
                    break;

                case "crowd_condition":
                    s.CrowdCondition = to; break;

                case "timeframe_consistency":
                    s.TfAlignment = to; break;

                case "market_behavior_type":
                    s.BehaviorType = to; break;

                case "market_regime":
                    s.Regime      = to;
                    s.SystemState = to;
                    PushSparkline(s.RegimeSparkline, PhaseToInt(to));
                    break;

                case "trading_permission":
                    s.TradingPermission = to; break;
            }

            PrependLog(s, entry.Timestamp, "OUTPUT CHANGE:", $"{output}: {from} → {to}");
            s.LastLogTime   = entry.Timestamp.ToString("HH:mm");
            s.OutputChanges++;
            s.TotalLogs++;
            RefreshLogBars(s);
        });
    }

    // ── orchestration_change ──────────────────────────────────────────────────

    private void ApplyOrchestrationChange(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;

        _state.Update(s =>
        {
            if (TryGetChangeTo(root, "portfolio_permission", out var perm))
                s.TradingPermission = perm;

            if (TryGetChangeTo(root, "strategy", out var strat))
                s.ActiveCycleLabel = strat;

            if (TryGetChangeTo(root, "direction", out var dir))
                s.DirectionBias = dir;

            if (TryGetChangeTo(root, "confidence", out var conf))
                s.LastLogged = conf;

            // Cycle ID comes from the entry's CycleId field directly
            if (entry.CycleId > 0)
            {
                s.CycleId   = entry.CycleId;
                UpdateDonut(s);
            }

            s.DecisionLogs++;
            s.TotalLogs++;
            s.LastLogTime = entry.Timestamp.ToString("HH:mm");
            RefreshLogBars(s);
        });
    }

    // ── entry ─────────────────────────────────────────────────────────────────

    private void ApplyEntry_(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var signal    = Str(root, "signal");
        var price     = Dec(root, "price");
        var reason    = Str(root, "reason");
        bool isLong   = signal is "Buy" or "BuyStop" or "BuyLimit";

        _activeTrades[entry.Asset] = new ActiveTrade(
            Asset:      entry.Asset,
            Direction:  isLong ? "LONG" : "SHORT",
            Position:   "FULL",
            EntryPrice: price,
            Pnl:        0m,
            StopLoss:   0m);

        _state.Update(s =>
        {
            s.ActiveTrades = _activeTrades.Values.ToList();

            // Refresh rank strip — occupied slots = active trade count
            RefreshRankStrip(s);

            var signals = s.EntrySignals.ToList();
            signals.Insert(0, new SignalRow(entry.Asset, $"{signal} @ {price:N5}", reason));
            if (signals.Count > 4) signals.RemoveAt(signals.Count - 1);
            s.EntrySignals = signals;

            PrependLog(s, entry.Timestamp, "ENTRY SIGNAL",
                $"{signal} {entry.Asset} @ {price:N5}");

            s.CycleAsset    = entry.Asset;
            s.ActionEvents++;
            s.TotalLogs++;
            s.LastLogTime   = entry.Timestamp.ToString("HH:mm");
            RefreshLogBars(s);
        });
    }

    // ── risk ──────────────────────────────────────────────────────────────────

    private void ApplyRisk(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var sizeLabel = Str(root, "entry_size");
        var sl        = Dec(root, "stop_loss");
        var posSize   = Dec(root, "position_size");
        var riskPct   = Dec(root, "risk_percent");

        if (_activeTrades.TryGetValue(entry.Asset, out var existing))
        {
            _activeTrades[entry.Asset] = existing with
            {
                StopLoss = sl,
                Position = $"{posSize:N2} LOTS"
            };
        }

        _state.Update(s =>
        {
            s.PositionSizeLabel = sizeLabel;
            s.EnteredSize       = posSize;
            s.StopLossDisplay   = $"{sl:N5} {entry.Asset}";
            s.ActiveTrades      = _activeTrades.Values.ToList();
            s.TotalLogs++;
            s.LastLogTime       = entry.Timestamp.ToString("HH:mm");
            RefreshLogBars(s);
        });
    }

    // ── execution ─────────────────────────────────────────────────────────────

    private void ApplyExecution(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var execPrice = Dec(root, "execution_price");
        var slip      = Dec(root, "slippage");

        _state.Update(s =>
        {
            PrependLog(s, entry.Timestamp, "EXECUTION",
                $"{entry.Asset} fill @ {execPrice:N5} slip={slip:N1}pts");
            s.ActionEvents++;
            s.TotalLogs++;
            s.LastLogTime = entry.Timestamp.ToString("HH:mm");
            RefreshLogBars(s);
        });
    }

    // ── exit ──────────────────────────────────────────────────────────────────

    private void ApplyExit(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var posState  = Str(root, "position_state");
        var reason    = Str(root, "reason");
        var pnl       = root.TryGetProperty("pnl", out var pv) &&
                        pv.ValueKind == JsonValueKind.Number
                            ? pv.GetDecimal() : (decimal?)null;
        var exitPrice = root.TryGetProperty("exit_price", out var ev) &&
                        ev.ValueKind == JsonValueKind.Number
                            ? ev.GetDecimal() : (decimal?)null;

        if (posState == "Closed")
            _activeTrades.Remove(entry.Asset);
        else if (_activeTrades.TryGetValue(entry.Asset, out var t))
            _activeTrades[entry.Asset] = t with
            {
                Position = posState,
                Pnl      = pnl ?? t.Pnl
            };

        _state.Update(s =>
        {
            s.ActiveTrades = _activeTrades.Values.ToList();
            s.OpenPnl      = _activeTrades.Values.Sum(x => x.Pnl);
            RefreshRankStrip(s);

            PrependLog(s, entry.Timestamp, "EXIT",
                $"{entry.Asset} [{posState}]{(exitPrice.HasValue ? $" @ {exitPrice:N5}" : "")} {reason}");

            s.ActionEvents++;
            s.TotalLogs++;
            s.LastLogTime = entry.Timestamp.ToString("HH:mm");
            s.LastError   = "NONE";
            RefreshLogBars(s);
        });
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static string Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static decimal Dec(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal() : 0m;

    /// <summary>Reads {key: {from, to}} and returns the "to" string.</summary>
    private static bool TryGetChangeTo(JsonElement root, string key, out string to)
    {
        to = "";
        if (!root.TryGetProperty(key, out var obj)) return false;
        if (!obj.TryGetProperty("to", out var toEl)) return false;
        to = toEl.GetString() ?? "";
        return to.Length > 0;
    }

    /// <summary>Appends a new value to a 10-slot sparkline list, dropping the oldest entry.<///></summary>
    private static void PushSparkline(List<int> sparkline, int value)
    {
        sparkline.Add(value);
        if (sparkline.Count > 10)
            sparkline.RemoveAt(0);
    }

    private static void PrependLog(DashboardState s, DateTime ts, string type, string detail)
    {
        s.LogEntries.Insert(0, new LogEntry(ts.ToString("HH:mm"), type, detail));
        if (s.LogEntries.Count > 10)
            s.LogEntries.RemoveAt(s.LogEntries.Count - 1);
    }

    private static void RefreshLogBars(DashboardState s) =>
        s.LogBarsFilled = Math.Min(8, s.TotalLogs % 9);

    /// <summary>
    /// Updates the rank strip to reflect which slots are occupied by active trades.
    /// </summary>
    private static void RefreshRankStrip(DashboardState s)
    {
        int occupied = s.ActiveTrades.Count;
        s.AssetsInCycle = occupied;
        s.CycleRank     = occupied;

        for (int i = 0; i < s.RankStrip.Count; i++)
        {
            var r = s.RankStrip[i];
            s.RankStrip[i] = r with { Active = i < occupied };
        }

        for (int i = 0; i < s.RankSlots.Count; i++)
        {
            var r = s.RankSlots[i];
            s.RankSlots[i] = r with { Occupied = i < occupied };
        }

        // Donut reflects occupied / max(5) ratio
        UpdateDonut(s);
    }

    private static void UpdateDonut(DashboardState s)
    {
        // SVG circle circumference at r=32 ≈ 201
        const double circ = 201.0;
        double fill  = circ * s.AssetsInCycle / 5.0;
        s.DonutFill  = Math.Round(fill, 1);
        s.DonutGap   = Math.Round(circ - fill, 1);
    }

    // Sparkline value converters (0-100 scale for bar height)
    private static int PhaseToInt(string v) => v switch
    {
        "EMERGING"    or "EMERGING_TREND"  => 30,
        "ESTABLISHED" or "TREND"           => 60,
        "EXTENDED"    or "LATE_TREND"      => 80,
        "EXHAUSTED"   or "STRESS_DISORDER" => 20,
        "RANGE_BOUND" or "CHOPPY_RANGE"    => 40,
        "TRANSITIONAL"                     => 50,
        _                                  => 0
    };

    private static int VolToInt(string v) => v switch
    {
        "COMPRESSED" => 20,
        "NORMAL"     => 50,
        "ELEVATED"   => 70,
        "EXTREME"    => 90,
        "UNSTABLE"   => 60,
        _            => 0
    };

    private static int MomToInt(string v) => v switch
    {
        "ACCELERATING" or "STRONG"   => 85,
        "STABLE"       or "MODERATE" => 50,
        "DECAYING"     or "WEAK"     => 20,
        _                            => 0
    };
}
