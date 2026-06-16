# DIS — Solution Architecture Reference

## Solution Structure

```
DIS/
├── DIS.sln
├── src/
│   ├── DIS.Core            ← Domain anchor. No upstream dependencies.
│   ├── DIS.DataFeed        ← Broker-agnostic data abstraction. Refs: Core
│   ├── DIS.StateEngine     ← 34 state calculators. Refs: Core, DataFeed
│   ├── DIS.OutputLayer     ← 13 output evaluators. Refs: Core, StateEngine
│   ├── DIS.Orchestration   ← 5 orchestration groups. Refs: Core, OutputLayer
│   ├── DIS.EntryEngine     ← Entry signals + price levels. Refs: Core, Orchestration, StateEngine
│   ├── DIS.TradeControl    ← Cycle ID, slot mgmt, trade control. Refs: Core, Orchestration, EntryEngine
│   ├── DIS.RiskManager     ← Position size, SL, risk %. Refs: Core, Orchestration, StateEngine
│   ├── DIS.ExitManager     ← TP calc, exit ladder. Refs: Core, Orchestration, RiskManager
│   ├── DIS.Logger          ← Structured JSON logger. Refs: Core only
│   ├── DIS.Execution       ← MT5 adapter (ZeroMQ). Refs: Core, DataFeed, Logger
│   ├── DIS.Host            ← Worker Service runtime. Refs: everything
│   └── DIS.Dashboard       ← WPF + Blazor Server UI. Refs: Core, Logger
└── tests/
    └── DIS.Tests           ← xUnit. Refs: all src except Host and Dashboard
```

## Dependency Graph

```
DIS.Core
  └── DIS.DataFeed
        └── DIS.StateEngine
              └── DIS.OutputLayer
                    └── DIS.Orchestration
                          ├── DIS.EntryEngine ──────────────┐
                          ├── DIS.TradeControl ─────────────┤
                          ├── DIS.RiskManager ──────────────┤
                          └── DIS.ExitManager ──────────────┤
                                                             ▼
DIS.Logger ◄──────────────────────────────── DIS.Execution ──► DIS.Host
                                                             ▲
DIS.Dashboard (reads Core + Logger only) ────────────────────┘
```

## Build Order

1. DIS.Core
2. DIS.DataFeed
3. DIS.Logger
4. DIS.StateEngine
5. DIS.OutputLayer
6. DIS.Orchestration
7. DIS.EntryEngine
8. DIS.RiskManager
9. DIS.ExitManager
10. DIS.TradeControl
11. DIS.Execution
12. DIS.Host
13. DIS.Dashboard
14. DIS.Tests

## Key Architectural Rules

- **DIS.Core** has no project references. It is the only source of enums, interfaces, and domain models.
- **State calculators** are stateless. They receive a `MarketContext`, return a typed enum value, no side effects.
- **Output evaluators** are pure functions. In → Out. No state held.
- **DIS.Logger** is the only write path for observability. All layers call it; nothing else produces output files.
- **DIS.Dashboard** has NO write path into execution. Read-only via log store and state bus.
- **DIS.Execution** owns both the MT5 execution adapter (PUSH/PULL) and the MT5 data feed adapter (PUB/SUB).

## ZeroMQ Channel Map (MT5 ↔ C#)

| Channel  | Pattern   | Port  | Direction    | Purpose                         |
|----------|-----------|-------|--------------|---------------------------------|
| Market data | PUB/SUB | 5557  | MT5 → C#    | Tick, OHLC, order book stream  |
| Execution   | PUSH/PULL| 5555  | C# → MT5    | Order commands                  |
| History     | REQ/REP  | 5556  | C# → MT5    | Historical bar requests (warmup)|

## Early Verification Checklist

Before implementing state calculators, verify:
- [ ] Broker allows loading `libzmq.dll` inside MT5 sandbox
- [ ] MT5 EA can be deployed as a Service (background process, no chart required)
- [ ] Chosen data provider supports all 20 asset symbols with required data types

## State → Output → Orchestration Map

| Output | States consumed         |
|--------|-------------------------|
| 1      | 1, 2                    |
| 2      | 3, 4                    |
| 3      | 7, 11, 34               |
| 4      | 5, 6, 9, 8, 10          |
| 5      | 23, 29, 32              |
| 6      | 21, 24, 25              |
| 7      | 13, 14, 16, 17          |
| 8      | 18, 15                  |
| 9      | 19, 20                  |
| 10     | 28, 30, 27              |
| 11     | 33, 26, 31              |
| 12     | 22                      |
| 13     | 12                      |

| Group | Inputs (Outputs)    | Produces              |
|-------|---------------------|-----------------------|
| G1    | Out 1, 2            | TradingPermission     |
| G2    | Out 7, 8, 9, 10     | MarketPermission      |
| G3    | Out 3, 11           | StrategyOutput        |
| G4    | Out 5, 13           | TradeDirection        |
| G5    | Out 6, 12, 4        | ConfidenceLevel       |
