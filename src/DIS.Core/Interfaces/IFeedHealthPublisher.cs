using DIS.Core.Models;
//using DIS.Engine.Hubs;
//using Microsoft.AspNetCore.SignalR;

namespace DIS.Core.Interfaces;

/// <summary>
/// Publishes FeedHealthSnapshot to connected Dashboard clients.
/// Client method name: "ReceiveFeedHealth"
/// </summary>
public interface IFeedHealthPublisher
{
    Task PublishFeedHealthAsync(FeedHealthSnapshot snapshot, CancellationToken ct = default);
}
