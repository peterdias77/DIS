using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.Engine.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DIS.Engine.Publishers;

/// <summary>
/// Broadcasts FeedHealthSnapshot to all Dashboard clients via DisHub.
/// Client method: "ReceiveFeedHealth"
/// </summary>
public sealed class FeedHealthPublisher : IFeedHealthPublisher
{
    private readonly IHubContext<DisHub> _hub;

    public FeedHealthPublisher(IHubContext<DisHub> hub) => _hub = hub;

    public async Task PublishFeedHealthAsync(FeedHealthSnapshot snapshot, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync("ReceiveFeedHealth", snapshot, ct);
    }
}
