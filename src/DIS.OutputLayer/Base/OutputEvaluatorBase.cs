using DIS.Core.Enums;
using DIS.Core.Interfaces;
using DIS.Core.Models;

namespace DIS.OutputLayer.Base;

public abstract class OutputEvaluatorBase<TOutput> : IOutputEvaluator<TOutput>
    where TOutput : struct, Enum
{
    public abstract int     OutputId   { get; }
    public abstract string  OutputName { get; }
    public abstract TOutput Evaluate(StateSnapshot states);
}
