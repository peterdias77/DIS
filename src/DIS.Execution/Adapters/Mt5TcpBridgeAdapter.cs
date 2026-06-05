using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using DIS.Core.Interfaces;
using DIS.Core.Models;
using DIS.DataFeed.Abstractions;
using Microsoft.Extensions.Logging;

namespace DIS.Execution.Adapters;

/// <summary>
/// Single TCP client that connects to the DIS.mq5 EA (TCP server on port 9000).
///
/// Responsibilities:
///   DATA FEED  — parses HIST_START/HIST_END and BATCH_START/BATCH_END messages
///                into OhlcBar records and distributes them to subscribers.
///   EXECUTION  — sends ORDER/CLOSE commands and awaits FILL/ERR responses.
///   KEEPALIVE  — sends PING every 30s; EA responds with PONG.
///
/// Threading model:
///   A single dedicated reader task owns the TCP stream.
///   Order commands are sent from the caller's thread under a write lock.
///   FILL responses are matched to pending orders via a ConcurrentDictionary.
/// </summary>
public sealed class Mt5TcpBridgeAdapter : MarketDataProviderBase, IExecutionAdapter, IAsyncDisposable
{
    // ── Configuration ─────────────────────────────────────────────────────────
    private readonly string _host;
    private readonly int    _port;
    private readonly ILogger<Mt5TcpBridgeAdapter> _log;

    // ── TCP state ─────────────────────────────────────────────────────────────
    private TcpClient?    _tcp;
    private NetworkStream? _stream;
    private bool          _connected;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // ── History store ─────────────────────────────────────────────────────────
    // Populated once on connect from HIST_START blocks. Keyed by symbol.
    private readonly ConcurrentDictionary<string, List<OhlcBar>> _history = new();

    // ── Live bar subscribers ──────────────────────────────────────────────────
    // symbol → list of channels waiting for bars on that symbol
    private readonly ConcurrentDictionary<string, List<System.Threading.Channels.Channel<OhlcBar>>>
        _ohlcSubscribers = new();

    // ── Pending order completions ─────────────────────────────────────────────
    // ticketRef → TaskCompletionSource that resolves when FILL or ERR arrives
    private readonly ConcurrentDictionary<string, TaskCompletionSource<OrderResult>>
        _pendingOrders = new();

    // ── Cancellation ─────────────────────────────────────────────────────────
    private CancellationTokenSource? _cts;
    private Task? _readerTask;

    // ── Keepalive ─────────────────────────────────────────────────────────────
    private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(30);

    public Mt5TcpBridgeAdapter(string host, int port, ILogger<Mt5TcpBridgeAdapter> logger)
    {
        _host = host;
        _port = port;
        _log  = logger;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to the EA TCP server and starts the background reader loop.
    /// Blocks until the initial HIST send from the EA is complete.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _log.LogInformation("Connecting to MT5 EA at {Host}:{Port}...", _host, _port);

        _tcp    = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, ct);
        _stream = _tcp.GetStream();
        _connected = true;

        _cts        = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readerTask = Task.Run(() => ReaderLoopAsync(_cts.Token), _cts.Token);

        _log.LogInformation("Connected to MT5 EA. Waiting for initial history...");

        // Wait up to 60s for all HIST blocks to arrive before returning
        // (EA sends history immediately on connection)
        await WaitForHistoryAsync(TimeSpan.FromSeconds(60), ct);

        // Start keepalive loop
        _ = Task.Run(() => KeepaliveLoopAsync(_cts.Token), _cts.Token);

