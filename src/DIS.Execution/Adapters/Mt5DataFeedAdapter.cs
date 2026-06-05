using System.Runtime.CompilerServices;
using System.Text.Json;
using DIS.Core.Models;
using DIS.DataFeed.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace DIS.Execution.Adapters;

/// <summary>
/// MT5 market data feed adapter.
/// Receives tick and OHLC data published by the MT5 EA via ZeroMQ PUB/SUB.
///
/// Topic conventions (agreed with MT5 EA):
///   "TICK:{SYMBOL}"       — raw tick (bid, ask, last, volume)
///   "OHLC:{SYMBOL}:{TF}"  — completed or in-progress bar
///   "OB:{SYMBOL}"         — order book snapshot
///
/// The MT5 EA publishes on each OnTick() call across all subscribed symbols.
/// A single EA instance running as an MT5 Service covers all 20 assets
/// without requiring 20 open charts.
///
/// IMPORTANT — Same broker DLL restriction applies as Mt5ExecutionAdapter.
/// Verify libzmq.dll is loadable before wiring this into production.
/// </summary>
public sealed class Mt5DataFeedAdapter : MarketDataProviderBase, IAsyncDisposable
{
    private readonly string _subAddress;   // e.g. "tcp://localhost:5557"
    private SubscriberSocket? _subSocket;
    private bool _connected;

    public Mt5DataFeedAdapter(string subAddress) => _subAddress = subAddress;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        // TODO: Initialise SubscriberSocket, connect to _subAddress,
        //       subscribe to all topics (empty string prefix = all),
        //       set _connected = true.
        throw new NotImplementedException();
    }

    // ── IMarketDataProvider ───────────────────────────────────────────────────

    public override async IAsyncEnumerable<OhlcBar> StreamOhlcAsync(
        string symbol, string timeframe,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConnected();
        var topic = $"OHLC:{symbol.ToUpperInvariant()}:{timeframe.ToUpperInvariant()}";

        // TODO: Subscribe _subSocket to topic.
        //       Loop: receive message frame, deserialise payload to OhlcBar, yield.
        //       Respect ct for graceful shutdown.
        await Task.CompletedTask;
        yield break;
    }

    public override async IAsyncEnumerable<TickData> StreamTicksAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConnected();
        var topic = $"TICK:{symbol.ToUpperInvariant()}";

        // TODO: Subscribe to topic, receive and deserialise TickData frames, yield.
        await Task.CompletedTask;
        yield break;
    }

    public override async IAsyncEnumerable<OrderBook> StreamOrderBookAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConnected();
        var topic = $"OB:{symbol.ToUpperInvariant()}";

        // TODO: Subscribe to topic, receive and deserialise OrderBook frames, yield.
        await Task.CompletedTask;
        yield break;
    }

    public override Task<IReadOnlyList<OhlcBar>> GetHistoryAsync(
        string symbol, string timeframe, int bars, CancellationToken ct)
    {
        // TODO: Delegate to Mt5ExecutionAdapter.RequestHistoryAsync()
        //       (REQ/REP channel) rather than the PUB/SUB feed.
        //       Wire the adapter reference in DI during Host setup.
        throw new NotImplementedException();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!_connected)
            throw new InvalidOperationException(
                "Mt5DataFeedAdapter is not connected. Call ConnectAsync() first.");
    }

    public async ValueTask DisposeAsync()
    {
        _subSocket?.Dispose();
        await Task.CompletedTask;
    }
}
