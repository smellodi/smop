using Smop.OdorDisplay.Packets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Smop.OdorDisplay;

/// <summary>
/// This class is used to emulate communication with the device via <see cref="CommPort"/>
/// </summary>
internal class SerialPortEmulator : ISerialPort, System.IDisposable
{
    public bool IsOpen { get; private set; } = false;

    public SerialPortEmulator()
    {
        _dataTimer.Elapsed += (s, e) =>
        {
            if (_timerAutoStop)
            {
                _timerAutoStop = false;
                _dataTimer.Stop();
            }

            lock (_responses) { _responses.Enqueue(GenerateData().ToArray()); }
        };
    }

    public void Open() { IsOpen = true; }

    public void Close()
    {
        IsOpen = false;
        Thread.Sleep(10);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        while (IsOpen)
        {
            Thread.Sleep(10);
            lock (_requests)
            {
                if (_requests.Count > 0)
                {
                    var req = _requests.Dequeue();
                    Execute(req);
                    MakeResponse(req);
                }
            }

            lock (_responses)
            {
                if (_responses.TryDequeue(out byte[]? resp))
                {
                    if (resp.Length > count)
                    {
                        var rest = resp[count..];
                        resp = resp[..count];

                        var q = new Queue<byte[]>();
                        q.Enqueue(rest);
                        while (_responses.Count > 0) q.Enqueue(_responses.Dequeue());
                        while (q.Count > 0) _responses.Enqueue(q.Dequeue());
                    }
                    resp.CopyTo(buffer, offset);
                    return resp.Length;
                }
            }
        }

        return 0;
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        lock (_requests)
        {
            var bytesWithoutPreamble = buffer[(offset + Packet.PREAMBLE_LENGTH)..(offset + count)];
            _requests.Enqueue(Request.From(bytesWithoutPreamble));
        }
    }

    public void Dispose()
    {
        _dataTimer.Dispose();
        System.GC.SuppressFinalize(this);
    }

    // Internal

    readonly Queue<Request> _requests = new();
    readonly Queue<byte[]> _responses = new();

    readonly System.Timers.Timer _dataTimer = new(CommPort.SamplingInterval);
    readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

    readonly HashSet<(long, Device.ID, Device.Controller)> _valveCloseEvents = new();
    
    bool _timerAutoStop = false;

    const float BASE_PID = 0.06f;

    // Target values (what the state should be like very soon)
    float _targetPID = BASE_PID;  // Volts

    // Current values (what the state is like right now)
    float _currentPID = BASE_PID;  // Volts

    readonly Dictionary<Device.ID, Actuator> _state = new()
    {
        { Device.ID.Base,
            new Actuator(Device.ID.Base, new ActuatorCapabilities(
            KeyValuePair.Create(Device.Controller.ChassisTemperature, 27f),
            KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
            KeyValuePair.Create(Device.Controller.DilutionAirFlow, 0f),
            KeyValuePair.Create(Device.Controller.OdorantValve, 0f),
            KeyValuePair.Create(Device.Controller.OutputValve, 0f)
        )) },
        { Device.ID.Odor1, new Actuator(Device.ID.Odor1, new ActuatorCapabilities(
            KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
            KeyValuePair.Create(Device.Controller.OdorantValve, 0f)
        )) },
        { Device.ID.Odor2, new Actuator(Device.ID.Odor2, new ActuatorCapabilities(
            KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
            KeyValuePair.Create(Device.Controller.OdorantValve, 0f)
        )) },
        { Device.ID.Odor3, new Actuator(Device.ID.Odor3, new ActuatorCapabilities(
            KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
            KeyValuePair.Create(Device.Controller.OdorantValve, 0f)
        )) },
        { Device.ID.Odor4, new Actuator(Device.ID.Odor4, new ActuatorCapabilities(
            KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
            KeyValuePair.Create(Device.Controller.OdorantValve, 0f)
        )) },
        { Device.ID.Odor5, new Actuator(Device.ID.Odor5, new ActuatorCapabilities(
            KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
            KeyValuePair.Create(Device.Controller.OdorantValve, 0f)
        )) },
    };

