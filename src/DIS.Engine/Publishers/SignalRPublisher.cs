using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Engine.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DIS.Engine.Publishers;

/// <summary>
/// Implements ISignalRPublisher by broadcasting DISLogEntry events
/// to all connected Dashboard clients via DisHub.
///
/// Called by DIS.Logger after every SQLite write.
/// The client method name "ReceiveLogEntry" must match DashboardHubClient.
/// </summary>
public sealed class SignalRPublisher : ISignalRPublisher
{
    private readonly IHubContext<DisHub> _hub;

    public SignalRPublisher(IHubContext<DisHub> hub) => _hub = hub;

    public async Task PublishAsync(DISLogEntry entry, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync("ReceiveLogEntry", entry, ct);
    }
}
