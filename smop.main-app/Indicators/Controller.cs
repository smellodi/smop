using Smop.OdorDisplay.Packets;
using Smop.MainApp.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Smop.MainApp.Reproducer;

namespace Smop.MainApp.Indicators;

public class Controller
{
    public Controller(LiveData graph)
    {
        _graph = graph;
    }

    public async Task Create(Dispatcher dispatcher, Panel stpOdorDisplayIndicators, Panel smellInspContainer)
    {
        await Factory.OdorDisplay(indicator => dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpOdorDisplayIndicators.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        await Factory.SmellInsp(indicator => dispatcher.Invoke(() =>
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
                var value = sv switch
                {
                    PIDValue pid => pid.Volts * 1000,
                    ThermometerValue temp => temp.Celsius,          // Ignored values:
                    BeadThermistorValue beadTemp => beadTemp.Ohms,  // beadTemp.Volts
                    HumidityValue humidity => humidity.Percent,     // humidity.Celsius
                    PressureValue pressure => pressure.Millibars,   // pressure.Celsius
                    GasValue gas => isBase ?                        // gas.Millibars, gas.Celsius
                        gas.SLPM :
                        gas.SLPM * 1000,
                    ValveValue valve => valve.Opened ? 1 : 0,
                    _ => 0
                };

                var source = Factory.GetSourceId(m.Device, (OdorDisplay.Device.Capability)sv.Sensor);
                Update(source, value);
            }
        }
    }

    public void ApplyGasProps(Gas gas)
    {
        foreach (var chi in _indicators.Values)
        {
            Factory.ApplyGasProps(chi, gas.ChannelID, gas.Name);
        }
    }

    public void Update(SmellInsp.Data data)
    {
        var value = data.Resistances[_smellInspResistor];
        var source = Factory.GetSourceId(Factory.SmellInspChannels[0].Type);
        Update(source, value);

        source = Factory.GetSourceId(Factory.SmellInspChannels[1].Type);
        Update(source, data.Temperature);

        source = Factory.GetSourceId(Factory.SmellInspChannels[2].Type);
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

    readonly LiveData _graph;

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
                OdorDisplay.SerialPortEmulator.SamplingFrequency :
                OdorDisplay.Device.DataMeasurementInterval)
                / 1000;
        }
        else if (chi.Source.StartsWith("snt"))
        {
            interval = SmellInsp.ISerialPort.Interval;
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