    private void Execute(Request req)
    {
        var ts = _stopwatch.ElapsedMilliseconds;

        if (req.Type == Type.SetActuators)
        {
            var setActuatorsReq = (SetActuators)req;
            foreach (var actuator in setActuatorsReq!.Actuators)
            {
                if (_state.ContainsKey(actuator.DeviceID))
                {
                    var deviceState = _state[actuator.DeviceID];
                    foreach (var cap in actuator.Capabilities)
                    {
                        if (deviceState.Capabilities.ContainsKey(cap.Key))
                        {
                            if ((cap.Key == Device.Controller.OdorantValve || cap.Key == Device.Controller.OutputValve) && cap.Value > 0)
                            {
                                _valveCloseEvents.Add(((long)(ts + cap.Value), actuator.DeviceID, cap.Key));
                            }
                            else if (cap.Key == Device.Controller.OdorantFlow)
                            {
                                if (actuator.DeviceID == Device.ID.Odor1)
                                {
                                    IonVision.SimulatedData.LineGains.Components[0] = 5 + 95 * cap.Value / 0.5f; // 0.5 == 50 scc
                                    SmellInsp.SimulatedData.Gains[0] = 8 + 800 * cap.Value / 0.5f; // 0.5 == 50 scc
                                }
                                else if (actuator.DeviceID == Device.ID.Odor2)
                                {
                                    IonVision.SimulatedData.LineGains.Components[1] = 5 + 85 * cap.Value / 0.5f;
                                    SmellInsp.SimulatedData.Gains[1] = 6 + 500 * cap.Value / 0.5f; // 0.5 == 50 scc
                                }
                                else if (actuator.DeviceID == Device.ID.Odor3)
                                {
                                    IonVision.SimulatedData.LineGains.Components[2] = 0 + 40 * cap.Value / 0.5f;
                                    SmellInsp.SimulatedData.Gains[2] = 5 + 300 * cap.Value / 0.5f; // 0.5 == 50 scc
                                }
                                else if (actuator.DeviceID == Device.ID.Odor4)
                                {
                                    IonVision.SimulatedData.LineGains.Components[3] = 0 + 60 * cap.Value / 0.5f;
                                    SmellInsp.SimulatedData.Gains[3] = 0 + 400 * cap.Value / 0.5f; // 0.5 == 50 scc
                                }
                                else if (actuator.DeviceID == Device.ID.Odor5)
                                {
                                    IonVision.SimulatedData.LineGains.Components[4] = 0 + 55 * cap.Value / 0.5f;
                                    SmellInsp.SimulatedData.Gains[4] = 0 + 550 * cap.Value / 0.5f; // 0.5 == 50 scc
                                }
                            }
                            deviceState.Capabilities[cap.Key] = cap.Value;
                        }
                    }
                }
            }

            UpdateSystemTargets();
        }
    }

    private void MakeResponse(Request req)
    {
        Result result = Result.OK;

        lock (_responses)
        {
            if (req.Type == Type.QueryVersion)
            {
                _responses.Enqueue(new Version(new string[] { "1.2", "3.4", "5.6" }).ToArray());
            }
            else if (req.Type == Type.QueryDevices)
            {
                _responses.Enqueue(new Devices(new bool[] {
                    true,       // Base
                    true, true, // Odor1 and Odor2
                    true, true, true, false, false, false, false,    // Odor 3-9
                    true        // Dilution
                }).ToArray());
            }
            else if (req.Type == Type.QueryCapabilities)
            {
                var queryCaps = req as QueryCapabilities;
                var isBase = queryCaps!.Device == Device.ID.Base;
                var isDilutionAir = queryCaps!.Device == Device.ID.DilutionAir;
                _responses.Enqueue(new Capabilities(new Dictionary<Device.Capability, bool> {
                    { Device.Capability.PID, isBase || isDilutionAir },
                    { Device.Capability.BeadThermistor, isDilutionAir },
                    { Device.Capability.ChassisThermometer, isBase || isDilutionAir },
                    { Device.Capability.OdorSourceThermometer, !isDilutionAir },
                    { Device.Capability.GeneralPurposeThermometer, isBase },
                    { Device.Capability.OutputAirHumiditySensor, isBase },
                    { Device.Capability.InputAirHumiditySensor, isBase },
                    { Device.Capability.PressureSensor, !isDilutionAir },
                    { Device.Capability.OdorantFlowSensor, !isDilutionAir },
                    { Device.Capability.DilutionAirFlowSensor, isBase },
                    { Device.Capability.OdorantValveSensor, true },
                    { Device.Capability.OutputValveSensor, isBase },
                    { Device.Capability.OdorantFlowController, true },
                    { Device.Capability.DilutionAirFlowController, isBase },
                    { Device.Capability.ChassisTemperatureController, isBase },
                    { Device.Capability.OdorantValveController, true },
                    { Device.Capability.OutputValveController, isBase },
                }).ToArray());
            }
            else if (req.Type == Type.SetActuators)
            {
                var sa = req as SetActuators;
                foreach (var actuator in sa!.Actuators)
                {
                    var maxFlowRate = actuator.DeviceID == Device.ID.Base ? Device.MaxBaseAirFlowRate : Device.MaxOdoredAirFlowRate;
                    foreach (var cap in actuator.Capabilities)
                    {
                        if ((cap.Key == Device.Controller.OdorantFlow || cap.Key == Device.Controller.DilutionAirFlow) &&
                            (cap.Value < 0 || cap.Value > maxFlowRate))
                            result = Result.InvalidValue;
                        else if (cap.Key == Device.Controller.ChassisTemperature && (cap.Value < 0 || cap.Value > 50f))
                            result = Result.InvalidValue;
                        else if ((cap.Key == Device.Controller.OutputValve || cap.Key == Device.Controller.OdorantValve) && (cap.Value < -1f || cap.Value > 3600_000f))
                            result = Result.InvalidValue;
                    }
                }
            }
            else if (req.Type == Type.SetSystem)
            {
            }
            else if (req.Type == Type.SetMeasurements)
            {
                var setMsms = req as SetMeasurements;
                if (setMsms!.Mode == SetMeasurements.Command.Start)
                {
                    _dataTimer.Start();
                }
                else if (setMsms!.Mode == SetMeasurements.Command.Stop)
                {
                    _dataTimer.Stop();
                }
                else
                {
                    _timerAutoStop = true;
                    _dataTimer.Start();
                }
            }
            else if (req.Type == Type.Reset)
            {
                return;
            }
            else
            {
                throw new System.Exception($"Unknown request '{req.Type}'");
            }

            _responses.Enqueue(new Ack(result).ToArray());
        }
    }

