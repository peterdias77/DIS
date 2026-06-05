namespace DIS.Execution.Adapters;

/// <summary>
/// Protocol constants matching DIS.mq5 exactly.
/// Any change here must be mirrored in the EA source.
/// </summary>
internal static class EaProtocol
{
    // Delimiters
    public const char   PipeDelimiter = '|';
    public const string MsgSeparator  = "\n";

    // EA → C# (data)
    public const string BatchStart = "BATCH_START";
    public const string BatchEnd   = "BATCH_END";
    public const string HistStart  = "HIST_START";
    public const string HistEnd    = "HIST_END";
    public const string Fill       = "FILL";
    public const string Error      = "ERR";
    public const string Ack        = "ACK";
    public const string Pong       = "PONG";

    // C# → EA (commands)
    public const string Order      = "ORDER";
    public const string Close      = "CLOSE";
    public const string Ping       = "PING";

    // ORDER command format:  ORDER|ticketRef|sym|BUY/SELL|vol|sl|tp\n
    // CLOSE command format:  CLOSE|ticketRef|sym|vol\n
    // FILL response format:  FILL|ticketRef|sym|dir|fillPrice|vol|slippage|dealId\n
    // FILL(CLOSE) format:    FILL|CLOSE|ticketRef|sym|fillPrice|vol|slippage|dealId\n
    // BATCH line format:     SYM,time,open,high,low,close,volume\n
    // HIST line format:      time,open,high,low,close,volume\n
}
