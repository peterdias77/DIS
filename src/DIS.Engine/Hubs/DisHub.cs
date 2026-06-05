using DIS.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace DIS.Engine.Hubs;

/// <summary>
/// SignalR hub hosted in DIS.Engine on http://localhost:5100/dis-hub.
/// DIS.Dashboard connects as a client and receives live DISLogEntry events.
///
/// Client method: "ReceiveLogEntry" — called on every new log event.
/// </summary>
public sealed class DisHub : Hub
{
    /// <summary>
    /// Called by the Dashboard client on connect to confirm the connection.
    /// Returns a simple acknowledgement with the server timestamp.
    /// </summary>
    public Task<string> Ping() =>
        Task.FromResult($"DIS Engine connected at {DateTime.UtcNow:O}");
}
