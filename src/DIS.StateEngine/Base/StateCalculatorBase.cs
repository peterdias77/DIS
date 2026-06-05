using DIS.Core.Interfaces;
using DIS.Core.Models;

namespace DIS.StateEngine.Base;

/// <summary>
/// Base class for all 34 DIS state calculators.
/// Each calculator is a self-contained unit with no side effects.
/// Subclasses implement Calculate() only.
/// </summary>
public abstract class StateCalculatorBase<TState> : IStateCalculator<TState>
    where TState : struct, Enum
{
    public abstract int    StateId   { get; }
    public abstract string StateName { get; }

    public abstract TState Calculate(MarketContext context);
}
