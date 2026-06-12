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
// Reads history from the same PostgreSQL instance as DIS.Engine
//
// Data flow:
//   PostgreSQL  ──► DashboardStateUpdater (startup hydration)
//   SignalR hub ──► DashboardHubClient.OnLogEntry
//                       └──► DashboardStateUpdater.ApplyEntry()
//                                 └──► IDashboardStateService.Update()
//                                           └──► Dashboard.razor re-renders
//
// Required environment variables:
//   DIS_PG_CONN   PostgreSQL connection string (mandatory)
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5200");

// ── Blazor Server ─────────────────────────────────────────────────────────────
builder.Services.AddRadzenComponents();   // registers ThemeService, DialogService, NotificationService, TooltipService
builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();

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

// ── PostgreSQL log reader ─────────────────────────────────────────────────────
builder.Services.AddSingleton<PostgresLogWriter>(_ =>
{
    var writer = new PostgresLogWriter(pgConn);
    writer.InitialiseAsync().GetAwaiter().GetResult();
    return writer;
});
builder.Services.AddSingleton<ILogReader>(sp => sp.GetRequiredService<PostgresLogWriter>());

// ── Dashboard state ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDashboardStateService, DashboardStateService>();

// ── Hub client (SignalR connection to DIS.Engine) ─────────────────────────────
// Singleton so DashboardStateUpdater can subscribe to its events.
builder.Services.AddSingleton<DashboardHubClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DashboardHubClient>());

// ── State updater (bridges hub + DB → DashboardState) ────────────────────────
// Must be registered AFTER DashboardHubClient so the singleton is available.
builder.Services.AddHostedService<DashboardStateUpdater>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<DIS.Dashboard.Components.App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync();
