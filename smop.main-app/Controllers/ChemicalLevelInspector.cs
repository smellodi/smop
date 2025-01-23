using System;

namespace Smop.MainApp.Controllers;

public class ChemicalLevelInspector
{
    public double BasePid { get; set; } = 0.05;        // V
    public double BaseTemp { get; set; } = 20.5;    // Celsius
    public double PidTempCompPower { get; set; } = 2.3;
    public double PidTempCompGain { get; set; } = 0.014;

    public ChemicalLevelInspector()
    {
        var settings = Properties.Settings.Default;
        BasePid = settings.Odor_BasePID;
        BaseTemp = settings.Odor_BasePIDTemp;
        PidTempCompPower = settings.Odor_PIDTempCompPower;
        PidTempCompGain = settings.Odor_PIDTempCompGain;
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
        var dt = temp - BaseTemp;
        var correction = dt > 0 ?
            PidTempCompGain * Math.Pow(dt, PidTempCompPower) :
            -PidTempCompGain * Math.Pow(-dt, PidTempCompPower);
        return 100 * (float)(pidMeasured - BasePid) / (pidExpectedChange + (float)correction);
    }

    public void Save()
    {
        var settings = Properties.Settings.Default;
        settings.Odor_BasePID = BasePid;
        settings.Odor_BasePIDTemp = BaseTemp;
        settings.Odor_PIDTempCompPower = PidTempCompPower;
        settings.Odor_PIDTempCompGain = PidTempCompGain;

        settings.Save();
    }
}


public record class ChemicalLevel(string OdorName, float Level)
{
    public static float TestFlow => 40; // sccm
    public static float Threshold => 95; // %
}
