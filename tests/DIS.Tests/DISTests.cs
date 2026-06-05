using DIS.Core.Enums;
using DIS.Core.Models;
using DIS.ExitManager;
using DIS.RiskManager;
using DIS.TradeControl.CycleManagement;
using FluentAssertions;
using Xunit;

namespace DIS.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// STATE ENGINE TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class State01_PortfolioDrawdownTests
{
    // TODO: Test normal drawdown below 3% → NORMAL
    // TODO: Test drawdown at 3.0% → WARNING (with 2-confirmation filter)
    // TODO: Test drawdown at 7.0% → BREACH
    // TODO: Test drawdown at 10.0% → triggers kill switch
    // TODO: Test peak confirmation logic (3 confirmations, 0.5% stability)
    // TODO: Test hysteresis: WARNING reverts only when drawdown ≤ 2.5%
    [Fact(Skip = "Not yet implemented")]
    public void Drawdown_Below3Pct_ReturnsNormal() { }

    [Fact(Skip = "Not yet implemented")]
    public void Drawdown_At7Pct_ReturnsBreach() { }

    [Fact(Skip = "Not yet implemented")]
    public void Drawdown_At10Pct_TriggersKillSwitch() { }
}

public class State05_MarketStructureTests
{
    // TODO: Test swing detection with ATR magnitude filter (≥0.5×ATR accepted)
    // TODO: Test swing below 0.5×ATR → rejected (not a confirmed swing)
    // TODO: Test uptrend: HL → HH → HL sequence → VALID
    // TODO: Test expansion pattern (HH → LL) → INVALID
    // TODO: Test >2 violations in last 10 swings → INVALID
    // TODO: Test range detection within 0.3×ATR tolerance → VALID
    [Fact(Skip = "Not yet implemented")]
    public void ValidUptrend_ReturnsValid() { }

    [Fact(Skip = "Not yet implemented")]
    public void ExpansionPattern_ReturnsInvalid() { }
}

public class State07_StructurePhaseTests
{
    // TODO: Test RANGE_BOUND takes priority when directional_dominance is false
    // TODO: Test EXHAUSTED when momentum_decay is true
    // TODO: Test EXTENDED: progression ≥4, expansion ≥1.0, age ≥5
    // TODO: Test ESTABLISHED: progression ≥3, CLEAN quality, no decay
    // TODO: Test EMERGING: progression == 2, age ≤3
    // TODO: Test parabolic acceleration (expansion_ratio > 1.5) fast-tracks EXTENDED
    [Fact(Skip = "Not yet implemented")]
    public void RangeBound_HasHighestPriority_WhenDominanceFalse() { }

    [Fact(Skip = "Not yet implemented")]
    public void Parabolic_Expansion_FastTracksExtended() { }
}

// ─────────────────────────────────────────────────────────────────────────────
// OUTPUT LAYER TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class Output01_TradingPermissionTests
{
    // TODO: Test all 9 combinations of drawdown × overtrading
    // TODO: Verify BREACH dominates regardless of other state
    // TODO: Verify safety fallback always returns BLOCK
    [Fact(Skip = "Not yet implemented")]
    public void BothNormal_ReturnsAllow() { }

    [Fact(Skip = "Not yet implemented")]
    public void DrawdownBreach_ReturnsBlock_RegardlessOfOvertrading() { }

    [Fact(Skip = "Not yet implemented")]
    public void OvertradingBreach_ReturnsBlock_RegardlessOfDrawdown() { }
}

public class Output03_MarketRegimeTests
{
    // TODO: Test ESTABLISHED + STABLE_RANGE + DEVELOPMENT → TREND
    // TODO: Test EXHAUSTED + any + any → STRESS_DISORDER
    // TODO: Test RANGE_BOUND + BROKEN_RANGE + any → STRESS_DISORDER
    // TODO: Test EMERGING + MIGRATING_RANGE + INITIATION → EMERGING_TREND
    [Fact(Skip = "Not yet implemented")]
    public void Established_StableRange_Development_ReturnsTrend() { }

    [Fact(Skip = "Not yet implemented")]
    public void Exhausted_Always_ReturnsStressDisorder() { }
}

// ─────────────────────────────────────────────────────────────────────────────
// ORCHESTRATION TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class Group3_MarketTypeTests
{
    // TODO: Test TREND + NORMAL → TREND_FOLLOWING
    // TODO: Test CHOPPY_RANGE + ABSORPTION → RANGE_TRADING
    // TODO: Test TRANSITIONAL + EXPANSION → BREAKOUT
    // TODO: Test any + MANIPULATION → NO_TRADE
    // TODO: Test STRESS_DISORDER + any → NO_TRADE
    [Fact(Skip = "Not yet implemented")]
    public void Trend_Normal_ReturnsTrendFollowing() { }

    [Fact(Skip = "Not yet implemented")]
    public void Manipulation_Always_ReturnsNoTrade() { }
}