        _log.LogInformation("MT5 bridge ready. History loaded for {Count} symbols.", _history.Count);
    }

    // ── IMarketDataProvider ───────────────────────────────────────────────────

    /// <summary>
    /// Streams completed M1 bars for a symbol as they arrive in BATCH messages.
    /// Each new batch from the EA (every new M1 bar) delivers one bar per subscribed symbol.
    /// </summary>
    public override async IAsyncEnumerable<OhlcBar> StreamOhlcAsync(
        string symbol, string timeframe,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConnected();

        var channel = System.Threading.Channels.Channel.CreateUnbounded<OhlcBar>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });

        _ohlcSubscribers.GetOrAdd(symbol.ToUpperInvariant(),
            _ => new List<System.Threading.Channels.Channel<OhlcBar>>())
            .Add(channel);

        try
        {
            await foreach (var bar in channel.Reader.ReadAllAsync(ct))
                yield return bar;
        }
        finally
        {
            if (_ohlcSubscribers.TryGetValue(symbol.ToUpperInvariant(), out var list))
                list.Remove(channel);
        }
    }

    /// <summary>
    /// The EA does not stream individual ticks — it sends completed M1 bars.
    /// Tick streaming is not supported by this bridge.
    /// </summary>
    public override IAsyncEnumerable<TickData> StreamTicksAsync(string symbol, CancellationToken ct)
        => throw new NotSupportedException(
            "The DIS.mq5 EA sends M1 bar batches, not individual ticks. Use StreamOhlcAsync.");

    /// <summary>
    /// Order book data is not available via this bridge.
    /// States 18/19/20 that require order book must use an alternative data source.
    /// </summary>
    public override IAsyncEnumerable<OrderBook> StreamOrderBookAsync(string symbol, CancellationToken ct)
        => throw new NotSupportedException(
            "Order book data is not available via the MT5 TCP bridge.");

    /// <summary>
    /// Returns history bars received from the EA on connect (HIST_START blocks).
    /// </summary>
    public override Task<IReadOnlyList<OhlcBar>> GetHistoryAsync(
        string symbol, string timeframe, int bars, CancellationToken ct)
    {
        var key = symbol.ToUpperInvariant();
        if (_history.TryGetValue(key, out var list))
        {
            IReadOnlyList<OhlcBar> result = list.TakeLast(bars).ToList();
            return Task.FromResult(result);
        }

        _log.LogWarning("No history available for {Symbol}. Has ConnectAsync completed?", symbol);
        return Task.FromResult<IReadOnlyList<OhlcBar>>(Array.Empty<OhlcBar>());
    }

    // ── IExecutionAdapter ─────────────────────────────────────────────────────

    /// <summary>
    /// Sends an ORDER command to the EA and awaits the FILL or ERR response.
    /// Format: ORDER|ticketRef|sym|BUY/SELL|vol|sl|tp\n
    /// </summary>
    public async Task<OrderResult> SubmitOrderAsync(OrderRequest request, CancellationToken ct)
    {
        EnsureConnected();

        var ticketRef = $"DIS-{request.CycleId}-{request.Symbol}-{DateTime.UtcNow.Ticks}";
        var direction = request.Signal switch
        {
            Core.Enums.EntrySignal.Buy  or
            Core.Enums.EntrySignal.BuyStop  or
            Core.Enums.EntrySignal.BuyLimit  => "BUY",
            _                                => "SELL"
        };

        var cmd = string.Join(EaProtocol.PipeDelimiter,
            EaProtocol.Order,
            ticketRef,
            request.Symbol,
            direction,
            request.Lots.ToString("F2", CultureInfo.InvariantCulture),
            request.StopLoss.ToString(CultureInfo.InvariantCulture),
            request.TakeProfit.ToString(CultureInfo.InvariantCulture))
            + EaProtocol.MsgSeparator;

        var tcs = new TaskCompletionSource<OrderResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingOrders[ticketRef] = tcs;

        try
        {
            await SendRawAsync(cmd, ct);
            _log.LogDebug("ORDER sent: {Cmd}", cmd.TrimEnd());

            // Wait for FILL or ERR with a 10-second timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _pendingOrders.TryRemove(ticketRef, out _);
            return new OrderResult { Success = false, Error = "Order timed out waiting for fill." };
        }
        finally
        {
            _pendingOrders.TryRemove(ticketRef, out _);
        }
    }

    /// <summary>
    /// Sends a CLOSE command to the EA. vol=0 closes the full position.
    /// Format: CLOSE|ticketRef|sym|vol\n
    /// </summary>
    public async Task<bool> ClosePositionAsync(string positionId, CancellationToken ct)
    {
        EnsureConnected();

        // positionId format: "SYM:ticketRef" — parse symbol from it
        var parts = positionId.Split(':');
        var sym   = parts.Length >= 1 ? parts[0] : positionId;
        var tRef  = parts.Length >= 2 ? parts[1] : positionId;

        var cmd = string.Join(EaProtocol.PipeDelimiter,
            EaProtocol.Close, tRef, sym, "0")
            + EaProtocol.MsgSeparator;

        var tcs = new TaskCompletionSource<OrderResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingOrders[$"CLOSE-{tRef}"] = tcs;

        await SendRawAsync(cmd, ct);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await tcs.Task.WaitAsync(timeoutCts.Token);
            return result.Success;
        }
        catch
        {
            return false;
        }
        finally
        {
            _pendingOrders.TryRemove($"CLOSE-{tRef}", out _);
        }
    }

    /// <summary>
    /// Modify is not directly supported by the EA protocol.
    /// SL/TP are set at order time. Modification requires close + re-entry.
    /// </summary>
    public Task<bool> ModifyPositionAsync(string positionId, decimal newSl, decimal newTp, CancellationToken ct)
    {
        _log.LogWarning("ModifyPosition is not supported by the MT5 TCP bridge. SL/TP must be set at order entry.");
        return Task.FromResult(false);
    }

    // ── Background reader loop ────────────────────────────────────────────────

    private async Task ReaderLoopAsync(CancellationToken ct)
    {
        var buffer = new StringBuilder();
        var byteBuffer = new byte[65536];

        _log.LogDebug("MT5 TCP reader loop started.");

        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                int bytesRead = await _stream.ReadAsync(byteBuffer, ct);
                if (bytesRead == 0)
                {
                    _log.LogWarning("MT5 EA closed the connection.");
                    break;
                }

                buffer.Append(Encoding.UTF8.GetString(byteBuffer, 0, bytesRead));

                // Process all complete messages (newline-terminated)
                string buffered = buffer.ToString();
                int newlineIdx;

                while ((newlineIdx = buffered.IndexOf('\n')) >= 0)
                {
                    var line = buffered[..newlineIdx].Trim();
                    buffered = buffered[(newlineIdx + 1)..];

                    if (line.Length > 0)
                        ProcessLine(line);
                }

                buffer.Clear();
                buffer.Append(buffered);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "MT5 TCP reader loop error.");
        }
        finally
        {
            _connected = false;
            _log.LogInformation("MT5 TCP reader loop stopped.");
        }
    }

    // ── Message parser ────────────────────────────────────────────────────────

    // State machine for multi-line blocks
    private bool   _inHistBlock;
    private bool   _inBatchBlock;
    private string _histSymbol = "";
    private readonly List<OhlcBar> _currentHistBars = new();
    private readonly List<OhlcBar> _currentBatchBars = new();

    // Signal history complete
    private readonly TaskCompletionSource<bool> _historySentTcs = new();

    private void ProcessLine(string line)
    {
        var parts = line.Split(EaProtocol.PipeDelimiter);
        var verb  = parts[0];

        // ── HIST_START|SYM|count ──────────────────────────────────────────────
        if (verb == EaProtocol.HistStart)
        {
            _histSymbol = parts.Length > 1 ? parts[1] : "";
            _currentHistBars.Clear();
            _inHistBlock = true;
            return;
        }

        // ── HIST_END|SYM ──────────────────────────────────────────────────────
        if (verb == EaProtocol.HistEnd)
        {
            if (_histSymbol.Length > 0)
            {
                _history[_histSymbol] = new List<OhlcBar>(_currentHistBars);
                _log.LogDebug("History loaded: {Symbol} — {Count} bars", _histSymbol, _currentHistBars.Count);
            }
            _inHistBlock = false;
            _currentHistBars.Clear();
            return;
        }

        // ── BATCH_START|time ──────────────────────────────────────────────────
        if (verb == EaProtocol.BatchStart)
        {
            _currentBatchBars.Clear();
            _inBatchBlock = true;
            return;
        }

        // ── BATCH_END ─────────────────────────────────────────────────────────
        if (verb == EaProtocol.BatchEnd)
        {
            _inBatchBlock = false;
            DistributeBatch(_currentBatchBars);
            _currentBatchBars.Clear();

            // Mark history complete once the first batch arrives
            // (means all HIST blocks were already sent by the EA)
            _historySentTcs.TrySetResult(true);
            return;
        }

        // ── Data lines inside HIST block: time,O,H,L,C,V ─────────────────────
        if (_inHistBlock)
        {
            var bar = ParseOhlcLine(line, _histSymbol, isHistLine: true);
            if (bar is not null) _currentHistBars.Add(bar);
            return;
        }

        // ── Data lines inside BATCH block: SYM,time,O,H,L,C,V ───────────────
        if (_inBatchBlock)
        {
            var bar = ParseOhlcLine(line, symbol: null, isHistLine: false);
            if (bar is not null) _currentBatchBars.Add(bar);
            return;
        }

        // ── FILL|ticketRef|sym|dir|fillPrice|vol|slip|deal ───────────────────
        if (verb == EaProtocol.Fill)
        {
            ProcessFill(parts);
            return;
        }

        // ── ERR|ref|reason ────────────────────────────────────────────────────
        if (verb == EaProtocol.Error && parts.Length >= 3)
        {
            var tRef   = parts[1];
            var reason = parts[2];
            _log.LogWarning("EA ERR: ref={Ref} reason={Reason}", tRef, reason);

            if (_pendingOrders.TryRemove(tRef, out var tcs))
                tcs.TrySetResult(new OrderResult { Success = false, Error = reason });
            return;
        }

        // ── ACK|payload ───────────────────────────────────────────────────────
        if (verb == EaProtocol.Ack)
        {
            _log.LogInformation("EA ACK: {Payload}", parts.Length > 1 ? parts[1] : "");
            return;
        }

        // ── PONG ──────────────────────────────────────────────────────────────
        if (verb == EaProtocol.Pong)
        {
            _log.LogDebug("PONG received.");
            return;
        }

        _log.LogDebug("Unhandled EA line: {Line}", line);
    }

    // ── FILL processor ────────────────────────────────────────────────────────

    private void ProcessFill(string[] parts)
    {
        // FILL|ticketRef|sym|dir|fillPrice|vol|slippage|dealId
        // FILL|CLOSE|ticketRef|sym|fillPrice|vol|slippage|dealId
        bool isClose = parts.Length > 1 && parts[1] == "CLOSE";

        string tRef, positionId;
        decimal fillPrice, slippage;

        if (isClose && parts.Length >= 8)
        {
            tRef      = parts[2];
            var sym   = parts[3];
            fillPrice = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
            slippage  = decimal.Parse(parts[6], CultureInfo.InvariantCulture);
            positionId = $"{sym}:{tRef}";

            _log.LogInformation("FILL(CLOSE): ref={Ref} sym={Sym} price={Price}", tRef, sym, fillPrice);

            if (_pendingOrders.TryRemove($"CLOSE-{tRef}", out var ctcs))
                ctcs.TrySetResult(new OrderResult
                {
                    Success        = true,
                    PositionId     = positionId,
                    ExecutionPrice = fillPrice,
                    Slippage       = slippage
                });
        }
        else if (!isClose && parts.Length >= 8)
        {
            tRef      = parts[1];
            var sym   = parts[2];
            var dir   = parts[3];
            fillPrice = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
            slippage  = decimal.Parse(parts[6], CultureInfo.InvariantCulture);
            positionId = $"{sym}:{tRef}";

            _log.LogInformation("FILL: ref={Ref} sym={Sym} dir={Dir} price={Price}", tRef, sym, dir, fillPrice);

            if (_pendingOrders.TryRemove(tRef, out var tcs))
                tcs.TrySetResult(new OrderResult
                {
                    Success        = true,
                    PositionId     = positionId,
                    ExecutionPrice = fillPrice,
                    Slippage       = slippage
                });
        }
    }

    // ── OHLC bar parser ───────────────────────────────────────────────────────

    private OhlcBar? ParseOhlcLine(string line, string? symbol, bool isHistLine)
    {
        try
        {
            var cols = line.Split(',');

            if (isHistLine && cols.Length >= 6)
            {
                // HIST format: time,open,high,low,close,volume
                return new OhlcBar
                {
                    Symbol    = symbol ?? "",
                    Timeframe = "M1",
                    Time      = ParseMt5Time(cols[0]),
                    Open      = decimal.Parse(cols[1], CultureInfo.InvariantCulture),
                    High      = decimal.Parse(cols[2], CultureInfo.InvariantCulture),
                    Low       = decimal.Parse(cols[3], CultureInfo.InvariantCulture),
                    Close     = decimal.Parse(cols[4], CultureInfo.InvariantCulture),
                    Volume    = long.Parse(cols[5], CultureInfo.InvariantCulture)
                };
            }
            else if (!isHistLine && cols.Length >= 7)
            {
                // BATCH format: SYM,time,open,high,low,close,volume
                return new OhlcBar
                {
                    Symbol    = cols[0].Trim(),
                    Timeframe = "M1",
                    Time      = ParseMt5Time(cols[1]),
                    Open      = decimal.Parse(cols[2], CultureInfo.InvariantCulture),
                    High      = decimal.Parse(cols[3], CultureInfo.InvariantCulture),
                    Low       = decimal.Parse(cols[4], CultureInfo.InvariantCulture),
                    Close     = decimal.Parse(cols[5], CultureInfo.InvariantCulture),
                    Volume    = long.Parse(cols[6], CultureInfo.InvariantCulture)
                };
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug("Failed to parse OHLC line: {Line} — {Ex}", line, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Parses MT5 TimeToString output: "2026.01.15 09:30:00"
    /// </summary>
    private static DateTime ParseMt5Time(string s)
    {
        // MT5 format: "2026.01.15 09:30:00" — dots as date separators
        return DateTime.ParseExact(s.Trim(), "yyyy.MM.dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    // ── Batch distributor ─────────────────────────────────────────────────────

    private void DistributeBatch(List<OhlcBar> bars)
    {
        foreach (var bar in bars)
        {
            if (_ohlcSubscribers.TryGetValue(bar.Symbol, out var channels))
            {
                foreach (var ch in channels)
                    ch.Writer.TryWrite(bar);
            }
        }

        if (_log.IsEnabled(LogLevel.Debug))
            _log.LogDebug("BATCH distributed: {Count} bars", bars.Count);
    }

    // ── History wait ──────────────────────────────────────────────────────────

    private async Task WaitForHistoryAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tcs.CancelAfter(timeout);
        try
        {
            await _historySentTcs.Task.WaitAsync(tcs.Token);
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("History wait timed out after {Timeout}s. Proceeding anyway.", timeout.TotalSeconds);
        }
    }

    // ── Keepalive ─────────────────────────────────────────────────────────────

    private async Task KeepaliveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_pingInterval, ct);
            if (_connected)
            {
                try
                {
                    await SendRawAsync(EaProtocol.Ping + EaProtocol.MsgSeparator, ct);
                    _log.LogDebug("PING sent.");
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "PING failed — connection may be lost.");
                }
            }
        }
    }

    // ── Raw write ─────────────────────────────────────────────────────────────

    private async Task SendRawAsync(string message, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected to MT5 EA.");

        var bytes = Encoding.UTF8.GetBytes(message);
        await _writeLock.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Guards ────────────────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!_connected)
            throw new InvalidOperationException(
                "Mt5TcpBridgeAdapter is not connected. Call ConnectAsync() first.");
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_readerTask is not null)
            await _readerTask.ConfigureAwait(false);

        _stream?.Dispose();
        _tcp?.Dispose();
        _writeLock.Dispose();
        _cts?.Dispose();
    }
}
