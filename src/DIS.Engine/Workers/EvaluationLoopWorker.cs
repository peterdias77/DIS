using DIS.Core.Interfaces;
using DIS.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Engine.Workers;

/// <summary>
/// Main runtime worker. Drives the per-asset evaluation loop:
///
///   For each asset (20 assets):
///     1. Receive latest market data from IMarketDataProvider
///     2. Build MarketContext
///     3. Run all 34 state calculators → StateSnapshot
///     4. Run all 13 output evaluators → OutputSnapshot
///     5. Run 5 orchestration groups   → OrchestrationDecision
///     6. Entry engine                 → EntrySignal + price
///     7. Trade control gate           → allow / block
///     8. Risk manager                 → EntrySize, SL, PositionSize
///     9. Execution adapter            → submit order
///    10. Exit manager                 → evaluate open positions
///    11. Logger                       → emit changed events
///
/// Evaluation interval: driven by market data events (tick/bar arrival),
/// not a fixed timer. The worker subscribes to the data feed and processes
/// each symbol as new data arrives.
/// </summary>
public sealed class EvaluationLoopWorker : BackgroundService
{
    private readonly IMarketDataProvider _feed;
    private readonly IExecutionAdapter   _execution;
    private readonly IDISLogger          _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<EvaluationLoopWorker> _log;

    public EvaluationLoopWorker(
        IMarketDataProvider      feed,
        IExecutionAdapter        execution,
        IDISLogger               logger,
        IHostApplicationLifetime lifetime,
        ILogger<EvaluationLoopWorker> log)
    {
        _feed      = feed;
        _execution = execution;
        _logger    = logger;
        _lifetime  = lifetime;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DIS evaluation loop starting...");

        try
        {
            // TODO: Connect MT5 adapters (ConnectAsync).
            //       Warm up all 20 assets (load history, seed baseline windows).
            //       Subscribe to tick/OHLC streams for all 20 assets.
            //       Fan out per-asset evaluation pipelines.
            //       Implement graceful shutdown on stoppingToken cancellation.

            // Placeholder: keep the worker alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("DIS evaluation loop stopped.");
        }
        catch (Exception ex)
        {
            _log.LogCritical(ex, "DIS evaluation loop encountered a fatal error. Shutting down.");
            _lifetime.StopApplication();
        }
    }
}
