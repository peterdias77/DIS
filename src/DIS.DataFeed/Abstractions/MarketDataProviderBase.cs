using DIS.Core.Models;

namespace DIS.DataFeed.Abstractions;

/// <summary>
/// Base class for market data providers.
/// Concrete implementations (MT5, Polygon.io, Twelve Data, etc.)
/// inherit from this and are registered in DIS.Execution or DIS.Host.
/// </summary>
public abstract class MarketDataProviderBase : DIS.Core.Interfaces.IMarketDataProvider
{
    public abstract IAsyncEnumerable<OhlcBar>   StreamOhlcAsync(string symbol, string timeframe, CancellationToken ct);
    public abstract IAsyncEnumerable<TickData>  StreamTicksAsync(string symbol, CancellationToken ct);
    public abstract IAsyncEnumerable<OrderBook> StreamOrderBookAsync(string symbol, CancellationToken ct);
    public abstract Task<IReadOnlyList<OhlcBar>> GetHistoryAsync(string symbol, string timeframe, int bars, CancellationToken ct);
}