    private Data GenerateData()
    {
        UpdateClosedValves();
        UpdateSystemVariables();

        var baseCaps = _state[Device.ID.Base].Capabilities;

        var measurements = new List<Measurement>
        {
            new(Device.ID.Base, new SensorValue[]
            {
                new PIDValue(_currentPID + Random.Range(0.001f)),
                //new BeadThermistorValue(float.PositiveInfinity, 3.5f + Random.Range(0.1f)),
                new ThermometerValue(Device.Sensor.ChassisThermometer, baseCaps[Device.Controller.ChassisTemperature] + Random.Range(0.1f)),
                new ThermometerValue(Device.Sensor.OdorSourceThermometer, 27.0f + Random.Range(0.1f)),
                new ThermometerValue(Device.Sensor.GeneralPurposeThermometer, 27.0f + Random.Range(0.1f)),
                new HumidityValue(Device.Sensor.OutputAirHumiditySensor, (baseCaps[Device.Controller.OdorantFlow] * Device.MaxBaseAirFlowRate) + Random.Range(0.07f), 27.1f + Random.Range(0.1f)),
                new HumidityValue(Device.Sensor.InputAirHumiditySensor, 0.22f + Random.Range(0.04f), 26.6f + Random.Range(0.1f)),
                new PressureValue(1006f + Random.Range(1.0f), 27.2f + Random.Range(0.1f)),
                new GasValue(Device.Sensor.OdorantFlowSensor, baseCaps[Device.Controller.OdorantFlow] + Random.Range(0.005f), 27.5f + Random.Range(0.1f), 1001.0f + Random.Range(0.5f)),
                new GasValue(Device.Sensor.DilutionAirFlowSensor, baseCaps[Device.Controller.DilutionAirFlow] + Random.Range(0.005f), 27.5f + Random.Range(0.1f), 1001.0f + Random.Range(0.5f)),
                new ValveValue(Device.Sensor.OdorantValveSensor, baseCaps[Device.Controller.OdorantValve] != 0),
                new ValveValue(Device.Sensor.OutputValveSensor, false),
            })
        };

        var flowConverter = Device.MaxOdoredAirFlowRate / 1000;
        for (int i = 1; i < _state.Count; i++)
        {
            var id = (Device.ID)i;
            var odorCaps = _state[id].Capabilities;
            measurements.Add(new Measurement(id, new SensorValue[]
            {
                new GasValue(Device.Sensor.OdorantFlowSensor, odorCaps[Device.Controller.OdorantFlow] * flowConverter + Random.Range(0.0005f), 27.4f + Random.Range(0.1f), 1002.0f + Random.Range(0.5f)),
                new ThermometerValue(Device.Sensor.OdorSourceThermometer, 27.0f + Random.Range(0.1f)),
                new PressureValue(1006f + Random.Range(1.0f), 27.2f + Random.Range(0.1f)),
                new ValveValue(Device.Sensor.OdorantValveSensor, odorCaps[Device.Controller.OdorantValve] != 0),
            }));
        }

        return new Data((int)_stopwatch.ElapsedMilliseconds, measurements.ToArray());
    }

    private void UpdateClosedValves()
    {
        var ts = _stopwatch.ElapsedMilliseconds;
        foreach (var finished in _valveCloseEvents.Where(ev => ev.Item1 <= ts))
        {
            _state[finished.Item2]!.Capabilities[finished.Item3] = 0;
        }

        _valveCloseEvents.RemoveWhere(ev => ev.Item1 <= ts);
    }

    private void UpdateSystemTargets()
    {
        var totalOdorFlow = _state
            .Where(state => state.Key != Device.ID.Base)
            .Select(state => state.Value.Capabilities)
            .Where(caps => caps.ContainsKey(Device.Controller.OdorantValve) && caps.ContainsKey(Device.Controller.OdorantFlow))
            .Where(caps => caps[Device.Controller.OdorantValve] != 0 && caps[Device.Controller.OdorantFlow] > 0)
            .Select(caps => caps[Device.Controller.OdorantFlow])    // normalized value!
            .Sum();

        _targetPID = BASE_PID + totalOdorFlow * Device.MaxOdoredAirFlowRate * 2 / 1000;
    }

    private void UpdateSystemVariables()
    {
        _currentPID += (_targetPID - _currentPID) * 0.1f;
    }

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static readonly System.Random _random = new();
    }
}
