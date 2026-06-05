#property copyright "DIS"
#property version   "1.00"
#property strict

#define SOCKET_LIBRARY_LOGGING        // Enable socket error logging to MT5 journal
#include <tclib.mqh>

//--- Input Parameters
input ushort   InpServerPort      = 9000;          // TCP Server Port (DIS connects here)
input string   InpSymbolsFile     = "DIS_Symbols.csv"; // Symbols CSV file (in MQL5/Files)
input int      InpHistoryBars     = 200;            // History bars to send on startup
input int      InpTimerMs         = 250;            // Poll interval in milliseconds
input double   InpMaxSlippage     = 10;             // Max slippage in points
input bool     InpLocalhostOnly   = true;           // Accept connections from localhost only
input bool     InpVerboseLog      = true;           // Verbose logging to journal

//--- Constants
#define MAX_SYMBOLS       50
#define MSG_SEPARATOR     "\n"
#define BATCH_START       "BATCH_START"
#define BATCH_END         "BATCH_END"
#define HIST_START        "HIST_START"
#define HIST_END          "HIST_END"
#define CMD_ORDER         "ORDER"
#define CMD_CLOSE         "CLOSE"
#define CMD_PING          "PING"
#define CMD_PONG          "PONG"
#define CMD_FILL          "FILL"
#define CMD_ERROR         "ERR"
#define CMD_ACK           "ACK"

//--- Global State
ServerSocket*  g_server           = NULL;
ClientSocket*  g_client           = NULL;

string         g_symbols[];
int            g_symbolCount      = 0;
datetime       g_lastBarTime[];       // Last bar time per symbol (to detect new bar)
bool           g_histSent         = false;
int            g_reconnectDelay   = 0;
datetime       g_startTime;

//+------------------------------------------------------------------+
//| Expert initialization                                            |
//+------------------------------------------------------------------+
int OnInit()
{
   g_startTime = TimeCurrent();
   Log("=== DIS Bridge EA v1.0 Starting ===");
   Log("Port: " + IntegerToString(InpServerPort) + 
       " | History bars: " + IntegerToString(InpHistoryBars) +
       " | Localhost only: " + (InpLocalhostOnly ? "YES" : "NO"));

   //--- Load symbol list from CSV
   if (!LoadSymbols())
   {
      Alert("DIS_Bridge: Failed to load symbols from " + InpSymbolsFile + ". Check MQL5/Files folder.");
      return INIT_FAILED;
   }
   Log("Loaded " + IntegerToString(g_symbolCount) + " symbols.");

   //--- Initialise last-bar tracking array
   ArrayResize(g_lastBarTime, g_symbolCount);
   ArrayInitialize(g_lastBarTime, 0);

   //--- Start TCP server
   g_server = new ServerSocket(InpServerPort, InpLocalhostOnly);
   if (!g_server.Created())
   {
      int err = g_server.GetLastSocketError();
      Alert("DIS_Bridge: Failed to create server socket on port " +
            IntegerToString(InpServerPort) + ". WSA Error: " + IntegerToString(err));
      delete g_server;
      g_server = NULL;
      return INIT_FAILED;
   }
   Log("TCP Server listening on port " + IntegerToString(InpServerPort) + " — waiting for DIS...");

   //--- Timer drives the main loop
   EventSetMillisecondTimer(InpTimerMs);

   return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| Expert deinitialization                                          |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
   EventKillTimer();
   DisconnectClient("EA shutting down.");
   if (g_server != NULL) { delete g_server; g_server = NULL; }
   Log("=== DIS Bridge EA stopped. Reason: " + IntegerToString(reason) + " ===");
}

