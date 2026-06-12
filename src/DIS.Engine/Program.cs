using DIS.Core.Interfaces;
using DIS.Engine.Hubs;
using DIS.Engine.Publishers;
using DIS.Engine.Workers;
using DIS.Execution.Adapters;
using DIS.Logger;
using DIS.Logger.Postgres;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// DIS Engine — entry point
//
// Two things run in one process:
//   1. ASP.NET Core web host  → SignalR hub at http://localhost:5100/dis-hub
//   2. BackgroundServices     → EvaluationLoopWorker + BarRouterService
//
// MT5 connectivity:
//   The DIS.mq5 EA runs as a TCP SERVER on port 9000.
//   This process connects as a TCP CLIENT via Mt5TcpBridgeAdapter.
//
// Database: PostgreSQL
//   Connection string sourced from DIS_PG_CONN environment variable.
//   Both PostgresLogWriter (log_entries) and PostgresBarStore (ohlc_bars)
//   connect to the same Postgres instance but use separate NpgsqlDataSource
//   pools — each class manages its own pool internally.
//   Schema + tables created automatically on startup (idempotent).
//
// Required environment variables:
//   DIS_PG_CONN   PostgreSQL connection string (mandatory)
//   DIS_EA_HOST   MT5 EA host (default: 127.0.0.1)
//   DIS_EA_PORT   MT5 EA port (default: 9000)
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5100");

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── PostgreSQL connection string ──────────────────────────────────────────────
var pgConn = Environment.GetEnvironmentVariable("DIS_PG_CONN");

if (string.IsNullOrEmpty(pgConn))
{
    // Fallback to configuration file
    pgConn = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException(
            "DIS_PG_CONN environment variable or 'Postgres' connection string is not set. " +
            "Example: Host=localhost;Port=5432;Database=dis;Username=dis;Password=secret");
}

// ── PostgreSQL log writer ─────────────────────────────────────────────────────
builder.Services.AddSingleton<PostgresLogWriter>(_ =>
{
    var writer = new PostgresLogWriter(pgConn);
    writer.InitialiseAsync().GetAwaiter().GetResult();
    return writer;
});
builder.Services.AddSingleton<ILogReader>(sp => sp.GetRequiredService<PostgresLogWriter>());

// ── PostgreSQL bar store ──────────────────────────────────────────────────────
builder.Services.AddSingleton<PostgresBarStore>(_ =>
{
    var store = new PostgresBarStore(pgConn);
    store.InitialiseAsync().GetAwaiter().GetResult();
    return store;
});

// ── Publishers ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISignalRPublisher, SignalRPublisher>();
builder.Services.AddSingleton<IFeedHealthPublisher, FeedHealthPublisher>();

// ── DIS Logger ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDISLogger, DisLogger>();

// ── MT5 TCP Bridge Adapter ────────────────────────────────────────────────────
var eaHost = Environment.GetEnvironmentVariable("DIS_EA_HOST") ?? "127.0.0.1";
var eaPort = int.TryParse(Environment.GetEnvironmentVariable("DIS_EA_PORT"), out var p) ? p : 9000;

builder.Services.AddSingleton<Mt5TcpBridgeAdapter>(sp =>
    new Mt5TcpBridgeAdapter(
        eaHost,
        eaPort,
        sp.GetRequiredService<ILogger<Mt5TcpBridgeAdapter>>()));

builder.Services.AddSingleton<IMarketDataProvider>(sp =>
    sp.GetRequiredService<Mt5TcpBridgeAdapter>());

builder.Services.AddSingleton<IExecutionAdapter>(sp =>
    sp.GetRequiredService<Mt5TcpBridgeAdapter>());

// ── Bar Router (Phase 1 — live bar ingestion) ─────────────────────────────────
// Singleton so EvaluationLoopWorker can set EaConnected = true on it.
builder.Services.AddSingleton<BarRouterService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BarRouterService>());

// ── Evaluation Loop Worker ────────────────────────────────────────────────────
builder.Services.AddHostedService<EvaluationLoopWorker>();

// ── State engine, output layer, orchestration etc. (Phase 2) ──────────────────
// TODO: Register all 34 state calculators, 13 output evaluators,
//       5 orchestration groups, EntryEngine, TradeControl, RiskManager, ExitManager.

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// SignalR hub — Dashboard connects here
app.MapHub<DisHub>("/dis-hub");

// Health endpoint — feed + connection status
app.MapGet("/health", async context =>
{
    var barStore = context.RequestServices.GetRequiredService<PostgresBarStore>();
    var router   = context.RequestServices.GetRequiredService<BarRouterService>();
    var counts   = await barStore.GetBarCountsAsync();

    await context.Response.WriteAsJsonAsync(new
    {
        status            = "DIS Engine running",
        ea_host           = eaHost,
        ea_port           = eaPort,
        ea_connected      = router.EaConnected,
        symbols_ingesting = router.LoadedSymbols.Count,
        total_bars        = counts.Values.Sum(),
        bar_counts        = counts,
        time              = DateTime.UtcNow
    });
});

await app.RunAsync();
