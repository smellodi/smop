using Smop.MainApp.Logging;
using Smop.OdorDisplay;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Smop.MainApp.Controllers;

internal class PauseEstimator: INotifyPropertyChanged
{
    public double MinSaturationDuration
    {
        get => _minSaturationDuration;
        set
        {
            _minSaturationDuration = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }
    public double SaturationGain
    {
        get => _saturationGain;
        set
        {
            _saturationGain = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }
    public double SaturationExpGain
    {
        get => _saturationExpGain;
        set
        {
            _saturationExpGain = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }

    public double MinCleanupDuration
    {
        get => _minCleanupDuration;
        set
        {
            _minCleanupDuration = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }
    public double CleanupGain
    {
        get => _cleanupGain;
        set
        {
            _cleanupGain = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }
    public double CleanupExpGain
    {
        get => _cleanupExpGain;
        set
        {
            _cleanupExpGain = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }

    public double? ExampleFlow
    {
        get => _exampleFlow;
        set
        {
            _exampleFlow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleSaturation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExampleCleanup)));
        }
    }

    public double? ExampleSaturation => _exampleFlow == null ? null : GetSaturationDuration(new float[] { (float)(_exampleFlow ?? 0) }, false);
    public double? ExampleCleanup => _exampleFlow == null ? null : GetCleanupDuration(new float[] { (float)(_exampleFlow ?? 0) }, false);

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public double GetSaturationDuration(IEnumerable<float>? flows, bool accountForSimulationFlag = true)
    {
        double result;

        if (accountForSimulationFlag && Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay))
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

    public double GetCleanupDuration(IEnumerable<float>? flows, bool accountForSimulationFlag = false)
    {
        double result;

        if (accountForSimulationFlag && Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay))
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

    double _minSaturationDuration = 8;  // 3.38
    double _saturationGain = 8;         // 28.53
    double _saturationExpGain = -0.07;  // -0.2752

    double _minCleanupDuration = 2.5;
    double _cleanupGain = 0.8;
    double _cleanupExpGain = 0.03;

    double? _exampleFlow = null;
}