//+------------------------------------------------------------------+
//| Timer — main polling loop                                        |
//+------------------------------------------------------------------+
void OnTimer()
{
   //--- 1. Accept new connection if no client connected
   if (g_client == NULL)
   {
      TryAcceptClient();
      return; // Don't proceed until connected
   }

   //--- 2. Check client still alive
   if (!g_client.IsSocketConnected())
   {
      DisconnectClient("Client socket dropped.");
      return;
   }

   //--- 3. Send history on first connection (blocks briefly, only once)
   if (!g_histSent)
   {
      SendHistoryAll();
      g_histSent = true;
   }

   //--- 4. Check for new 1-min bar and send batch
   CheckAndSendBatch();

   //--- 5. Read any incoming commands from DIS
   ProcessIncoming();
}

//+------------------------------------------------------------------+
//| Load symbols from CSV file                                       |
//+------------------------------------------------------------------+
bool LoadSymbols()
{
   string path = InpSymbolsFile;
   int handle = FileOpen(path, FILE_READ | FILE_CSV | FILE_ANSI, ',');
   if (handle == INVALID_HANDLE)
   {
      Log("ERROR: Cannot open symbols file: " + path);
      return false;
   }

   ArrayResize(g_symbols, MAX_SYMBOLS);
   g_symbolCount = 0;

   bool firstRow = true;
   while (!FileIsEnding(handle) && g_symbolCount < MAX_SYMBOLS)
   {
      string sym    = FileReadString(handle); // Column 0: SYMBOL
      //string desc   = FileReadString(handle); // Column 1: DESCRIPTION
      //string aclass = FileReadString(handle); // Column 2: ASSET_CLASS

      // Skip header row
      //if (firstRow && sym == "SYMBOL") { firstRow = false; continue; }
      firstRow = false;

      StringTrimLeft(sym);
      StringTrimRight(sym);
      if (StringLen(sym) == 0) continue;

      // Validate symbol exists in MT5
      if (!SymbolSelect(sym, true))
      {
         Log("WARNING: Symbol not found in MT5 Market Watch — skipping: " + sym);
         continue;
      }

      g_symbols[g_symbolCount] = sym;
      g_symbolCount++;
      Log("  + Loaded: " + sym );
   }

   FileClose(handle);
   ArrayResize(g_symbols, g_symbolCount);
   return (g_symbolCount > 0);
}

//+------------------------------------------------------------------+
//| Try to accept an incoming connection from DIS                    |
//+------------------------------------------------------------------+
void TryAcceptClient()
{
   if (g_server == NULL) return;
   ClientSocket* newClient = g_server.Accept();
   if (newClient != NULL)
   {
      g_client   = newClient;
      g_histSent = false; // Trigger history send for new connection
      ArrayInitialize(g_lastBarTime, 0); // Reset bar tracking
      Log(">>> DIS Backend connected.");
      SendAck("CONNECTED|DIS_BRIDGE_v1.0|SYMBOLS=" + IntegerToString(g_symbolCount));
   }
}

//+------------------------------------------------------------------+
//| Disconnect and clean up client                                   |
//+------------------------------------------------------------------+
void DisconnectClient(string reason)
{
   if (g_client != NULL)
   {
      delete g_client;
      g_client   = NULL;
      g_histSent = false;
      Log("<<< DIS Backend disconnected. Reason: " + reason);
   }
}

//+------------------------------------------------------------------+
//| Send history for ALL symbols on connection                       |
//+------------------------------------------------------------------+
void SendHistoryAll()
{
   Log("Sending " + IntegerToString(InpHistoryBars) + " bars history for " +
       IntegerToString(g_symbolCount) + " symbols...");

   for (int i = 0; i < g_symbolCount; i++)
   {
      if (!g_client.IsSocketConnected()) { DisconnectClient("Dropped during history send."); return; }
      SendHistory(g_symbols[i]);
   }
   Log("History send complete.");
}

