using System.Text.Json;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Dashboard.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Dashboard.Services;

/// <summary>
/// The bridge between live data and the UI.
///
/// Two data sources feed DashboardState:
///
///   1. STARTUP — PostgreSQL history (via ILogReader)
///      Reads the most recent log entries on startup and replays them
///      so the dashboard is populated immediately, even before any live
///      events arrive from the engine.
///
///   2. LIVE — SignalR hub events (via DashboardHubClient)
///      Subscribes to OnLogEntry and OnFeedHealth. Every incoming event
///      is parsed and mapped to the correct DashboardState field via
///      IDashboardStateService.Update().
///
/// Event → State mapping:
///   state_change          → LogEntries list, per-state field updates (regime, volatility etc.)
///   output_change         → structural condition, direction bias, volatility env, risk env,
///                           execution quality, crowd condition, TF alignment, behavior type
///   orchestration_change  → trading permission, strategy, direction, confidence
///   entry                 → EntrySignals list, active trade seeding
///   risk                  → position size label, stop loss display
///   execution             → (logged to LogEntries)
///   exit                  → active trade removal / position state update, PnL
///   feed_health           → (not shown in DashboardState panels — handled separately)
/// </summary>
public sealed class DashboardStateUpdater : BackgroundService
{
    private readonly DashboardHubClient          _hub;
    private readonly ILogReader                  _db;
    private readonly IDashboardStateService      _state;
    private readonly ILogger<DashboardStateUpdater> _log;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Track active trades in memory so we can update PnL on exit events
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
        // ── Step 1: hydrate from DB history before subscribing to live events ──
        await HydrateFromHistoryAsync(stoppingToken);

        // ── Step 2: subscribe to live hub events ──────────────────────────────
        _hub.OnLogEntry  += OnLogEntry;
        _hub.OnFeedHealth += OnFeedHealth;

        _log.LogInformation("DashboardStateUpdater: live event subscription active.");

        await Task.Delay(Timeout.Infinite, stoppingToken);

