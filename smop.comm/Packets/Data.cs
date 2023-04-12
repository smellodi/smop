using System.Collections.Generic;
using System.Linq;

namespace SMOP.OdorDisplay.Packets
{
    public class Data : Response
    {
        public int Timestamp { get; }
        public Measurement[] Measurements { get; }

        public static Data? From(Response response)
        {
            if (response?.Type != Type.Data || response?.Payload.Length < 7)
            {
                return null;
            }

            return new Data(response!.Payload);
        }

        public Data(byte[] payload) : base(Type.Data, payload)
        {
            int index = 4;
            Timestamp = (int)new FourBytes(payload[0..index]).Int;

            List<Measurement> measurements = new();
            Measurement? measurement = null;

            while ((index + 1) < payload.Length)    // at least 2 bytes must be ahead to continue the loop
            {
                byte deviceOrType = payload[index++];
                if ((deviceOrType & DEVICE_MASK) > 0)          // device type has 0x80 mask applied
                {
                    Device.ID device = (Device.ID)(deviceOrType & ~DEVICE_MASK);
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

                measurement?.SensorValues.Add(sensorValue);
            }

            Measurements = measurements.ToArray();
        }

        public override string ToString() => $"{_type} [{Measurements.Length} devices]\n    Timestamp: {Timestamp}\n    " +
            string.Join("\n    ", Measurements.Select(m => m.ToString()));

        // Internal
        internal Data(int timestamp, Measurement[] measurements) : base(Type.Data, new object[] {
                FourBytes.ToArray(timestamp),
                measurements.SelectMany(m => m.ToArray()).ToArray()
            }.SelectMany(v => (byte[])v).ToArray())
        {
            Timestamp = timestamp;
            Measurements = measurements;
        }
    }

    public class Measurement
    {
        public Device.ID Device { get; }
        public List<SensorValue> SensorValues { get; } = new();
        public Measurement(Device.ID device)
        {
            Device = device;
        }
        public Measurement(Device.ID device, SensorValue[] values)
        {
            Device = device;
            SensorValues.AddRange(values);
        }
        public byte[] ToArray()
        {
            List<byte> result = new() { (byte)((byte)Device | Packet.DEVICE_MASK) };
            foreach (var sensorValue in SensorValues)
            {
                result.AddRange(sensorValue.ToArray());
            }
            return result.ToArray();
        }
        public override string ToString() => $"{Device}:\n      " + string.Join("\n      ", SensorValues.Select(sv => sv.ToString()));
    }

    public abstract class SensorValue
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