//+------------------------------------------------------------------+
//| Send bar history for one symbol                                  |
//+------------------------------------------------------------------+
void SendHistory(string sym)
{
   MqlRates rates[];
   int copied = CopyRates(sym, PERIOD_M1, 0, InpHistoryBars, rates);
   if (copied <= 0)
   {
      Log("WARNING: No history data for " + sym);
      return;
   }

   // Build the history block
   string msg = HIST_START + "|" + sym + "|" + IntegerToString(copied) + MSG_SEPARATOR;
   for (int i = 0; i < copied; i++)
   {
      msg += TimeToString(rates[i].time, TIME_DATE | TIME_MINUTES | TIME_SECONDS) + "," +
             DoubleToString(rates[i].open,  _Digits) + "," +
             DoubleToString(rates[i].high,  _Digits) + "," +
             DoubleToString(rates[i].low,   _Digits) + "," +
             DoubleToString(rates[i].close, _Digits) + "," +
             IntegerToString(rates[i].tick_volume) + MSG_SEPARATOR;
   }
   msg += HIST_END + "|" + sym + MSG_SEPARATOR;

   if (!g_client.Send(msg))
      DisconnectClient("Send failed during history: " + sym);
   else if (InpVerboseLog)
      Log("HIST sent: " + sym + " (" + IntegerToString(copied) + " bars)");
}

//+------------------------------------------------------------------+
//| Check all symbols for a new bar, send batch if any new bar found |
//+------------------------------------------------------------------+
void CheckAndSendBatch()
{
   // We key off the first symbol's bar time to decide when to send
   // A batch is sent when ANY symbol has a new bar (they're all on M1 so
   // they all tick at the same wall-clock second)
   bool newBarFound = false;
   for (int i = 0; i < g_symbolCount; i++)
   {
      datetime lastBar[1];
      if (CopyTime(g_symbols[i], PERIOD_M1, 0, 1, lastBar) <= 0) continue;
      if (lastBar[0] != g_lastBarTime[i])
      {
         newBarFound = true;
         break;
      }
   }
   if (!newBarFound) return;

   //--- Build full batch
   string batchTime = TimeToString(TimeCurrent(), TIME_DATE | TIME_MINUTES | TIME_SECONDS);
   string msg = BATCH_START + "|" + batchTime + MSG_SEPARATOR;
   int sentCount = 0;

   for (int i = 0; i < g_symbolCount; i++)
   {
      MqlRates rates[1];
      if (CopyRates(g_symbols[i], PERIOD_M1, 1, 1, rates) <= 0)
      {
         Log("WARNING: No M1 bar data for " + g_symbols[i]);
         continue;
      }

      datetime lastBar[1];
      CopyTime(g_symbols[i], PERIOD_M1, 0, 1, lastBar);
      g_lastBarTime[i] = lastBar[0];

      msg += g_symbols[i] + "," +
             TimeToString(rates[0].time, TIME_DATE | TIME_MINUTES | TIME_SECONDS) + "," +
             DoubleToString(rates[0].open,  _Digits) + "," +
             DoubleToString(rates[0].high,  _Digits) + "," +
             DoubleToString(rates[0].low,   _Digits) + "," +
             DoubleToString(rates[0].close, _Digits) + "," +
             IntegerToString(rates[0].tick_volume) + MSG_SEPARATOR;
      sentCount++;
   }

   msg += BATCH_END + MSG_SEPARATOR;

   if (!g_client.Send(msg))
      DisconnectClient("Send failed during batch.");
   else if (InpVerboseLog)
      Log("BATCH sent: " + batchTime + " (" + IntegerToString(sentCount) + " symbols)");
}

//+------------------------------------------------------------------+
//| Read and dispatch incoming commands from DIS                     |
//+------------------------------------------------------------------+
void ProcessIncoming()
{
   if (g_client == NULL) return;

   string raw = g_client.Receive(MSG_SEPARATOR);
   while (StringLen(raw) > 0)
   {
      StringTrimLeft(raw);
      StringTrimRight(raw);
      if (StringLen(raw) > 0)
      {
         if (InpVerboseLog) Log("CMD << " + raw);
         DispatchCommand(raw);
      }
      raw = g_client.Receive(MSG_SEPARATOR);
   }
}

