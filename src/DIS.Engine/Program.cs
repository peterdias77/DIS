using DIS.Core.Interfaces;
using DIS.Engine.Hubs;
using DIS.Engine.Publishers;
using DIS.Engine.Workers;
using DIS.Execution.Adapters;
using DIS.Logger;
using DIS.Logger.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// DIS Engine — entry point
//
// Two things run in one process:
//   1. ASP.NET Core web host  → SignalR hub at http://localhost:5100/dis-hub
//   2. BackgroundService      → EvaluationLoopWorker (trading loop)
//
// MT5 connectivity:
//   The DIS.mq5 EA runs as a TCP SERVER on port 9000.
//   This process connects as a TCP CLIENT via Mt5TcpBridgeAdapter.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5100");

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── SQLite log writer ─────────────────────────────────────────────────────────
var dbPath = Path.Combine(AppContext.BaseDirectory, "dis_logs.db");
builder.Services.AddSingleton<SqliteLogWriter>(_ =>
{
    var writer = new SqliteLogWriter(dbPath);
    writer.InitialiseAsync().GetAwaiter().GetResult();
    return writer;
});
builder.Services.AddSingleton<ILogReader>(sp => sp.GetRequiredService<SqliteLogWriter>());

// ── SignalR publisher ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISignalRPublisher, SignalRPublisher>();

// ── DIS Logger ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDISLogger, DisLogger>();

// ── MT5 TCP Bridge Adapter ────────────────────────────────────────────────────
// The EA (DIS.mq5) is the TCP SERVER on port 9000.
// We connect as a client. Host is always localhost when EA runs on same machine.
// To change port: set environment variable DIS_EA_PORT.
var eaHost = Environment.GetEnvironmentVariable("DIS_EA_HOST") ?? "127.0.0.1";
var eaPort = int.TryParse(Environment.GetEnvironmentVariable("DIS_EA_PORT"), out var p) ? p : 9000;

builder.Services.AddSingleton<Mt5TcpBridgeAdapter>(sp =>
    new Mt5TcpBridgeAdapter(
        eaHost,
        eaPort,
        sp.GetRequiredService<ILogger<Mt5TcpBridgeAdapter>>()));

// Register the same instance as both IMarketDataProvider and IExecutionAdapter
// (single TCP connection handles both data feed and execution)
builder.Services.AddSingleton<IMarketDataProvider>(sp =>
    sp.GetRequiredService<Mt5TcpBridgeAdapter>());

builder.Services.AddSingleton<IExecutionAdapter>(sp =>
    sp.GetRequiredService<Mt5TcpBridgeAdapter>());

// ── State engine, output layer, orchestration etc. ────────────────────────────
// TODO: Register all 34 state calculators, 13 output evaluators,
//       5 orchestration groups, EntryEngine, TradeControl,
//       RiskManager, ExitManager.

// ── Evaluation loop worker ────────────────────────────────────────────────────
builder.Services.AddHostedService<EvaluationLoopWorker>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// SignalR hub — Dashboard connects here
app.MapHub<DisHub>("/dis-hub");

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    status    = "DIS Engine running",
    ea_host   = eaHost,
    ea_port   = eaPort,
    time      = DateTime.UtcNow
}));

await app.RunAsync();
