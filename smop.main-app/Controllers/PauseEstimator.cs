using Smop.MainApp.Logging;
using Smop.OdorDisplay;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.MainApp.Controllers;

internal class PauseEstimator
{
    public double MinSaturationDuration { get; set; } = 8;  // 3.38
    public double SaturationGain { get; set; } = 8;         // 28.53
    public double SaturationExpGain { get; set; } = -0.07;  // -0.2752

    public double MinCleanupDuration { get; set; } = 2.5;
    public double CleanupGain { get; set; } = 0.8;
    public double CleanupExpGain { get; set; } = 0.03;

    public PauseEstimator()
    {
        var settings = Properties.Settings.Default;

        MinSaturationDuration = settings.PauseEstimator_MinSaturationDuration;
        SaturationGain = settings.PauseEstimator_SaturationGain;
        SaturationExpGain = settings.PauseEstimator_SaturationExpGain;
        MinCleanupDuration = settings.PauseEstimator_MinCleanupDuration;
        CleanupGain = settings.PauseEstimator_CleanupGain;
        CleanupExpGain = settings.PauseEstimator_CleanupExpGain;
    }

    public void Save()
    {
        var settings = Properties.Settings.Default;

        settings.PauseEstimator_MinSaturationDuration = MinSaturationDuration;
        settings.PauseEstimator_SaturationGain = SaturationGain;
        settings.PauseEstimator_SaturationExpGain = SaturationExpGain;
        settings.PauseEstimator_MinCleanupDuration = MinCleanupDuration;
        settings.PauseEstimator_CleanupGain = CleanupGain;
        settings.PauseEstimator_CleanupExpGain = CleanupExpGain;

        settings.Save();
    }

    public double GetSaturationDuration(IEnumerable<float>? flows)
    {
        double result;

        if (Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay))
        {
            result = 1;
        }
        else
        {
            var minFlow = flows?.Any() == true ? flows.Min() : 0;
            result = MinSaturationDuration + SaturationGain * Math.Exp(SaturationExpGain * minFlow);
        }

        _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "OD", "Waiting", result.ToString("0.#")));
        return result;
    }

    public double GetCleanupDuration(IEnumerable<float>? flows)
    {
        double result;

        if (Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay))
        {
            result = 0.2;
        }
        else
        {
            var maxFlow = flows?.Any() == true ? flows.Max() : Device.MaxOdoredAirFlowRate;
            result = MinCleanupDuration + CleanupGain * Math.Exp(CleanupExpGain * maxFlow);
        }

        _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "OD", "Waiting", result.ToString("0.#")));
        return result;
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(PauseEstimator));
}