public class Group5_ConfidenceTests
{
    // TODO: STRONG + CLEAN + BALANCED → HIGH
    // TODO: MODERATE + CLEAN + BALANCED → MEDIUM
    // TODO: BROKEN structure → NO_TRADE regardless of other inputs
    // TODO: EXTREME crowd → NO_TRADE regardless of other inputs
    // TODO: WEAK strength → LOW regardless of other inputs
    [Fact(Skip = "Not yet implemented")]
    public void Strong_Clean_Balanced_ReturnsHigh() { }

    [Fact(Skip = "Not yet implemented")]
    public void BrokenStructure_ReturnsNoTrade() { }

    [Fact(Skip = "Not yet implemented")]
    public void ExtremeCrowd_ReturnsNoTrade() { }
}

// ─────────────────────────────────────────────────────────────────────────────
// TRADE CONTROL TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class CycleIdEngineTests
{
    [Fact]
    public void CycleId_Increments_WhenSignatureChanges()
    {
        var engine = new CycleIdEngine();
        var id1 = engine.Update(StructurePhaseState.Established, HtfStructureState.Aligned,    MarketRegime.Trend);
        var id2 = engine.Update(StructurePhaseState.Established, HtfStructureState.Aligned,    MarketRegime.Trend);      // same → no change
        var id3 = engine.Update(StructurePhaseState.Exhausted,   HtfStructureState.Conflicted, MarketRegime.LateTrend);  // changed

        id2.Should().Be(id1);
        id3.Should().BeGreaterThan(id2);
    }

    [Fact]
    public void CycleId_SameSignatureReappearing_IsNewCycle()
    {
        var engine = new CycleIdEngine();
        var id1 = engine.Update(StructurePhaseState.Established, HtfStructureState.Aligned, MarketRegime.Trend);
        var id2 = engine.Update(StructurePhaseState.Exhausted,   HtfStructureState.Aligned, MarketRegime.Trend);  // change
        var id3 = engine.Update(StructurePhaseState.Established, HtfStructureState.Aligned, MarketRegime.Trend);  // back to original sig → still new cycle

        id3.Should().BeGreaterThan(id2);
    }
}

public class GroupSlotManagerTests
{
    [Fact]
    public void NextAvailableRank_IsOne_WhenEmpty()
    {
        var mgr = new GroupSlotManager();
        mgr.NextAvailableRank().Should().Be(1);
    }

    [Fact]
    public void NextAvailableRank_SkipsOccupied_FillsGap()
    {
        var mgr = new GroupSlotManager();
        mgr.TryOccupy(1);
        mgr.TryOccupy(2);
        mgr.TryOccupy(4);

        mgr.NextAvailableRank().Should().Be(3);
    }

    [Fact]
    public void NextAvailableRank_IsNull_WhenFull()
    {
        var mgr = new GroupSlotManager();
        for (int i = 1; i <= 5; i++) mgr.TryOccupy(i);

        mgr.NextAvailableRank().Should().BeNull();
    }

    [Fact]
    public void Release_MakesRankAvailableAgain()
    {
        var mgr = new GroupSlotManager();
        for (int i = 1; i <= 5; i++) mgr.TryOccupy(i);
        mgr.Release(3);

        mgr.NextAvailableRank().Should().Be(3);
    }
}

public class TradeControlServiceTests
{
    [Fact]
    public void BlocksDuplicateTrade_SameAsset()
    {
        var svc = new TradeControlService();
        svc.RegisterTrade("XAUUSD", StrategyOutput.TrendFollowing, cycleId: 1);

        svc.IsEntryAllowed("XAUUSD", StrategyOutput.TrendFollowing, cycleId: 1)
           .Should().BeFalse();
    }

    [Fact]
    public void BlocksDuplicateTrade_SameStrategySameCycle_DifferentAsset()
    {
        var svc = new TradeControlService();
        svc.RegisterTrade("XAUUSD", StrategyOutput.TrendFollowing, cycleId: 2);

        svc.IsEntryAllowed("BTCUSD", StrategyOutput.TrendFollowing, cycleId: 2)
           .Should().BeFalse();
    }

    [Fact]
    public void AllowsTrade_DifferentStrategy_SameCycle()
    {
        var svc = new TradeControlService();
        svc.RegisterTrade("XAUUSD", StrategyOutput.TrendFollowing, cycleId: 1);

        svc.IsEntryAllowed("ETHUSD", StrategyOutput.Breakout, cycleId: 1)
           .Should().BeTrue();
    }

    [Fact]
    public void AllowsTrade_SameStrategy_NewCycle()
    {
        var svc = new TradeControlService();
        svc.RegisterTrade("XAUUSD", StrategyOutput.TrendFollowing, cycleId: 1);

        svc.IsEntryAllowed("BTCUSD", StrategyOutput.TrendFollowing, cycleId: 2)
           .Should().BeTrue();
    }

