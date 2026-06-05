using DIS.Core.Interfaces;
using DIS.Execution.Adapters;
using DIS.Logger;
using DIS.Host.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// DIS Host — entry point
// Bootstraps all services and starts the evaluation loop worker.
// ─────────────────────────────────────────────────────────────────────────────

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        // ── Logging ───────────────────────────────────────────────────────────
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            // TODO: Add file sink (Serilog or custom JSON file writer)
            //       for persistent structured log storage.
        });

        // ── DIS Logger ────────────────────────────────────────────────────────
        services.AddSingleton<IDISLogger, DisLogger>();

        // ── MT5 Adapters ──────────────────────────────────────────────────────
        // TODO: Bind addresses from appsettings.json / environment variables.
        services.AddSingleton<Mt5ExecutionAdapter>(sp =>
            new Mt5ExecutionAdapter(
                pushAddress: "tcp://localhost:5555",
                reqAddress:  "tcp://localhost:5556"));

        services.AddSingleton<Mt5DataFeedAdapter>(sp =>
            new Mt5DataFeedAdapter(
                subAddress: "tcp://localhost:5557"));

        services.AddSingleton<IExecutionAdapter>(sp =>
            sp.GetRequiredService<Mt5ExecutionAdapter>());

        services.AddSingleton<IMarketDataProvider>(sp =>
            sp.GetRequiredService<Mt5DataFeedAdapter>());

        // ── State engine, output layer, orchestration, etc. ───────────────────
        // TODO: Register all 34 state calculators, 13 output evaluators,
        //       5 orchestration groups, EntryEngine, TradeControl,
        //       RiskManager, ExitManager as singletons or scoped services.

        // ── Main evaluation worker ────────────────────────────────────────────
        services.AddHostedService<EvaluationLoopWorker>();
    })
    .Build();

await host.RunAsync();
