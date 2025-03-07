using System;

namespace Smop.MainApp.Controllers;

public class SetupSettings
{
    /// <summary>
    /// Dilution ratio denominator, ie. "x" in "1:x"
    /// </summary>
    public float DilutionRatio
    {
        get => Properties.Settings.Default.Setup_DilutionRatio;
        set
        {
            if (Properties.Settings.Default.Setup_DilutionRatio != value)
            {
                Properties.Settings.Default.Setup_DilutionRatio = value;
                Properties.Settings.Default.Save();
            }
        }
    }

    public float Humidity
    {
        get => Properties.Settings.Default.Setup_Humidity;
        set
        {
            if (Properties.Settings.Default.Setup_Humidity != value)
            {
                Properties.Settings.Default.Setup_Humidity = value;
                Properties.Settings.Default.Save();

                HumidityChanged?.Invoke(this, value);
            }
        }
    }

    public bool HumidityAutoAdjustment
    {
        get => Properties.Settings.Default.Setup_HumidityAutoAdjustment;
        set
        {
            if (Properties.Settings.Default.Setup_HumidityAutoAdjustment != value)
            {
                Properties.Settings.Default.Setup_HumidityAutoAdjustment = value;
                Properties.Settings.Default.Save();

                HumidityAutoAdjustmentChanged?.Invoke(this, value);
            }
        }
    }

    public float ChassisHeaterTemperature
    {
        get => Properties.Settings.Default.Setup_ChassisHeaterTemperature;
        set
        {
            if (Properties.Settings.Default.Setup_ChassisHeaterTemperature != value)
            {
                Properties.Settings.Default.Setup_ChassisHeaterTemperature = value;
                Properties.Settings.Default.Save();
            }
        }
    }

    public event EventHandler<float>? HumidityChanged;
    public event EventHandler<bool>? HumidityAutoAdjustmentChanged;
}
