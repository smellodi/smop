using Smop.MainApp.Controls;
using Smop.OdorDisplay.Packets;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Sensor = Smop.OdorDisplay.Packets.Sensor;

namespace Smop.MainApp.Controllers;

public class IndicatorController(LiveData graph)
{
    public async Task Create(Dispatcher dispatcher, Panel stpOdorDisplayIndicators, Panel smellInspContainer)
    {
        await IndicatorFactory.OdorDisplay(indicator => dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpOdorDisplayIndicators.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        await IndicatorFactory.SmellInsp(indicator => dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            smellInspContainer.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        if (smellInspContainer.Children[0] is ChannelIndicator chi)
        {
            chi.ChannelIdChanged += (s, e) =>
            {
                _smellInspResistor = e;
                ResetGraph(chi);
            };
        }
    }

    public void Update(Data data)
    {
        foreach (var m in data.Measurements)
        {
            bool isBase = m.Device == OdorDisplay.Device.ID.Base;
            foreach (var sv in m.SensorValues)
            {
                /*var value = sv switch
                {
                    Sensor.PID pid => pid.Volts * 1000,
                    Sensor.Thermometer temp => temp.Celsius,          // Ignored values:
                    Sensor.BeadThermistor beadTemp => beadTemp.Ohms,  // beadTemp.Volts
                    Sensor.Humidity humidity => humidity.Percent,     // humidity.Celsius
                    Sensor.Pressure pressure => pressure.Millibars,   // pressure.Celsius
                    Sensor.Gas gas => isBase ?                        // gas.Millibars, gas.Celsius
                        gas.SLPM :
                        gas.SLPM * 1000,
                    Sensor.Valve valve => valve.Opened ? 1 : 0,
                    _ => 0
                };

                var source = IndicatorFactory.GetSourceId(m.Device, (OdorDisplay.Device.Capability)sv.Sensor);
                Update(source, value);
                */

                var values = sv.Values;
                for (int sensorSubID = 0; sensorSubID < values.Length; sensorSubID++)
                {
                    var value = values[sensorSubID];
                    if (sv is Sensor.PID)
                        value *= 1000;
                    else if (sv is Sensor.Gas && !isBase && sensorSubID == 0)   // SLPM
                        value *= 1000;

                    var source = IndicatorFactory.GetSourceId(m.Device, (OdorDisplay.Device.Capability)sv.Sensor, sensorSubID);
                    Update(source, value);
                }
            }
        }
    }

    public void ApplyOdorChannelProps(OdorChannel odorChannel)
    {
        foreach (var chi in _indicators.Values)
        {
            IndicatorFactory.ApplyChannelProps(chi, odorChannel.ID, odorChannel.Name);
        }
    }

    public void Update(SmellInsp.Data data)
    {
        var value = data.Resistances[_smellInspResistor];
        var source = IndicatorFactory.GetSourceId(IndicatorFactory.SmellInspChannels[0].Type);
        Update(source, value);

        source = IndicatorFactory.GetSourceId(IndicatorFactory.SmellInspChannels[1].Type);
        Update(source, data.Temperature);

        source = IndicatorFactory.GetSourceId(IndicatorFactory.SmellInspChannels[2].Type);
        Update(source, data.Humidity);
    }

    public void Update(string source, float value)
    {
        if (_indicators.ContainsKey(source))
        {
            var indicator = _indicators[source];
            indicator.Value = value;

            if (_currentIndicator == indicator)
            {
                double timestamp = Utils.Timestamp.Sec;
                _graph.Add(timestamp, value);
            }
        }
    }

    public void Clear()
    {
        foreach (var chi in _indicators.Values)
        {
            chi.Value = 0;
        }

        if (_currentIndicator != null)
        {
            _currentIndicator.IsActive = false;
            _currentIndicator = null;
            _graph.Empty();
        }
    }


    // Internal

    readonly Storage _storage = Storage.Instance;
    readonly Dictionary<string, ChannelIndicator> _indicators = new();

    readonly LiveData _graph = graph;

    ChannelIndicator? _currentIndicator = null;
    int _smellInspResistor = 0;

    private void ResetGraph(ChannelIndicator? chi, double baseValue = .0)
    {
        var interval = 1.0;
        if (chi == null)
        {
            interval = 1.0;
        }
        else if (chi.Source.StartsWith("od"))
        {
            interval = (double)(_storage.Simulating.HasFlag(SimulationTarget.OdorDisplay) ?
                OdorDisplay.CommPort.SamplingInterval :
                OdorDisplay.Device.DataMeasurementInterval)
                / 1000;
        }
        else if (chi.Source.StartsWith("snt"))
        {
            interval = SmellInsp.CommPort.SamplingInterval;
        }

        _graph.Reset(interval, baseValue);
    }

    private void ChannelIndicator_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        var chi = sender as ChannelIndicator;
        if (!chi?.IsActive ?? false)
        {
            if (_currentIndicator != null)
            {
                _currentIndicator.IsActive = false;
            }

            _currentIndicator = chi;

            if (_currentIndicator != null)
            {
                _currentIndicator.IsActive = true;
            }

            ResetGraph(chi);
        }
    }
}
