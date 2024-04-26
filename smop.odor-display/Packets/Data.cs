using System.Collections.Generic;
using System.Linq;

namespace Smop.OdorDisplay.Packets;

public class Data : Response
{
    public int Timestamp { get; }
    public Sensors[] Measurements { get; }

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

        List<Sensors> measurements = new();
        Sensors? measurement = null;

        while ((index + 1) < payload.Length)    // at least 2 bytes must be ahead to continue the loop
        {
            byte deviceOrType = payload[index++];
            if ((deviceOrType & DEVICE_MASK) > 0)          // device type has 0x80 mask applied
            {
                Device.ID device = (Device.ID)(deviceOrType & ~DEVICE_MASK);
                measurement = new Sensors(device);
                measurements.Add(measurement);
                deviceOrType = payload[index++];
            }

            Device.Sensor sensor = (Device.Sensor)deviceOrType;
            var sensorValue = Sensor.Value.Create(sensor, payload, ref index);
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
    internal Data(int timestamp, Sensors[] measurements) : base(Type.Data, new object[] {
            FourBytes.ToArray(timestamp),
            measurements.SelectMany(m => m.ToArray()).ToArray()
        }.SelectMany(v => (byte[])v).ToArray())
    {
        Timestamp = timestamp;
        Measurements = measurements;
    }
}

public class Sensors
{
    public Device.ID Device { get; }
    public List<Sensor.Value> SensorValues { get; } = new();
    public Sensors(Device.ID device)
    {
        Device = device;
    }
    public Sensors(Device.ID device, Sensor.Value[] values)
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
