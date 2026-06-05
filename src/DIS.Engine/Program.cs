using DIS.Core.Interfaces;
using DIS.Engine.Hubs;
using DIS.Engine.Publishers;
using DIS.Engine.Workers;
using DIS.Execution.Adapters;
using DIS.Logger;
using DIS.Logger.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// DIS Engine — entry point
// Hosts two things in one process:
//   1. ASP.NET Core web host → SignalR hub at http://localhost:5100/dis-hub
//   2. BackgroundService     → EvaluationLoopWorker (trading loop)
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5100");

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// TODO: Add file/structured sink (Serilog) for persistent console logs

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── SQLite log writer ─────────────────────────────────────────────────────────
var dbPath = Path.Combine(AppContext.BaseDirectory, "dis_logs.db");
builder.Services.AddSingleton(_ =>
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

// ── MT5 Adapters ──────────────────────────────────────────────────────────────
// TODO: Bind addresses from appsettings.json
builder.Services.AddSingleton<Mt5ExecutionAdapter>(_ =>
    new Mt5ExecutionAdapter(
        pushAddress: "tcp://localhost:5555",
        reqAddress:  "tcp://localhost:5556"));

builder.Services.AddSingleton<Mt5DataFeedAdapter>(_ =>
    new Mt5DataFeedAdapter(
        subAddress: "tcp://localhost:5557"));

builder.Services.AddSingleton<IExecutionAdapter>(sp =>
    sp.GetRequiredService<Mt5ExecutionAdapter>());

builder.Services.AddSingleton<IMarketDataProvider>(sp =>
    sp.GetRequiredService<Mt5DataFeedAdapter>());

// ── State engine, output layer, orchestration etc. ────────────────────────────
// TODO: Register all 34 state calculators, 13 output evaluators,
//       5 orchestration groups, EntryEngine, TradeControl,
//       RiskManager, ExitManager.

// ── Evaluation loop worker ────────────────────────────────────────────────────
builder.Services.AddHostedService<EvaluationLoopWorker>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Map SignalR hub — Dashboard connects here
app.MapHub<DisHub>("/dis-hub");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "DIS Engine running", time = DateTime.UtcNow }));

await app.RunAsync();