        _hub.OnLogEntry   -= OnLogEntry;
        _hub.OnFeedHealth -= OnFeedHealth;
    }

    // ── Startup history hydration ─────────────────────────────────────────────

    private async Task HydrateFromHistoryAsync(CancellationToken ct)
    {
        try
        {
            _log.LogInformation("DashboardStateUpdater: hydrating from DB history...");

            // Fetch recent entries for each event type we care about, oldest first
            var allEntries = new List<DISLogEntry>();

            foreach (var eventType in new[]
            {
                "state_change", "output_change", "orchestration_change",
                "entry", "risk", "execution", "exit"
            })
            {
                var entries = await _db.GetByEventTypeAsync(eventType, 200, ct);
                allEntries.AddRange(entries);
            }

            // Process oldest → newest so final state reflects most recent values
            foreach (var entry in allEntries.OrderBy(e => e.Id))
                ApplyEntry(entry);

            _log.LogInformation(
                "DashboardStateUpdater: hydrated {Count} entries from DB.", allEntries.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "DashboardStateUpdater: history hydration failed — starting with seed data.");
        }
    }

    // ── Live event handlers ───────────────────────────────────────────────────

    private void OnLogEntry(DISLogEntry entry) => ApplyEntry(entry);

    private void OnFeedHealth(FeedHealthSnapshot snapshot)
    {
        // FeedHealthSnapshot is not part of DashboardState panels.
        // It is handled by components that subscribe to OnFeedHealth directly.
        // Nothing to do here.
    }

    // ── Core dispatcher ───────────────────────────────────────────────────────

    private void ApplyEntry(DISLogEntry entry)
    {
        try
        {
            switch (entry.EventType)
            {
                case "state_change":         ApplyStateChange(entry);         break;
                case "output_change":        ApplyOutputChange(entry);        break;
                case "orchestration_change": ApplyOrchestrationChange(entry); break;
                case "entry":                ApplyEntrySignal(entry);         break;
                case "risk":                 ApplyRisk(entry);                break;
                case "execution":            AppendLogEntry(entry, "EXECUTION"); break;
                case "exit":                 ApplyExit(entry);                break;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "DashboardStateUpdater: failed to apply {Type} entry {Id}",
                entry.EventType, entry.Id);
        }
    }

    // ── state_change ──────────────────────────────────────────────────────────

    private void ApplyStateChange(DISLogEntry entry)
    {
        using var doc  = JsonDocument.Parse(entry.Payload);
        var root       = doc.RootElement;
        var stateName  = root.GetProperty("state").GetString() ?? "";
        var toValue    = root.GetProperty("to").GetString()    ?? "";

        _state.Update(s =>
        {
            // Append to log entries list (cap at 10 for the panel)
            PrependLog(s, entry.Timestamp, "STATE CHANGE:",
                $"{stateName}: {root.GetProperty("from").GetString()} → {toValue}");

            // Map specific state names to DashboardState display fields
            switch (stateName)
            {
                case "structure_phase_state":
                    s.Regime = toValue;
                    break;

                case "volatility_regime_state":
                    s.Volatility = toValue;
                    break;

                case "market_strength" or "momentum_state":
                    s.Strength = toValue;
                    break;

                case "market_structure_state" or "structure_quality_state":
                    s.StructureCondition = toValue;
                    break;
            }

            s.LastLogTime    = entry.Timestamp.ToString("HH:mm");
            s.StateChanges  += 1;
            s.TotalLogs     += 1;
            UpdateLogBars(s);
        });
    }

    // ── output_change ─────────────────────────────────────────────────────────

    private void ApplyOutputChange(DISLogEntry entry)
    {
        using var doc  = JsonDocument.Parse(entry.Payload);
        var root       = doc.RootElement;
        var outputName = root.GetProperty("output").GetString() ?? "";
        var toValue    = root.GetProperty("to").GetString()     ?? "";

        _state.Update(s =>
        {
            PrependLog(s, entry.Timestamp, "OUTPUT CHANGE:",
                $"{outputName}: {root.GetProperty("from").GetString()} → {toValue}");

            switch (outputName)
            {
                case "structural_condition":
                    s.StructureCondition = toValue;
                    break;

                case "directional_bias":
                    s.DirectionBias  = toValue;
                    s.ConflictState  = toValue == "NEUTRAL" ? "HIGH" : "LOW";
                    break;

                case "volatility_environment":
                    s.Volatility = toValue;
                    break;

                case "market_strength":
                    s.Strength = toValue;
                    break;

                case "risk_environment":
                    s.RiskEnvLeft  = toValue;
                    s.RiskEnvRight = toValue;
                    break;

                case "execution_quality":
                    s.ExecutionQuality    = toValue;
                    s.ExecutionQualityNum = toValue switch
                    {
                        "GOOD"       => "1",
                        "ACCEPTABLE" => "2",
                        "POOR"       => "3",
                        _            => "—"
                    };
                    break;

                case "crowd_condition":
                    s.CrowdCondition = toValue;
                    break;

                case "timeframe_consistency":
                    s.TfAlignment = toValue;
                    break;

                case "market_behavior_type":
                    s.BehaviorType = toValue;
                    break;

                case "market_regime":
                    s.Regime       = toValue;
                    s.SystemState  = toValue;
                    break;

                case "trading_permission":
                    s.TradingPermission = toValue;
                    break;
            }

            s.LastLogTime    = entry.Timestamp.ToString("HH:mm");
            s.OutputChanges += 1;
            s.TotalLogs     += 1;
            UpdateLogBars(s);
        });
    }

    // ── orchestration_change ──────────────────────────────────────────────────

    private void ApplyOrchestrationChange(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;

        _state.Update(s =>
        {
            // strategy
            if (root.TryGetProperty("strategy", out var strat) &&
                strat.TryGetProperty("to", out var stratTo))
                s.ActiveCycleLabel = stratTo.GetString() ?? s.ActiveCycleLabel;

            // direction → DirectionBias
            if (root.TryGetProperty("direction", out var dir) &&
                dir.TryGetProperty("to", out var dirTo))
                s.DirectionBias = dirTo.GetString() ?? s.DirectionBias;

            // portfolio_permission → TradingPermission
            if (root.TryGetProperty("portfolio_permission", out var perm) &&
                perm.TryGetProperty("to", out var permTo))
                s.TradingPermission = permTo.GetString() ?? s.TradingPermission;

            // confidence → Last logged
            if (root.TryGetProperty("confidence", out var conf) &&
                conf.TryGetProperty("to", out var confTo))
                s.LastLogged = confTo.GetString() ?? s.LastLogged;

            s.DecisionLogs += 1;
            s.TotalLogs    += 1;
            s.LastLogTime   = entry.Timestamp.ToString("HH:mm");
            UpdateLogBars(s);
        });
    }

    // ── entry ─────────────────────────────────────────────────────────────────

    private void ApplyEntrySignal(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;
        var signal    = root.GetProperty("signal").GetString() ?? "";
        var price     = root.TryGetProperty("price",  out var p) ? p.GetDecimal() : 0m;
        var reason    = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

        bool isLong = signal is "Buy" or "BuyStop" or "BuyLimit";

        // Seed an active trade — risk event will fill in SL/size
        var trade = new ActiveTrade(
            Asset:      entry.Asset,
            Direction:  isLong ? "LONG" : "SHORT",
            Position:   "FULL",
            EntryPrice: price,
            Pnl:        0m,
            StopLoss:   0m);

        _activeTrades[entry.Asset] = trade;

        _state.Update(s =>
        {
            // Add to entry signals list (cap at 4)
            var signals = s.EntrySignals.ToList();
            signals.Insert(0, new SignalRow(entry.Asset, $"{signal} @ {price:N2}", reason));
            if (signals.Count > 4) signals.RemoveAt(signals.Count - 1);
            s.EntrySignals = signals;

            // Rebuild active trades list from memory dictionary
            s.ActiveTrades = _activeTrades.Values.ToList();

            PrependLog(s, entry.Timestamp, "ENTRY SIGNAL",
                $"{signal} {entry.Asset} @ {price:N2}");

            s.ActionEvents += 1;
            s.TotalLogs    += 1;
            s.LastLogTime   = entry.Timestamp.ToString("HH:mm");
            UpdateLogBars(s);
        });
    }

    // ── risk ──────────────────────────────────────────────────────────────────

    private void ApplyRisk(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;

        var entrySize = root.TryGetProperty("entry_size",  out var es) ? es.GetString() ?? "" : "";
        var sl        = root.TryGetProperty("stop_loss",   out var slv) ? slv.GetDecimal() : 0m;
        var posSize   = root.TryGetProperty("position_size", out var ps) ? ps.GetDecimal() : 0m;

        // Update the active trade for this asset with SL and position size
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
            s.PositionSizeLabel = entrySize;
            s.EnteredSize       = posSize;
            s.StopLossDisplay   = $"{sl:N5} {entry.Asset}";
            s.ActiveTrades      = _activeTrades.Values.ToList();
            s.TotalLogs        += 1;
            s.LastLogTime       = entry.Timestamp.ToString("HH:mm");
            UpdateLogBars(s);
        });
    }

    // ── exit ──────────────────────────────────────────────────────────────────

    private void ApplyExit(DISLogEntry entry)
    {
        using var doc = JsonDocument.Parse(entry.Payload);
        var root      = doc.RootElement;

        var posState  = root.TryGetProperty("position_state", out var ps) ? ps.GetString() ?? "" : "";
        var pnl       = root.TryGetProperty("pnl",            out var pv) && pv.ValueKind != JsonValueKind.Null
                          ? pv.GetDecimal() : (decimal?)null;
        var exitPrice = root.TryGetProperty("exit_price",     out var ep) && ep.ValueKind != JsonValueKind.Null
                          ? ep.GetDecimal() : (decimal?)null;
        var reason    = root.TryGetProperty("reason",         out var rv) ? rv.GetString() ?? "" : "";

        if (posState == "Closed")
            _activeTrades.Remove(entry.Asset);
        else if (_activeTrades.TryGetValue(entry.Asset, out var existing))
        {
            _activeTrades[entry.Asset] = existing with
            {
                Position = posState.ToUpperInvariant(),
                Pnl      = pnl ?? existing.Pnl
            };
        }

        _state.Update(s =>
        {
            s.ActiveTrades = _activeTrades.Values.ToList();

            // Update portfolio PnL totals
            s.OpenPnl  = _activeTrades.Values.Sum(t => t.Pnl);
            s.TotalPnl = s.OpenPnl; // Will be refined when closed-trade PnL tracking is added

            PrependLog(s, entry.Timestamp, "EXIT",
                $"{entry.Asset} [{posState}]{(exitPrice.HasValue ? $" @ {exitPrice:N2}" : "")} {reason}");

            s.ActionEvents += 1;
            s.TotalLogs    += 1;
            s.LastLogTime   = entry.Timestamp.ToString("HH:mm");
            s.LastError     = "NONE";
            UpdateLogBars(s);
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void AppendLogEntry(DISLogEntry entry, string typeLabel)
    {
        // Handled inline where needed; this overload is for simple log-only events
    }

    private static void PrependLog(DashboardState s, DateTime ts, string type, string detail)
    {
        var entries = s.LogEntries.ToList();
        entries.Insert(0, new LogEntry(ts.ToString("HH:mm"), type, detail));
        if (entries.Count > 10) entries.RemoveAt(entries.Count - 1);
        s.LogEntries = entries;
    }

    private static void UpdateLogBars(DashboardState s)
    {
        // LogBarsFilled visualises how many of the last 8 evaluation slots fired
        s.LogBarsFilled = Math.Min(8, (s.TotalLogs % 8) + 1);
    }
}