    [Fact]
    public void AllowsTrade_AfterRelease_SameAsset()
    {
        var svc = new TradeControlService();
        svc.RegisterTrade("XAUUSD", StrategyOutput.TrendFollowing, cycleId: 1);
        svc.ReleaseTrade("XAUUSD");

        svc.IsEntryAllowed("XAUUSD", StrategyOutput.Breakout, cycleId: 3)
           .Should().BeTrue();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RISK MANAGER TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class PositionSizerTests
{
    private readonly PositionSizer _sizer = new();

    [Fact]
    public void RiskPercent_Full_Is0Point5Pct()
    {
        _sizer.GetRiskPercent(EntrySize.Full).Should().Be(0.005m);
    }

    [Fact]
    public void RiskPercent_Half_Is0Point3Pct()
    {
        _sizer.GetRiskPercent(EntrySize.Half).Should().Be(0.003m);
    }

    [Fact]
    public void RiskPercent_Quarter_Is0Point2Pct()
    {
        _sizer.GetRiskPercent(EntrySize.Quarter).Should().Be(0.002m);
    }

    [Fact]
    public void PositionSize_Formula_IsCorrect()
    {
        // Capital = 10,000 | Risk = 0.5% = 50 | SL distance = 2.0
        // Expected position size = 50 / 2 = 25
        var size = _sizer.CalculatePositionSize(
            capital:    10_000m,
            size:       EntrySize.Full,
            entryPrice: 2350m,
            slPrice:    2348m);

        size.Should().Be(25m);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// EXIT MANAGER TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class ExitLadderTests
{
    [Theory]
    [InlineData(PositionState.Full,    PositionState.Half)]
    [InlineData(PositionState.Half,    PositionState.Quarter)]
    [InlineData(PositionState.Quarter, PositionState.Minimal)]
    [InlineData(PositionState.Minimal, PositionState.Closed)]
    [InlineData(PositionState.Closed,  PositionState.Closed)]
    public void ReduceOneLevel_StepsDownCorrectly(PositionState input, PositionState expected)
    {
        ExitLadderManager.ReduceOneLevel(input).Should().Be(expected);
    }

    [Fact]
    public void InitialState_MapsEntrySize_Correctly()
    {
        ExitLadderManager.InitialState(EntrySize.Full).Should().Be(PositionState.Full);
        ExitLadderManager.InitialState(EntrySize.Half).Should().Be(PositionState.Half);
        ExitLadderManager.InitialState(EntrySize.Quarter).Should().Be(PositionState.Quarter);
        ExitLadderManager.InitialState(EntrySize.NoTrade).Should().Be(PositionState.Closed);
    }
}

public class TakeProfitCalculatorTests
{
    private readonly TakeProfitCalculator _calc = new();

    [Fact]
    public void LongTrade_TP_Is4xSlDistance_AboveEntry()
    {
        // Entry = 2350, SL = 2348 → SL distance = 2 → TP = 2350 + 8 = 2358
        var tp = _calc.Calculate(TradeDirection.Long, entryPrice: 2350m, stopLossPrice: 2348m);
        tp.Should().Be(2358m);
    }

    [Fact]
    public void ShortTrade_TP_Is4xSlDistance_BelowEntry()
    {
        // Entry = 2350, SL = 2352 → SL distance = 2 → TP = 2350 - 8 = 2342
        var tp = _calc.Calculate(TradeDirection.Short, entryPrice: 2350m, stopLossPrice: 2352m);
        tp.Should().Be(2342m);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ASSET REGISTRY TESTS
// ─────────────────────────────────────────────────────────────────────────────

public class AssetRegistryTests
{
    [Fact]
    public void Registry_Contains_Exactly20Assets()
    {
        Core.Models.AssetRegistry.All.Should().HaveCount(20);
    }

    [Fact]
    public void EachGroup_Contains_Exactly5Assets()
    {
        foreach (var group in Enum.GetValues<DIS.Core.Enums.StrategyGroup>())
        {
            Core.Models.AssetRegistry.All
                .Where(a => a.Group == group)
                .Should().HaveCount(5, because: $"group {group} must have exactly 5 assets");
        }
    }

    [Fact]
    public void EachGroup_HasExactlyOneAssetPerRank()
    {
        foreach (var group in Enum.GetValues<DIS.Core.Enums.StrategyGroup>())
        {
            var ranks = Core.Models.AssetRegistry.All
                .Where(a => a.Group == group)
                .Select(a => (int)a.Rank)
                .OrderBy(r => r)
                .ToList();

            ranks.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 },
                because: $"group {group} must have one asset per rank 1–5");
        }
    }

    [Fact]
    public void Find_ReturnsCorrectAsset_CaseInsensitive()
    {
        var gold = Core.Models.AssetRegistry.Find("xauusd");
        gold.Should().NotBeNull();
        gold!.Group.Should().Be(DIS.Core.Enums.StrategyGroup.TrendFollowing);
        gold.Rank.Should().Be(DIS.Core.Enums.AssetRank.Rank1);
    }
}