//+------------------------------------------------------------------+
//| Dispatch a single command line from DIS                          |
//+------------------------------------------------------------------+
void DispatchCommand(string cmd)
{
   //--- PING → PONG (keepalive)
   if (cmd == CMD_PING)
   {
      g_client.Send(CMD_PONG + MSG_SEPARATOR);
      return;
   }

   //--- Split by pipe delimiter
   string parts[];
   int count = StringSplit(cmd, '|', parts);
   if (count < 1) return;

   string verb = parts[0];

   //--- ORDER|<ticket>|<sym>|<BUY/SELL>|<vol>|<sl>|<tp>
   if (verb == CMD_ORDER && count >= 7)
   {
      string ticket_ref = parts[1];
      string sym        = parts[2];
      string direction  = parts[3];
      double vol        = StringToDouble(parts[4]);
      double sl         = StringToDouble(parts[5]);
      double tp         = StringToDouble(parts[6]);
      ExecuteOrder(ticket_ref, sym, direction, vol, sl, tp);
      return;
   }

   //--- CLOSE|<ticket>|<sym>|<vol>
   if (verb == CMD_CLOSE && count >= 4)
   {
      string ticket_ref = parts[1];
      string sym        = parts[2];
      double vol        = StringToDouble(parts[3]);
      CloseOrder(ticket_ref, sym, vol);
      return;
   }

   //--- Unknown command
   Log("WARNING: Unknown command: " + cmd);
   SendError(cmd, "UNKNOWN_COMMAND");
}

//+------------------------------------------------------------------+
//| Execute a market order                                           |
//+------------------------------------------------------------------+
void ExecuteOrder(string ticketRef, string sym, string direction, double vol, double sl, double tp)
{
   MqlTradeRequest req = {};
   MqlTradeResult  res = {};

   req.action    = TRADE_ACTION_DEAL;
   req.symbol    = sym;
   req.volume    = vol;
   req.type      = (direction == "BUY") ? ORDER_TYPE_BUY : ORDER_TYPE_SELL;
   req.deviation = (ulong)InpMaxSlippage;
   req.magic     = 20260601; // DIS project magic number
   req.comment   = "DIS|" + ticketRef;
   req.type_filling = ORDER_FILLING_IOC;

   // Price
   if (req.type == ORDER_TYPE_BUY)
      req.price = SymbolInfoDouble(sym, SYMBOL_ASK);
   else
      req.price = SymbolInfoDouble(sym, SYMBOL_BID);

   // SL/TP (0 = no SL or TP)
   req.sl = sl;
   req.tp = tp;

   Log("ORDER >> " + ticketRef + " " + direction + " " + sym +
       " vol=" + DoubleToString(vol, 2) +
       " sl=" + DoubleToString(sl, _Digits) +
       " tp=" + DoubleToString(tp, _Digits));

   bool sent = OrderSend(req, res);

   if (sent && res.retcode == TRADE_RETCODE_DONE)
   {
      // Fill confirmation
      double fillPrice = res.price;
      double slippage  = MathAbs(fillPrice - req.price) / SymbolInfoDouble(sym, SYMBOL_POINT);

      string fill = CMD_FILL + "|" + ticketRef + "|" + sym + "|" + direction + "|" +
                    DoubleToString(fillPrice, _Digits) + "|" +
                    DoubleToString(res.volume, 2) + "|" +
                    DoubleToString(slippage, 1) + "|" +
                    IntegerToString((int)res.deal) + MSG_SEPARATOR;

      if (!g_client.Send(fill))
         DisconnectClient("Send failed on FILL confirmation.");
      else
         Log("FILL << " + fill);
   }
   else
   {
      string errMsg = "RETCODE=" + IntegerToString(res.retcode) +
                      " " + res.comment;
      Log("ORDER FAILED: " + ticketRef + " — " + errMsg);
      SendError(ticketRef, errMsg);
   }
}

