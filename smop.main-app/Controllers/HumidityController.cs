using Smop.MainApp.Logging;
using Smop.MainApp.Utils;
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
            _nlog.Info(LogIO.Text(Timestamp.Ms, "Target humidity", _targetHumidity));
            if (_timer.Enabled)
            {
                _timer.Stop();
                _timer.Start();
            }
        }
    }

    public void Init() { }

    // Internal

    const float UPDATE_INTERVAL = 5000; // ms
    const float CONTROL_GAIN = 0.75f;
    const float SENSITIVITY = 0.1f; // RH will not be altered if the difference with the target RH is within +-SENSITIVITY range
    const float RH_MIN = -2;    // allow minor negative values for RH
    const float RH_MAX = 70;    // this is around the maximum possble RH available if 100% of the clean air flow comes from the humidification branch.

    static HumidityController? _instance = null;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(HumidityController));

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

        _targetHumidity = Properties.Settings.Default.Setup_Humidity;
        _currentHumidity = _targetHumidity;

        if (_isEnabled)
        {
            Start();
        }
    }

    private void Start()
    {
        _currentHumidity = _targetHumidity;
        _nlog.Info(LogIO.Text(Timestamp.Ms, "Humidity at start", _measuredHumidity));

        _timer.Start();
        Timer_Elapsed(this, null);
    }

    private void Stop()
    {
        _timer.Stop();
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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
        if (Math.Abs(diff) > SENSITIVITY)
        {
            _currentHumidity += CONTROL_GAIN * diff;
            
            // Limit the value
            if (_currentHumidity < RH_MIN)
                _currentHumidity = RH_MIN;
            else if (_currentHumidity > RH_MAX)
                _currentHumidity = RH_MAX;

            _ctrl.SetHumidity(_currentHumidity);
            _nlog.Info(LogIO.Text(Timestamp.Ms, "Set humidity", _currentHumidity));
        }
    }
}
