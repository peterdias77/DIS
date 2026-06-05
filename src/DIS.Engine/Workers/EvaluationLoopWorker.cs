using DIS.Core.Interfaces;
using DIS.Execution.Adapters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Engine.Workers;

/// <summary>
/// Main runtime worker. Connects to the MT5 EA on startup,
/// then drives the per-asset evaluation loop on every incoming bar batch.
/// </summary>
public sealed class EvaluationLoopWorker : BackgroundService
{
    private readonly Mt5TcpBridgeAdapter         _bridge;
    private readonly IExecutionAdapter           _execution;
    private readonly IDISLogger                  _logger;
    private readonly IHostApplicationLifetime    _lifetime;
    private readonly ILogger<EvaluationLoopWorker> _log;

    public EvaluationLoopWorker(
        Mt5TcpBridgeAdapter            bridge,
        IExecutionAdapter              execution,
        IDISLogger                     logger,
        IHostApplicationLifetime       lifetime,
        ILogger<EvaluationLoopWorker>  log)
    {
        _bridge    = bridge;
        _execution = execution;
        _logger    = logger;
        _lifetime  = lifetime;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DIS Engine starting — connecting to MT5 EA...");

        try
        {
            // ── Step 1: Connect to EA (blocks until history is received) ──────
            await ConnectWithRetryAsync(stoppingToken);

            _log.LogInformation("MT5 bridge connected. DIS evaluation loop running.");

            // ── Step 2: Subscribe to live M1 bar stream for all 20 assets ────
            // TODO: Read symbol list from AssetRegistry, fan out one subscriber
            //       per asset, and pipe each bar into the state engine pipeline:
            //
            //   await foreach (var bar in _bridge.StreamOhlcAsync(symbol, "M1", stoppingToken))
            //   {
            //       var context    = await BuildMarketContextAsync(bar, stoppingToken);
            //       var states     = RunStateCalculators(context);
            //       var outputs    = RunOutputEvaluators(states);
            //       var decision   = RunOrchestrationGroups(outputs);
            //       var signal     = _entryEngine.DetermineSignal(decision);
            //       var controlled = _tradeControl.IsEntryAllowed(...);
            //       if (controlled) await _execution.SubmitOrderAsync(...);
            //       EvaluateOpenPositions(decision);
            //   }

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

    /// <summary>
    /// Attempts to connect to the MT5 EA with retry.
    /// The EA must be running and attached to a chart before this succeeds.
    /// Retries every 5 seconds indefinitely until connected or shutdown requested.
    /// </summary>
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