//+------------------------------------------------------------------+
//| Close (partial or full) an open position by symbol               |
//+------------------------------------------------------------------+
void CloseOrder(string ticketRef, string sym, double vol)
{
   if (!PositionSelect(sym))
   {
      Log("CLOSE FAILED: No open position for " + sym);
      SendError(ticketRef, "NO_POSITION|" + sym);
      return;
   }

   MqlTradeRequest req = {};
   MqlTradeResult  res = {};

   long   posType = PositionGetInteger(POSITION_TYPE);
   double posVol  = PositionGetDouble(POSITION_VOLUME);

   req.action    = TRADE_ACTION_DEAL;
   req.symbol    = sym;
   req.volume    = (vol <= 0 || vol >= posVol) ? posVol : vol; // 0 = close all
   req.deviation = (ulong)InpMaxSlippage;
   req.magic     = 20260601;
   req.comment   = "DIS|CLOSE|" + ticketRef;
   req.type_filling = ORDER_FILLING_IOC;
   req.position  = PositionGetInteger(POSITION_TICKET);

   if (posType == POSITION_TYPE_BUY)
   {
      req.type  = ORDER_TYPE_SELL;
      req.price = SymbolInfoDouble(sym, SYMBOL_BID);
   }
   else
   {
      req.type  = ORDER_TYPE_BUY;
      req.price = SymbolInfoDouble(sym, SYMBOL_ASK);
   }

   Log("CLOSE >> " + ticketRef + " " + sym + " vol=" + DoubleToString(req.volume, 2));

   bool sent = OrderSend(req, res);

   if (sent && res.retcode == TRADE_RETCODE_DONE)
   {
      double fillPrice = res.price;
      double slippage  = MathAbs(fillPrice - req.price) / SymbolInfoDouble(sym, SYMBOL_POINT);

      string fill = CMD_FILL + "|CLOSE|" + ticketRef + "|" + sym + "|" +
                    DoubleToString(fillPrice, _Digits) + "|" +
                    DoubleToString(res.volume, 2) + "|" +
                    DoubleToString(slippage, 1) + "|" +
                    IntegerToString((int)res.deal) + MSG_SEPARATOR;

      if (!g_client.Send(fill))
         DisconnectClient("Send failed on CLOSE FILL.");
      else
         Log("FILL (CLOSE) << " + fill);
   }
   else
   {
      string errMsg = "RETCODE=" + IntegerToString(res.retcode) + " " + res.comment;
      Log("CLOSE FAILED: " + ticketRef + " — " + errMsg);
      SendError(ticketRef, "CLOSE_FAILED|" + errMsg);
   }
}

//+------------------------------------------------------------------+
//| Helper: send ACK message                                         |
//+------------------------------------------------------------------+
void SendAck(string payload)
{
   if (g_client == NULL) return;
   string msg = CMD_ACK + "|" + payload + MSG_SEPARATOR;
   if (!g_client.Send(msg))
      DisconnectClient("Send failed on ACK.");
}

//+------------------------------------------------------------------+
//| Helper: send ERR message                                         |
//+------------------------------------------------------------------+
void SendError(string ref, string reason)
{
   if (g_client == NULL) return;
   string msg = CMD_ERROR + "|" + ref + "|" + reason + MSG_SEPARATOR;
   if (!g_client.Send(msg))
      DisconnectClient("Send failed on ERR.");
}

//+------------------------------------------------------------------+
//| Helper: Log with timestamp                                       |
//+------------------------------------------------------------------+
void Log(string text)
{
   Print("[DIS_Bridge] " + text);
}

//+------------------------------------------------------------------+
//| OnTick — not used, timer-driven instead                          |
//+------------------------------------------------------------------+
void OnTick() { }
