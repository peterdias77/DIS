using DIS.Core.Interfaces;
using DIS.Dashboard.Services;
using DIS.Logger.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radzen;

// ─────────────────────────────────────────────────────────────────────────────
// DIS Dashboard — Blazor Server entry point
// Runs on http://localhost:5200
// Connects to DIS.Engine SignalR hub at http://localhost:5100/dis-hub
// Reads history from the same SQLite DB written by DIS.Engine
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5200");

// ── Blazor Server ─────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();

// ── SQLite log reader ─────────────────────────────────────────────────────────
// Reads from the same dis_logs.db written by DIS.Engine.
// Set DIS_DB_PATH environment variable to point at the correct file location.
var dbPath = Environment.GetEnvironmentVariable("DIS_DB_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "dis_logs.db");

builder.Services.AddSingleton<SqliteLogWriter>(_ =>
{
    var writer = new SqliteLogWriter(dbPath);
    writer.InitialiseAsync().GetAwaiter().GetResult();
    return writer;
});

// Register both as ILogReader (for Razor page injection) and as SqliteLogWriter
builder.Services.AddSingleton<ILogReader>(sp => sp.GetRequiredService<SqliteLogWriter>());

// ── Dashboard services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<LogReaderService>();

// ── SignalR hub client ────────────────────────────────────────────────────────
builder.Services.AddSingleton<DashboardHubClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DashboardHubClient>());

builder.Services.AddSingleton<IDashboardStateService, DashboardStateService>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<DIS.Dashboard.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync();
