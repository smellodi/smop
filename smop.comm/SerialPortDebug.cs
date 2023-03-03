using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using SMOP.Comm.Packets;

namespace SMOP.Comm
{
    /// <summary>
    /// This class is used to debug communication with the device via <see cref="CommPort"/>
    /// </summary>
    public class SerialPortDebug : ISerialPort
    {
        public bool IsOpen => _isOpen;

        public SerialPortDebug()
        {
            _dataTimer.Elapsed += (s, e) => { lock (_responses) { _responses.Enqueue(GenerateData().ToArray()); } };
        }

        public void Open() { _isOpen = true; }

        public void Close() { _isOpen = false; }

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
        Queue<Request> _requests = new();
        Queue<byte[]> _responses = new();

        System.Timers.Timer _dataTimer = new(2000);
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
                        { Device.Capability.BeadThermistor, isBase },
                        { Device.Capability.ChassisThermometer, false },
                        { Device.Capability.OdorSourceThermometer, false },
                        { Device.Capability.GeneralPurposeThermometer, false },
                        { Device.Capability.InputAirHumiditySensor, isBase },
                        { Device.Capability.OutputAirHumiditySensor, false },
                        { Device.Capability.PressureSensor, isBase },
                        { Device.Capability.OdorantFlowSensor, true },
                        { Device.Capability.DilutionAirFlowSensor, false },
                        { Device.Capability.OdorantValveSensor, true },
                        { Device.Capability.OutputValveSensor, false },
                        { Device.Capability.OdorantFlowController, true },
                        { Device.Capability.DilutionAirFlowController, false },
                        { Device.Capability.ChassisTemperatureController, false },
                        { Device.Capability.OdorantValveController, true },
                        { Device.Capability.OutputValveController, false },
                    }).ToArray());
                }
                else if (req.Type == Type.SetActuators)
                {
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
                        _responses.Enqueue(GenerateData().ToArray());
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
                            new PIDValue(25.0f),
                            new BeadThermistorValue(520f, 3.5f),
                            new ThermometerValue(Device.Sensor.OdorSourceThermometer, 27.0f),
                            new HumidityValue(Device.Sensor.InputAirHumiditySensor, 60f, 27.1f),
                            new PressureValue(1200f, 27.2f),
                            new GasValue(Device.Sensor.OdorantFlowSensor, 5.0f, 27.5f, 1001.0f),
                            new ValveValue(Device.Sensor.OdorantValveSensor, true),
                        }),
                        new Measurement(Device.ID.Odor1, new SensorValue[]
                        {
                            new GasValue(Device.Sensor.OdorantFlowSensor, 0.1f, 27.4f, 1002.0f),
                            new ValveValue(Device.Sensor.OdorantValveSensor, true),
                        }),
                        new Measurement(Device.ID.Odor2, new SensorValue[]
                        {
                            new GasValue(Device.Sensor.OdorantFlowSensor, 0.05f, 27.3f, 1003.0f),
                            new ValveValue(Device.Sensor.OdorantValveSensor, true),
                        }),
                    });
        }
    }
}
