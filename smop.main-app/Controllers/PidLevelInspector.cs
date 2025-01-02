using System;

namespace Smop.MainApp.Controllers;

public class PidLevelInspector
{
    // Constants for ComputePidLevel, found empirically.. may be incorrect!

    public double BasePID { get; set; } = 0.052;       // V
    public double BasePIDTemp { get; set; } = 20.5;    // Celsius
    public double PIDCompPower { get; set; } = 2.3;
    public double PIDCompGain { get; set; } = 0.014;

    public PidLevelInspector()
    {
        var settings = Properties.Settings.Default;
        BasePID = settings.Odor_BasePID;
        BasePIDTemp = settings.Odor_BasePIDTemp;
        PIDCompPower = settings.Odor_PIDTempCompPower;
        PIDCompGain = settings.Odor_PIDTempCompGain;
    }

    /// <summary>
    /// Computes the PID level as a percentage to the expected level given the reference flow <see cref="ChemicalLevel.TestFlow"/>.
    /// The correction formula was found empirically and may be wrong!
    /// </summary>
    /// <param name="pidExpectedChange">PID from odor properties, Volts</param>
    /// <param name="pidMeasured">PID measured, Volts</param>
    /// <param name="temp">Temperature, Celsius</param>
    /// <returns>Percentage</returns>
    public float ComputePidLevel(float pidExpectedChange, float pidMeasured, float temp)
    {
        var dt = temp - BasePIDTemp;
        var correction = dt > 0 ?
            PIDCompGain * Math.Pow(dt, PIDCompPower) :
            -PIDCompGain * Math.Pow(-dt, PIDCompPower);
        return 100 * (float)(pidMeasured - BasePID) / (pidExpectedChange + (float)correction);
    }

    public void Save()
    {
        var settings = Properties.Settings.Default;
        settings.Odor_BasePID = BasePID;
        settings.Odor_BasePIDTemp = BasePIDTemp;
        settings.Odor_PIDTempCompPower = PIDCompPower;
        settings.Odor_PIDTempCompGain = PIDCompGain;

        settings.Save();
    }
}


public record class ChemicalLevel(string OdorName, float Level)
{
    public static float TestFlow => 40; // nccm
    public static float Threshold => 95; // %
}
