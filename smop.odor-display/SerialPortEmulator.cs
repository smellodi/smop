using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Smop.OdorDisplay.Packets;

namespace Smop.OdorDisplay;

/// <summary>
/// This class is used to emulate communication with the device via <see cref="CommPort"/>
/// </summary>
public class SerialPortEmulator : ISerialPort
{
    /// <summary>
    /// ms
    /// </summary>
    public static int SamplingFrequency { get; set; } = 100;

    public bool IsOpen => _isOpen;

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

    public void Open() { _isOpen = true; }

    public void Close()
    {
        _isOpen = false;
        Thread.Sleep(10);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        while (_isOpen)
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

    // Internal

    bool _isOpen = false;
    bool _timerAutoStop = false;
    Queue<Request> _requests = new();
    Queue<byte[]> _responses = new();

    System.Timers.Timer _dataTimer = new(SamplingFrequency);
    Stopwatch _stopwatch = Stopwatch.StartNew();

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
                            deviceState.Capabilities[cap.Key] = cap.Value;
                        }
                    }
                }
            }
        }
    }

    private void MakeResponse(Request req)
    {
        Packets.Result result = Packets.Result.OK;

        lock (_responses)
        {
            if (req.Type == Type.QueryVersion)
            {
                _responses.Enqueue(new Version(new string[] { "1.2", "3.4", "5.6" }).ToArray());
            }
            else if (req.Type == Type.QueryDevices)
            {
                _responses.Enqueue(new Devices(new bool[] {
                true,
                true, true,
                false, false, false, false, false, false, false,
                true
            }).ToArray());
            }
            else if (req.Type == Type.QueryCapabilities)
            {
                var queryCaps = req as QueryCapabilities;
                var isBase = queryCaps!.Device == Device.ID.Base;
                _responses.Enqueue(new Capabilities(new Dictionary<Device.Capability, bool> {
                    { Device.Capability.PID, isBase },
                    { Device.Capability.BeadThermistor, false },
                    { Device.Capability.ChassisThermometer, isBase },
                    { Device.Capability.OdorSourceThermometer, isBase },
                    { Device.Capability.GeneralPurposeThermometer, isBase },
                    { Device.Capability.OutputAirHumiditySensor, isBase },
                    { Device.Capability.InputAirHumiditySensor, isBase },
                    { Device.Capability.PressureSensor, isBase },
                    { Device.Capability.OdorantFlowSensor, true },
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
                            result = Packets.Result.InvalidValue;
                        else if (cap.Key == Device.Controller.ChassisTemperature && (cap.Value < 0 || cap.Value > 50f))
                            result = Packets.Result.InvalidValue;
                        else if ((cap.Key == Device.Controller.OutputValve || cap.Key == Device.Controller.OdorantValve) && (cap.Value < -1f || cap.Value > 3600_000f))
                            result = Packets.Result.InvalidValue;
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
        var baseCaps = _state[Device.ID.Base].Capabilities;

        var measurements = new List<Measurement>
        {
            new Measurement(Device.ID.Base, new SensorValue[]
            {
                new PIDValue(0.06f + Random.Range(0.001f)),
                //new BeadThermistorValue(float.PositiveInfinity, 3.5f + Random.Range(0.1f)),
                new ThermometerValue(Device.Sensor.ChassisThermometer, baseCaps[Device.Controller.ChassisTemperature] + Random.Range(0.1f)),
                new ThermometerValue(Device.Sensor.OdorSourceThermometer, 27.0f + Random.Range(0.1f)),
                new ThermometerValue(Device.Sensor.GeneralPurposeThermometer, 27.0f + Random.Range(0.1f)),
                new HumidityValue(Device.Sensor.OutputAirHumiditySensor, (baseCaps[Device.Controller.OdorantFlow] * Device.MaxBaseAirFlowRate) + Random.Range(0.04f), 27.1f + Random.Range(0.1f)),
                new HumidityValue(Device.Sensor.InputAirHumiditySensor, 0f + Random.Range(0.05f), 26.6f + Random.Range(0.1f)),
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
                new ValveValue(Device.Sensor.OdorantValveSensor, odorCaps[Device.Controller.OdorantValve] != 0),
            }));
        }

        return new Data((int)_stopwatch.ElapsedMilliseconds, measurements.ToArray());
    }

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static System.Random _random = new();
    }
}
