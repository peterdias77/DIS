using DIS.Core.Enums;
using DIS.Core.Models;
using DIS.OutputLayer.Base;

namespace DIS.OutputLayer.Evaluators;

/// <summary>OUTPUT 1 — Trading Permission (States 1, 2)</summary>
public sealed class Output01_TradingPermission : OutputEvaluatorBase<TradingPermission>
{
    public override int    OutputId   => 1;
    public override string OutputName => "trading_permission";

    public override TradingPermission Evaluate(StateSnapshot s)
    {
        // TODO: Implement rule book. BREACH on either → BLOCK.
        //       WARNING on either → RESTRICT. Both NORMAL → ALLOW.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 2 — Exposure Control (States 3, 4)</summary>
public sealed class Output02_ExposureControl : OutputEvaluatorBase<ExposureControl>
{
    public override int    OutputId   => 2;
    public override string OutputName => "exposure_control";

    public override ExposureControl Evaluate(StateSnapshot s)
    {
        // TODO: COLLAPSE or CLUSTERED → LIMITED_EXPOSURE.
        //       ELEVATED + INTACT/WEAKENING → REDUCED_SIZE.
        //       ELEVATED + BROKEN → LIMITED_EXPOSURE.
        //       NORMAL + INTACT → FULL_SIZE. NORMAL + WEAKENING/BROKEN → REDUCED_SIZE.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 3 — Market Regime (States 7, 11, 34)</summary>
public sealed class Output03_MarketRegime : OutputEvaluatorBase<MarketRegime>
{
    public override int    OutputId   => 3;
    public override string OutputName => "market_regime";

    public override MarketRegime Evaluate(StateSnapshot s)
    {
        // TODO: Implement the full 45-combination rule table across
        //       structure_phase × range_migration × trend_phase.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 4 — Structural Condition (States 5, 6, 9, 8, 10)</summary>
public sealed class Output04_StructuralCondition : OutputEvaluatorBase<StructuralCondition>
{
    public override int    OutputId   => 4;
    public override string OutputName => "structural_condition";

    public override StructuralCondition Evaluate(StateSnapshot s)
    {
        // TODO: Implement 108-combination rule table.
        //       INVALID market_structure → always BROKEN.
        //       DECOUPLED drift → always BROKEN.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 5 — Directional Bias (States 23, 29, 32)</summary>
public sealed class Output05_DirectionalBias : OutputEvaluatorBase<DirectionalBias>
{
    public override int    OutputId   => 5;
    public override string OutputName => "directional_bias";

    public override DirectionalBias Evaluate(StateSnapshot s)
    {
        // TODO: 27-combination rule table across momentum × crowd × intermarket.
        //       ACCELERATING + most conditions → LONG.
        //       DECAYING + most conditions → SHORT.
        //       STABLE is mixed.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 6 — Market Strength (States 21, 24, 25)</summary>
public sealed class Output06_MarketStrength : OutputEvaluatorBase<MarketStrength>
{
    public override int    OutputId   => 6;
    public override string OutputName => "market_strength";

    public override MarketStrength Evaluate(StateSnapshot s)
    {
        // TODO: 27-combination rule table across participation × follow_through × volume.
        //       RISING + CONFIRMED + CONFIRMING/SPIKING → STRONG.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 7 — Volatility Environment (States 13, 14, 16, 17)</summary>
public sealed class Output07_VolatilityEnvironment : OutputEvaluatorBase<VolatilityEnvironment>
{
    public override int    OutputId   => 7;
    public override string OutputName => "volatility_environment";

    public override VolatilityEnvironment Evaluate(StateSnapshot s)
    {
        // TODO: IRREGULAR alignment or UNSTABLE trend → UNSTABLE (dominant).
        //       EXTREME regime + aligned → EXTREME.
        //       ELEVATED + contracting + aligned + symmetric → NORMAL.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 8 — Liquidity Environment (States 18, 15)</summary>
public sealed class Output08_LiquidityEnvironment : OutputEvaluatorBase<LiquidityEnvironment>
{
    public override int    OutputId   => 8;
    public override string OutputName => "liquidity_environment";

    public override LiquidityEnvironment Evaluate(StateSnapshot s)
    {
        // TODO: SHOCK + BROKEN → BROKEN. SHOCK + anything → UNSTABLE.
        //       AMPLIFIED + HEALTHY → THIN. STABLE + HEALTHY → HEALTHY.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 9 — Execution Quality (States 19, 20)</summary>
public sealed class Output09_ExecutionQuality : OutputEvaluatorBase<ExecutionQuality>
{
    public override int    OutputId   => 9;
    public override string OutputName => "execution_quality";

    public override ExecutionQuality Evaluate(StateSnapshot s)
    {
        // TODO: BROKEN spread → always POOR. SEVERE slippage → always POOR.
        //       NORMAL spread + ACCEPTABLE slippage → GOOD.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 10 — Risk Environment (States 28, 30, 27)</summary>
public sealed class Output10_RiskEnvironment : OutputEvaluatorBase<RiskEnvironment>
{
    public override int    OutputId   => 10;
    public override string OutputName => "risk_environment";

    public override RiskEnvironment Evaluate(StateSnapshot s)
    {
        // TODO: 16-combination rule table across session × news_proximity × event_importance.
        //       OFF_HOURS + NEAR_EVENT + HIGH → EXTREME.
        //       ASIA + CLEAR + LOW → LOW.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 11 — Market Behavior Type (States 33, 26, 31)</summary>
public sealed class Output11_MarketBehaviorType : OutputEvaluatorBase<MarketBehaviorType>
{
    public override int    OutputId   => 11;
    public override string OutputName => "market_behavior_type";

    public override MarketBehaviorType Evaluate(StateSnapshot s)
    {
        // TODO: 48-combination rule table across wick × follow_through × sweep.
        //       STOP_HUNT + REJECTED + any sweep → MANIPULATION.
        //       AGGRESSIVE + CONFIRMED + any sweep → EXPANSION.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 12 — Crowd Condition (State 22)</summary>
public sealed class Output12_CrowdCondition : OutputEvaluatorBase<CrowdCondition>
{
    public override int    OutputId   => 12;
    public override string OutputName => "crowd_condition";

    public override CrowdCondition Evaluate(StateSnapshot s)
    {
        // TODO: Direct pass-through from positioning_state with enum alignment.
        throw new NotImplementedException();
    }
}

/// <summary>OUTPUT 13 — Timeframe Consistency (State 12)</summary>
public sealed class Output13_TimeframeConsistency : OutputEvaluatorBase<TimeframeConsistency>
{
    public override int    OutputId   => 13;
    public override string OutputName => "timeframe_consistency";

    public override TimeframeConsistency Evaluate(StateSnapshot s)
    {
        // TODO: Direct pass-through from htf_structure_state with enum alignment.
        throw new NotImplementedException();
    }
}
