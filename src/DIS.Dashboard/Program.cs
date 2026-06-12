using DIS.Core.Interfaces;
using DIS.Dashboard.Services;
using DIS.Logger.Postgres;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radzen;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5200");

// ── Blazor Server ─────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();

// ── PostgreSQL connection string ──────────────────────────────────────────────
// Priority: Environment variable > appsettings.json
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