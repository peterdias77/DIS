using DIS.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Engine.Workers;

/// <summary>
/// Main runtime worker. Drives the per-asset evaluation loop.
/// Runs as a BackgroundService alongside the ASP.NET Core SignalR host.
/// </summary>
public sealed class EvaluationLoopWorker : BackgroundService
{
    private readonly IMarketDataProvider         _feed;
    private readonly IExecutionAdapter           _execution;
    private readonly IDISLogger                  _logger;
    private readonly IHostApplicationLifetime    _lifetime;
    private readonly ILogger<EvaluationLoopWorker> _log;

    public EvaluationLoopWorker(
        IMarketDataProvider            feed,
        IExecutionAdapter              execution,
        IDISLogger                     logger,
        IHostApplicationLifetime       lifetime,
        ILogger<EvaluationLoopWorker>  log)
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
            // TODO: Connect MT5 adapters.
            //       Warm up all 20 assets (load history, seed baseline windows).
            //       Subscribe to tick/OHLC streams for all 20 assets.
            //       Fan out per-asset evaluation pipelines.

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
