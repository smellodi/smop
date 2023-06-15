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
                    { Device.Capability.InputAirHumiditySensor, isBase },
                    { Device.Capability.OutputAirHumiditySensor, isBase },
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
        return new Data((int)_stopwatch.ElapsedMilliseconds, new Measurement[]
                {
                    new Measurement(Device.ID.Base, new SensorValue[]
                    {
                        new PIDValue(2.5f + Random.Range(0.1f)),
                        new BeadThermistorValue(float.PositiveInfinity, 3.5f + Random.Range(0.1f)),
                        new ThermometerValue(Device.Sensor.OdorSourceThermometer, 27.0f + Random.Range(0.1f)),
                        new HumidityValue(Device.Sensor.InputAirHumiditySensor, 60f + Random.Range(0.2f), 27.1f + Random.Range(0.1f)),
                        new HumidityValue(Device.Sensor.OutputAirHumiditySensor, 55f + Random.Range(0.2f), 26.6f + Random.Range(0.1f)),
                        new PressureValue(1200f + Random.Range(1.0f), 27.2f + Random.Range(0.1f)),
                        new GasValue(Device.Sensor.OdorantFlowSensor, 5.0f + Random.Range(0.05f), 27.5f + Random.Range(0.1f), 1001.0f + Random.Range(0.5f)),
                        new ValveValue(Device.Sensor.OdorantValveSensor, true),
                    }),
                    new Measurement(Device.ID.Odor1, new SensorValue[]
                    {
                        new GasValue(Device.Sensor.OdorantFlowSensor, 0.1f + Random.Range(0.005f), 27.4f + Random.Range(0.1f), 1002.0f + Random.Range(0.5f)),
                        new ValveValue(Device.Sensor.OdorantValveSensor, true),
                    }),
                    new Measurement(Device.ID.Odor2, new SensorValue[]
                    {
                        new GasValue(Device.Sensor.OdorantFlowSensor, 0.05f + Random.Range(0.0005f), 27.3f + Random.Range(0.1f), 1003.0f + Random.Range(0.5f)),
                        new ValveValue(Device.Sensor.OdorantValveSensor, true),
                    }),
                });
    }

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static System.Random _random = new();
    }
}
