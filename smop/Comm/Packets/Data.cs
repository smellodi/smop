using System.Collections.Generic;

namespace SMOP.Comm.Packets
{
    public class Data : Response
    {
        public int Timestamp { get; }
        public Measurement[] Measurements { get; }

        public static Data? From(Response msg)
        {
            if (msg?.Type != PacketType.Data || msg?.Payload.Length < 1)
            {
                return null;
            }

            return new Data(msg!.Payload);
        }

        public Data(byte[] payload) : base(PacketType.Data, payload)
        {
            int index = 4;
            Timestamp = (int)new BtoD(payload[0..index]).D;

            List<Measurement> measurements = new();
            Measurement? measurement = null;

            while ((index + 1) < payload.Length)    // at least 2 bytes must be ahead to continue the loop
            {
                byte deviceOrType = payload[index++];
                if ((deviceOrType & 0x80) > 0)          // device type has 0x80 mask applied
                { 
                    Device.ID device = (Device.ID)(deviceOrType & 0x7F);
                    measurement = new Measurement(device);
                    measurements.Add(measurement);
                    deviceOrType = payload[index++];
                }

                Device.Sensor sensor = (Device.Sensor)deviceOrType;
                var sensorValue = SensorValue.Create(sensor, payload, ref index);
                if (sensorValue == null)
                {
                    break;
                }

                measurement?.Entries.Add(sensorValue);
            }

            Measurements = measurements.ToArray();
        }
        public override string ToString()
        {
            return $"{_type} [{Measurements.Length} devices]";
        }
    }

    public class Measurement
    {
        public Device.ID Device { get; }
        public List<SensorValue> Entries { get; } = new();
        public Measurement(Device.ID device)
        {
            Device = device;
        }
    }

    public class SensorValue
    {
        public Device.Sensor Sensor { get; }
        public byte[] Data { get; }
        public static SensorValue? Create(Device.Sensor sensor, byte[] data, ref int offset)
        {
            int dataLength = sensor switch
            {
                Device.Sensor.PID or Device.Sensor.ChassisThermometer or Device.Sensor.OdorSourceThermometer or Device.Sensor.GeneralPurposeThermometer
                    => 4,
                Device.Sensor.BeadThermistor or Device.Sensor.InputAirHumiditySensor or Device.Sensor.OutputAirHumiditySensor or Device.Sensor.PressureSensor
                    => 8,
                Device.Sensor.OdorantFlowSensor or Device.Sensor.DilutionAirFlowSensor
                    => 12,
                Device.Sensor.OdorantValveSensor or Device.Sensor.OutputValveSensor
                    => 1,
                _ => throw new System.Exception($"Unknown sensor '{sensor}'")
            };

            if (offset + dataLength >= data.Length)
            {
                return null;
            }

            int startIndex = offset;
            int endIndex = offset + dataLength;
            var sensorData = data[startIndex..endIndex];

            offset += dataLength;

            return sensor switch
            {
                Device.Sensor.PID
                    => new PIDValue(sensor, sensorData),
                Device.Sensor.ChassisThermometer or Device.Sensor.OdorSourceThermometer or Device.Sensor.GeneralPurposeThermometer
                    => new ThermometerValue(sensor, sensorData),
                Device.Sensor.BeadThermistor
                    => new BeadThermistorValue(sensor, sensorData),
                Device.Sensor.InputAirHumiditySensor or Device.Sensor.OutputAirHumiditySensor
                    => new HumidityValue(sensor, sensorData),
                Device.Sensor.PressureSensor
                    => new PressureValue(sensor, sensorData),
                Device.Sensor.OdorantFlowSensor or Device.Sensor.DilutionAirFlowSensor
                    => new GasValue(sensor, sensorData),
                Device.Sensor.OdorantValveSensor or Device.Sensor.OutputValveSensor
                    => new ValveValue(sensor, sensorData),
                _ => throw new System.Exception($"Unknown sensor '{sensor}'")
            };
        }

        protected SensorValue(Device.Sensor sensor, byte[] data)
        {
            Sensor = sensor;
            Data = data;
        }
    }

    public class PIDValue : SensorValue
    {
        public float Volts { get; }
        public PIDValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Volts = new BtoD(data).F;
        }
    }
    public class BeadThermistorValue : SensorValue
    {
        public float Ohms { get; }
        public float Volts { get; }
        public BeadThermistorValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Ohms = new BtoD(data[..4]).F;
            Volts = new BtoD(data[4..]).F;
        }
    }
    public class ThermometerValue : SensorValue
    {
        public float Celsius { get; }
        public ThermometerValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Celsius = new BtoD(data).F;
        }
    }
    public class HumidityValue : SensorValue
    {
        public float Percent { get; }
        public float Celsius { get; }
        public HumidityValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Percent = new BtoD(data[..4]).F;
            Celsius = new BtoD(data[4..]).F;
        }
    }
    public class PressureValue : SensorValue
    {
        public float Millibars { get; }
        public float Celsius { get; }
        public PressureValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Millibars = new BtoD(data[..4]).F;
            Celsius = new BtoD(data[4..]).F;
        }
    }
    public class GasValue : SensorValue
    {
        public float SLPM { get; }
        public float Celsius { get; }
        public float Millibars { get; }
        public GasValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            SLPM = new BtoD(data[..4]).F;
            Celsius = new BtoD(data[4..8]).F;
            Millibars = new BtoD(data[8..]).F;
        }
    }
    public class ValveValue : SensorValue
    {
        public bool Opened { get; }
        public ValveValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Opened = new BtoD(data).D != 0;
        }
    }
}
