using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Execution.Adapters;
using DIS.Logger.Postgres;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Engine.Workers;

/// <summary>
/// Main runtime worker. Connects to the MT5 EA on startup, bootstraps history
/// into the bar store, then hands control to BarRouterService for live ingestion.
///
/// Phase 1 responsibilities:
///   1. Connect to EA with retry
///   2. Read history from Mt5TcpBridgeAdapter in-memory store
///   3. Persist history bars to PostgresBarStore (bootstrap)
///   4. Signal BarRouterService that EA is ready (live bar routing begins)
///   5. State engine evaluation loop (Phase 2 — TODO stub preserved)
/// </summary>
public sealed class EvaluationLoopWorker : BackgroundService
{
    private readonly Mt5TcpBridgeAdapter         _bridge;
    private readonly IExecutionAdapter           _execution;
    private readonly IDISLogger                  _logger;
    private readonly PostgresBarStore              _barStore;
    private readonly BarRouterService            _barRouter;
    private readonly IHostApplicationLifetime    _lifetime;
    private readonly ILogger<EvaluationLoopWorker> _log;

    // All 20 DIS symbols — must match AssetRegistry and DIS_Symbols.csv
    private static readonly IReadOnlyList<string> Symbols =
        DIS.Core.Models.AssetRegistry.All.Select(a => a.Symbol).ToList();

    public EvaluationLoopWorker(
        Mt5TcpBridgeAdapter            bridge,
        IExecutionAdapter              execution,
        IDISLogger                     logger,
        PostgresBarStore                 barStore,
        BarRouterService               barRouter,
        IHostApplicationLifetime       lifetime,
        ILogger<EvaluationLoopWorker>  log)
    {
        _bridge    = bridge;
        _execution = execution;
        _logger    = logger;
        _barStore  = barStore;
        _barRouter = barRouter;
        _lifetime  = lifetime;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DIS Engine starting — connecting to MT5 EA...");

        try
        {
            // ── Step 1: Connect to EA (blocks until history HIST blocks arrive) ──
            await ConnectWithRetryAsync(stoppingToken);

            _log.LogInformation("MT5 bridge connected. Beginning history bootstrap...");

            // ── Step 2: Persist history bars to PostgreSQL ────────────────────────
            await BootstrapHistoryAsync(stoppingToken);

            _log.LogInformation("History bootstrap complete. Activating bar router.");

            // ── Step 3: Signal BarRouterService — live bars can now flow ─────
            _barRouter.EaConnected   = true;
            _barRouter.LoadedSymbols = new HashSet<string>(Symbols, StringComparer.OrdinalIgnoreCase);

            _log.LogInformation("DIS Engine running. Phase 2 evaluation loop pending.");

            // ── Step 4: Evaluation loop (Phase 2) ────────────────────────────
            // TODO: Subscribe to live M1 bar stream and drive state engine pipeline:
            //
            //   await foreach (var bar in _bridge.StreamOhlcAsync(symbol, "M1", stoppingToken))
            //   {
            //       var context  = await BuildMarketContextAsync(bar, stoppingToken);
            //       var states   = RunStateCalculators(context);
            //       var outputs  = RunOutputEvaluators(states);
            //       var decision = RunOrchestrationGroups(outputs);
            //       var signal   = _entryEngine.DetermineSignal(decision);
            //       if (_tradeControl.IsEntryAllowed(...))
            //           await _execution.SubmitOrderAsync(...);
            //       EvaluateOpenPositions(decision);
            //   }

            // Park until shutdown — BarRouterService runs independently
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("DIS Engine stopped.");
        }
        catch (Exception ex)
        {
            _log.LogCritical(ex, "DIS Engine fatal error. Shutting down.");
            _lifetime.StopApplication();
        }
    }

    // ── History bootstrap ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads the in-memory history received from the EA during ConnectAsync
    /// and bulk-writes it to the bar store. INSERT OR IGNORE — safe on reconnect.
    /// </summary>
    private async Task BootstrapHistoryAsync(CancellationToken ct)
    {
        int totalBars    = 0;
        int totalSymbols = 0;

        foreach (var symbol in Symbols)
        {
            // GetHistoryAsync returns bars already in memory from HIST_START blocks
            var bars = await _bridge.GetHistoryAsync(symbol, "M1", 200, ct);
            if (bars.Count == 0)
            {
                _log.LogWarning("Bootstrap: no history for {Symbol}", symbol);
                continue;
            }

            int inserted = await _barStore.WriteBarsAsync(bars, ct);
            totalBars   += inserted;
            totalSymbols++;

            _log.LogDebug("Bootstrap: {Symbol} — {Total} bars received, {New} new stored",
                symbol, bars.Count, inserted);
        }

        _log.LogInformation(
            "History bootstrap: {Symbols}/{Total} symbols, {Bars} new bars stored.",
            totalSymbols, Symbols.Count, totalBars);
    }

    // ── Connection with retry ─────────────────────────────────────────────────

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _bridge.ConnectAsync(ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(
                    "Cannot connect to MT5 EA: {Msg}. " +
                    "Make sure DIS.mq5 is running in MT5. Retrying in 5s...",
                    ex.Message);
                await Task.Delay(5_000, ct);
            }
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await _bridge.DisposeAsync();
        await base.StopAsync(ct);
    }
}
