using DIS.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIS.Dashboard.Services;

/// <summary>
/// Background service that maintains a SignalR connection to DIS.Engine
/// at http://localhost:5100/dis-hub.
///
/// Receives "ReceiveLogEntry" events and raises OnLogEntry for
/// Blazor components to subscribe to and re-render.
///
/// Implements automatic reconnect with exponential backoff.
/// </summary>
public sealed class DashboardHubClient : BackgroundService
{
    private readonly ILogger<DashboardHubClient> _log;
    private HubConnection? _connection;

    /// <summary>Raised on the thread pool whenever a new DISLogEntry arrives from the Engine.</summary>
    public event Action<DISLogEntry>? OnLogEntry;

    /// <summary>Current connection state — readable by Blazor components.</summary>
    public HubConnectionState ConnectionState =>
        _connection?.State ?? HubConnectionState.Disconnected;

    public DashboardHubClient(ILogger<DashboardHubClient> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

        // Wire the incoming event
        _connection.On<DISLogEntry>("ReceiveLogEntry", entry =>
        {
            OnLogEntry?.Invoke(entry);
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

        // Keep alive until shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_connection is not null)
            await _connection.StopAsync(ct);
        await base.StopAsync(ct);
    }
}
