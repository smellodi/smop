using System;
using System.Linq;

namespace Smop.MainApp.Controllers;

internal class HumidityController
{
    public static HumidityController Instance => _instance ??= new();

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                if (_isEnabled)
                    Start();
                else
                    Stop();
            }
        }
    }

    public float TargetHumidity
    {
        get => _targetHumidity;
        set 
        {
            _targetHumidity = value;
            System.Diagnostics.Debug.WriteLine($"[HC] Target humidity: {_targetHumidity:F2}");
            if (_timer.Enabled)
            {
                _timer.Stop();
                _timer.Start();
            }
        }
    }

    public void Init() { }

    // Internal

    const float CONTROL_GAIN = 0.75f;
    const float UPDATE_INTERVAL = 5000; // ms

    static HumidityController? _instance = null;

    readonly OdorDisplayController _ctrl = new();
    readonly System.Timers.Timer _timer = new();
    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;

    bool _isEnabled;
    float _targetHumidity;
    float? _measuredHumidity = null;
    float _currentHumidity;

    private HumidityController()
    {
        _timer.Interval = UPDATE_INTERVAL;
        _timer.AutoReset = true;
        _timer.Elapsed += Timer_Elapsed;

        _isEnabled = Properties.Settings.Default.Setup_HumidityAutoAdjustment;

        _odorDisplay.Data += OdorDisplay_Data;

        _targetHumidity = Properties.Settings.Default.Pulses_Humidity;
        _currentHumidity = _targetHumidity;

        if (_isEnabled)
        {
            Start();
        }
    }

    private void Start()
    {
        _currentHumidity = _targetHumidity;
        System.Diagnostics.Debug.WriteLine($"[HC] Current humidity: {_measuredHumidity:F2}");

        _timer.Start();
        Timer_Elapsed(this, null);
    }

    private void Stop()
    {
        _timer.Stop();
    }

    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        foreach (var measurement in data.Measurements)
        {
            if (measurement.Device == OdorDisplay.Device.ID.Base &&
                measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.OutputAirHumiditySensor) is OdorDisplay.Packets.Sensor.Humidity humidSensor)
            {
                _measuredHumidity = humidSensor.Percent;
                break;
            }
        }
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs? e)
    {
        var diff = _targetHumidity - (_measuredHumidity ?? _targetHumidity);
        if (Math.Abs(diff) > 0.1)
        {
            _currentHumidity += CONTROL_GAIN * diff;
            _ctrl.SetHumidity(_currentHumidity);
            System.Diagnostics.Debug.WriteLine($"[HC] Corrected humidity: {_currentHumidity:F2}");
        }
    }
}