            if (offset + dataLength > data.Length)
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
                    => new PIDValue(sensorData),
                Device.Sensor.ChassisThermometer or Device.Sensor.OdorSourceThermometer or Device.Sensor.GeneralPurposeThermometer
                    => new ThermometerValue(sensor, sensorData),
                Device.Sensor.BeadThermistor
                    => new BeadThermistorValue(sensorData),
                Device.Sensor.InputAirHumiditySensor or Device.Sensor.OutputAirHumiditySensor
                    => new HumidityValue(sensor, sensorData),
                Device.Sensor.PressureSensor
                    => new PressureValue(sensorData),
                Device.Sensor.OdorantFlowSensor or Device.Sensor.DilutionAirFlowSensor
                    => new GasValue(sensor, sensorData),
                Device.Sensor.OdorantValveSensor or Device.Sensor.OutputValveSensor
                    => new ValveValue(sensor, sensorData),
                _ => throw new System.Exception($"Unknown sensor '{sensor}'")
            };
        }


        public virtual byte[] ToArray()
        {
            List<byte> result = new() { (byte)Sensor };
            result.AddRange(Data);
            return result.ToArray();
        }

        // Internal

        protected const int SENSOR_NAME_LENGTH = 25;

        protected SensorValue(Device.Sensor sensor, byte[] data)
        {
            Sensor = sensor;
            Data = data;
        }
    }

    public class PIDValue : SensorValue
    {
        public float Volts { get; }
        public PIDValue(byte[] data) : base(Device.Sensor.PID, data)
        {
            Volts = FourBytes.ToFloat(data);
        }
        public PIDValue(float value) : base(Device.Sensor.PID, FourBytes.ToArray(value))
        {
            Volts = value;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Volts:F3} V";
    }
    public class BeadThermistorValue : SensorValue
    {
        public float Ohms { get; }
        public float Volts { get; }
        public BeadThermistorValue(byte[] data) : base(Device.Sensor.BeadThermistor, data)
        {
            Ohms = FourBytes.ToFloat(data[..4]);
            Volts = FourBytes.ToFloat(data[4..]);
        }
        public BeadThermistorValue(float ohms, float volts) : base(Device.Sensor.BeadThermistor, new FourBytes[] {
                new FourBytes(ohms),
                new FourBytes(volts)
            }.SelectMany(v => v.ToArray()).ToArray())
        {
            Ohms = ohms;
            Volts = volts;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Ohms:F1} Ohm, {Volts:F3} V";
    }
    public class ThermometerValue : SensorValue
    {
        public float Celsius { get; }
        public ThermometerValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Celsius = FourBytes.ToFloat(data);
        }
        public ThermometerValue(Device.Sensor sensor, float value) : base(sensor, FourBytes.ToArray(value))
        {
            Celsius = value;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Celsius:F2}°C";
    }
    public class HumidityValue : SensorValue
    {
        public float Percent { get; }
        public float Celsius { get; }
        public HumidityValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Percent = FourBytes.ToFloat(data[..4]);
            Celsius = FourBytes.ToFloat(data[4..]);
        }
        public HumidityValue(Device.Sensor sensor, float percent, float celsius) : base(sensor, new FourBytes[] {
                new FourBytes(percent),
                new FourBytes(celsius)
            }.SelectMany(v => v.ToArray()).ToArray())
        {
            Percent = percent;
            Celsius = celsius;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Percent:F1}%, {Celsius:F1}°C";
    }
    public class PressureValue : SensorValue
    {
        public float Millibars { get; }
        public float Celsius { get; }
        public PressureValue(byte[] data) : base(Device.Sensor.PressureSensor, data)
        {
            Millibars = FourBytes.ToFloat(data[..4]);
            Celsius = FourBytes.ToFloat(data[4..]);
        }
        public PressureValue(float millibars, float celsius) : base(Device.Sensor.PressureSensor, new FourBytes[] {
                new FourBytes(millibars),
                new FourBytes(celsius)
            }.SelectMany(v => v.ToArray()).ToArray())
        {
            Millibars = millibars;
            Celsius = celsius;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Millibars:F1} mB, {Celsius:F1}°C";
    }
    public class GasValue : SensorValue
    {
        public float SLPM { get; }
        public float Celsius { get; }
        public float Millibars { get; }
        public GasValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            SLPM = FourBytes.ToFloat(data[..4]);
            Celsius = FourBytes.ToFloat(data[4..8]);
            Millibars = FourBytes.ToFloat(data[8..]);
        }
        public GasValue(Device.Sensor sensor, float slpm, float celsius, float millibars) : base(sensor, new FourBytes[] {
                new FourBytes(slpm),
                new FourBytes(celsius),
                new FourBytes(millibars),
            }.SelectMany(v => v.ToArray()).ToArray())
        {
            SLPM = slpm;
            Celsius = celsius;
            Millibars = millibars;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {SLPM:F4} L/min, {Millibars:F1} mB, {Celsius:F1}°C";
    }
    public class ValveValue : SensorValue
    {
        public bool Opened { get; }
        public ValveValue(Device.Sensor sensor, byte[] data) : base(sensor, data)
        {
            Opened = data[0] != 0;
        }
        public ValveValue(Device.Sensor sensor, bool opened) : base(sensor, new byte[] { (byte)(opened ? 1 : 0) })
        {
            Opened = opened;
        }
        public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Opened.AsFlag()}";
    }
}
