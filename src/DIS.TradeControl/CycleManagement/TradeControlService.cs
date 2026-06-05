using DIS.Core.Enums;
using DIS.Core.Models;

namespace DIS.TradeControl.CycleManagement;

/// <summary>
/// Derives and tracks the global Cycle ID from the combination of
/// structure_phase_state, htf_structure_state, and market_regime.
/// A new cycle is created whenever any of these three states changes.
/// The cycle ID is global across all assets.
/// </summary>
public sealed class CycleIdEngine
{
    private int    _cycleId          = 1;
    private (StructurePhaseState Phase, HtfStructureState Htf, MarketRegime Regime)? _previousSignature;

    public int CurrentCycleId => _cycleId;

    /// <summary>
    /// Evaluates whether the state signature has changed and increments the cycle ID if so.
    /// </summary>
    public int Update(StructurePhaseState phase, HtfStructureState htf, MarketRegime regime)
    {
        var current = (phase, htf, regime);
        if (_previousSignature is null || _previousSignature.Value != current)
        {
            _cycleId++;
            _previousSignature = current;
        }
        return _cycleId;
    }
}

/// <summary>
/// Manages rank slot allocation within a strategy group.
/// Enforces: one trade per rank, maximum 5 trades per group,
/// gap-filling execution (lowest unoccupied rank wins).
/// </summary>
public sealed class GroupSlotManager
{
    private readonly HashSet<int> _activeRanks = new();

    public IReadOnlySet<int> ActiveRanks => _activeRanks;

    /// <summary>Returns the lowest unoccupied rank (1–5), or null if the group is full.</summary>
    public int? NextAvailableRank()
    {
        for (int rank = 1; rank <= 5; rank++)
            if (!_activeRanks.Contains(rank))
                return rank;
        return null;
    }

    public bool TryOccupy(int rank)
    {
        if (_activeRanks.Contains(rank)) return false;
        _activeRanks.Add(rank);
        return true;
    }

    public void Release(int rank) => _activeRanks.Remove(rank);
}

/// <summary>
/// Enforces the two trade-control rules across all assets:
///   Rule 1 — Only one active trade per asset.
///   Rule 2 — Only one trade per strategy per cycle (global, across all assets).
/// </summary>
public sealed class TradeControlService
{
    // asset symbol → active trade exists
    private readonly Dictionary<string, bool> _assetActiveTrades = new();

    // (strategy, cycleId) → executed
    private readonly HashSet<(StrategyOutput Strategy, int CycleId)> _executedStrategyCycles = new();

    public bool IsEntryAllowed(string symbol, StrategyOutput strategy, int cycleId)
    {
        // Rule 1
        if (_assetActiveTrades.TryGetValue(symbol, out var active) && active)
            return false;

        // Rule 2
        if (_executedStrategyCycles.Contains((strategy, cycleId)))
            return false;

        return true;
    }

    public void RegisterTrade(string symbol, StrategyOutput strategy, int cycleId)
    {
        _assetActiveTrades[symbol] = true;
        _executedStrategyCycles.Add((strategy, cycleId));
    }

    public void ReleaseTrade(string symbol)
    {
        _assetActiveTrades[symbol] = false;
    }
}
