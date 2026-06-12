using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Logger.Postgres;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Engine.Workers;

/// <summary>
/// Subscribes to M1 bar streams for all 20 DIS assets, persists every bar
/// to PostgreSQL via PostgresBarStore, and broadcasts FeedHealthSnapshot updates
/// to connected Dashboard clients every 5 seconds.
///
/// This is the "bar router" component required for Phase 1 exit criteria:
///   ✔ All 20 symbols ingesting and storing bars
///   ✔ History bootstrap clean (history written on connect)
///   ✔ Dashboard showing feed status
///
/// Lifecycle:
///   Started after EvaluationLoopWorker has called ConnectAsync() successfully.
///   One Task per symbol (fan-out). All tasks share the same PostgresBarStore.
///   A health-broadcast timer runs independently every 5 seconds.
/// </summary>
public sealed class BarRouterService : BackgroundService
{
    private readonly IMarketDataProvider                _feed;
    private readonly PostgresBarStore                     _barStore;
    private readonly IFeedHealthPublisher               _publisher;
    private readonly ILogger<BarRouterService>          _log;

    // Per-symbol live counters (session bars since last connect)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int>
        _sessionCounts = new();

    // Set by EvaluationLoopWorker once EA is connected and history is loaded
    public bool EaConnected { get; set; }
    public IReadOnlySet<string> LoadedSymbols { get; set; } = new HashSet<string>();

    // Stale threshold — if no bar received for this long, mark as STALE
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(3);

    public BarRouterService(
        IMarketDataProvider       feed,
        PostgresBarStore            barStore,
        IFeedHealthPublisher      publisher,
        ILogger<BarRouterService> log)
    {
        _feed      = feed;
        _barStore  = barStore;
        _publisher = publisher;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("BarRouterService starting — waiting for EA connection...");

        // Wait until EaConnected is set by EvaluationLoopWorker
        while (!EaConnected && !stoppingToken.IsCancellationRequested)
            await Task.Delay(500, stoppingToken);

        if (stoppingToken.IsCancellationRequested) return;

        _log.LogInformation("BarRouterService: EA connected. Subscribing to {Count} symbols.",
            LoadedSymbols.Count);

        // Fan-out: one streaming task per symbol
        var streamTasks = LoadedSymbols
            .Select(sym => StreamSymbolAsync(sym, stoppingToken))
            .ToList();

        // Health broadcast timer — every 5 seconds
        var healthTimer = BroadcastHealthLoopAsync(stoppingToken);

        await Task.WhenAll(streamTasks.Append(healthTimer));

        _log.LogInformation("BarRouterService stopped.");
    }

    // ── Per-symbol stream ─────────────────────────────────────────────────────

    private async Task StreamSymbolAsync(string symbol, CancellationToken ct)
    {
        _sessionCounts[symbol] = 0;

        _log.LogDebug("BarRouter: subscribing to {Symbol}", symbol);

        try
        {
            await foreach (var bar in _feed.StreamOhlcAsync(symbol, "M1", ct))
            {
                bool isNew = await _barStore.WriteBarAsync(bar, ct);

                if (isNew)
                {
                    _sessionCounts.AddOrUpdate(symbol, 1, (_, old) => old + 1);

                    _log.LogDebug("Bar stored: {Symbol} {Time} C={Close} (session #{Count})",
                        symbol, bar.Time, bar.Close, _sessionCounts[symbol]);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "BarRouter stream error for {Symbol}", symbol);
        }
    }

    // ── Health broadcast ──────────────────────────────────────────────────────

    private async Task BroadcastHealthLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                var snapshot = await BuildSnapshotAsync(ct);
                await _publisher.PublishFeedHealthAsync(snapshot, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Health broadcast failed.");
            }
        }
    }

    private async Task<FeedHealthSnapshot> BuildSnapshotAsync(CancellationToken ct)
    {
        var counts    = await _barStore.GetBarCountsAsync(ct);
        var lastTimes = await _barStore.GetLastBarTimesAsync(ct);
        var now       = DateTime.UtcNow;

        var symbols = LoadedSymbols.Select(sym =>
        {
            counts.TryGetValue(sym, out long barCount);
            lastTimes.TryGetValue(sym, out DateTime lastTime);
            _sessionCounts.TryGetValue(sym, out int sessionBars);

            string status;
            if (!EaConnected)
                status = "DISCONNECTED";
            else if (barCount == 0)
                status = "NO_DATA";
            else if (lastTime != default && (now - lastTime) > StaleThreshold)
                status = "STALE";
            else
                status = "OK";

            return new SymbolFeedStatus
            {
                Symbol           = sym,
                EaConnected      = EaConnected,
                HistoryLoaded    = LoadedSymbols.Contains(sym),
                BarCount         = barCount,
                LastBarTime      = lastTime == default ? null : lastTime,
                BarsThisSession  = sessionBars,
                Status           = status
            };
        }).ToList();

        int ok      = symbols.Count(s => s.Status == "OK");
        int stale   = symbols.Count(s => s.Status == "STALE");
        int noData  = symbols.Count(s => s.Status is "NO_DATA" or "DISCONNECTED");
        long total  = counts.Values.Sum();

        return new FeedHealthSnapshot
        {
            Timestamp      = now,
            EaConnected    = EaConnected,
            SymbolsTotal   = symbols.Count,
            SymbolsOk      = ok,
            SymbolsStale   = stale,
            SymbolsNoData  = noData,
            TotalBarsStored = total,
            Symbols        = symbols
        };
    }
}
