using DIS.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Dashboard.Services;

/// <summary>
/// Background service that maintains a SignalR connection to DIS.Engine
/// at http://localhost:5100/dis-hub.
///
/// Receives two event types:
///   "ReceiveLogEntry"  → raises OnLogEntry  (state/output/trade events)
///   "ReceiveFeedHealth" → raises OnFeedHealth (bar ingestion status, every 5s)
///
/// Implements automatic reconnect with exponential backoff.
/// </summary>
public sealed class DashboardHubClient : BackgroundService
{
    private readonly ILogger<DashboardHubClient> _log;
    private HubConnection? _connection;

    /// <summary>Raised on the thread pool whenever a new DISLogEntry arrives.</summary>
    public event Action<DISLogEntry>?       OnLogEntry;

    /// <summary>Raised every 5 seconds with the latest FeedHealthSnapshot.</summary>
    public event Action<FeedHealthSnapshot>? OnFeedHealth;

    public HubConnectionState ConnectionState =>
        _connection?.State ?? HubConnectionState.Disconnected;

    public DashboardHubClient(ILogger<DashboardHubClient> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5100/dis-hub")
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            // ── Event: log entry (state, output, trade events) ────────────────
            _connection.On<DISLogEntry>("ReceiveLogEntry", entry =>
            {
                OnLogEntry?.Invoke(entry);
            });

            // ── Event: feed health snapshot (bar ingestion status) ─────────────
            _connection.On<FeedHealthSnapshot>("ReceiveFeedHealth", snapshot =>
            {
                OnFeedHealth?.Invoke(snapshot);
            });

            _connection.Reconnecting += error =>
            {
                _log.LogWarning("DIS Hub connection lost. Reconnecting... {Error}", error?.Message);
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                _log.LogInformation("DIS Hub reconnected. ConnectionId: {Id}", connectionId);
                return Task.CompletedTask;
            };

            // Connect with retry loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _connection.StartAsync(stoppingToken);
                    _log.LogInformation("Connected to DIS Engine hub.");
                    break;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _log.LogWarning(ex, "Could not connect to DIS Engine hub. Retrying in 5s...");
                    await Task.Delay(5_000, stoppingToken);
                }
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error in DashboardHubClient execution.");
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_connection is not null)
            await _connection.StopAsync(ct);
        await base.StopAsync(ct);
    }
}
