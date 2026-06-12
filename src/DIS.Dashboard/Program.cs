using DIS.Core.Interfaces;
using DIS.Dashboard.Services;
using DIS.Logger.Postgres;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radzen;

// ─────────────────────────────────────────────────────────────────────────────
// DIS Dashboard — Blazor Server entry point
// Runs on http://localhost:5200
// Connects to DIS.Engine SignalR hub at http://localhost:5100/dis-hub
// Reads history from the same PostgreSQL instance written by DIS.Engine
//
// Required environment variables:
//   DIS_PG_CONN   PostgreSQL connection string (mandatory, same as DIS.Engine)
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5200");

// ── Blazor Server ─────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();

// ── PostgreSQL connection string ──────────────────────────────────────────────
var pgConn = Environment.GetEnvironmentVariable("DIS_PG_CONN")
    ?? throw new InvalidOperationException(
        "DIS_PG_CONN environment variable is not set. " +
        "Example: Host=localhost;Port=5432;Database=dis;Username=dis;Password=secret");

// ── PostgreSQL log reader ─────────────────────────────────────────────────────
// Read-only connection to the same DB written by DIS.Engine.
// PostgresLogWriter implements ILogReader — Dashboard only calls the read methods.
builder.Services.AddSingleton<PostgresLogWriter>(_ =>
{
    var writer = new PostgresLogWriter(pgConn);
    // InitialiseAsync is idempotent — safe to call from Dashboard too.
    // Ensures tables exist if Dashboard starts before Engine (dev convenience).
    writer.InitialiseAsync().GetAwaiter().GetResult();
    return writer;
});
builder.Services.AddSingleton<ILogReader>(sp => sp.GetRequiredService<PostgresLogWriter>());

// ── Dashboard services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<LogReaderService>();
builder.Services.AddSingleton<IDashboardStateService, DashboardStateService>();

// ── SignalR hub client ────────────────────────────────────────────────────────
builder.Services.AddSingleton<DashboardHubClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DashboardHubClient>());

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<DIS.Dashboard.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync();
